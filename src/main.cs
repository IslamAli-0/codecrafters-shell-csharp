using System.Diagnostics;
using System.Text;
using System.IO;
using System;

class Program
{

    private static List<string> commandHistory = new List<string>();
    static void Main()
    {
        while (true)
        {
            Console.Write("$ ");
            string command = ReadCommand();

            if (string.IsNullOrWhiteSpace(command)) continue;

            commandHistory.Add(command);

            // 1. HIGHEST PRIORITY: Pipelines
            // We check this first because a pipeline might contain built-ins or redirection
            if (command.Contains("|")) // Changed from " | " to just "|" to be safer
            {
                // Split the entire thing into an array: ["cat file", " head -n 3", " wc"]
                string[] stages = command.Split('|', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < stages.Length; i++) stages[i] = stages[i].Trim();

                ExecuteMultiStagePipeline(stages);
                continue;
            }
            // 2. SECOND PRIORITY: Redirection Interceptor
            // We parse this before execution so we know if we need to 'trap' the output
            string redirectPath = string.Empty;
            int redirectType = 0; // 0 = none, 1 = stdout, 2 = stderr
            bool appendMode = false;
            int redirectIndex = -1;
            string operatorStr = "";

            // Check for longest operators first to avoid partial matching (e.g., '>>' before '>')
            if (command.Contains(" 2>> ")) { redirectIndex = command.IndexOf(" 2>> "); operatorStr = " 2>> "; redirectType = 2; appendMode = true; }
            else if (command.Contains(" 1>> ")) { redirectIndex = command.IndexOf(" 1>> "); operatorStr = " 1>> "; redirectType = 1; appendMode = true; }
            else if (command.Contains(" >> ")) { redirectIndex = command.IndexOf(" >> "); operatorStr = " >> "; redirectType = 1; appendMode = true; }
            else if (command.Contains(" 1> ")) { redirectIndex = command.IndexOf(" 1> "); operatorStr = " 1> "; redirectType = 1; }
            else if (command.Contains(" 2> ")) { redirectIndex = command.IndexOf(" 2> "); operatorStr = " 2> "; redirectType = 2; }
            else if (command.Contains(" > ")) { redirectIndex = command.IndexOf(" > "); operatorStr = " > "; redirectType = 1; }

            if (redirectIndex != -1)
            {
                redirectPath = command.Substring(redirectIndex + operatorStr.Length).Trim();
                command = command.Substring(0, redirectIndex).Trim();
            }

            // 3. THIRD PRIORITY: Shell Built-ins
            // We check for 'exit' separately because it needs to 'break' the loop
            if (command == "exit") break;

            // Use the Master Function for all other built-ins
            string? builtinOutput = ExecuteBuiltin(command);
            if (builtinOutput != null)
            {
                if (redirectType == 1) // Redirect Standard Output
                {
                    if (appendMode) File.AppendAllText(redirectPath, builtinOutput);
                    else File.WriteAllText(redirectPath, builtinOutput);
                }
                else
                {
                    // Print to screen if no redirection (or if it's 2> which doesn't apply to success)
                    Console.Write(builtinOutput);

                    // Edge case: if user did 'echo hi 2> file', we print 'hi' but must create/touch the file
                    if (redirectType == 2)
                    {
                        if (appendMode) File.AppendAllText(redirectPath, "");
                        else File.WriteAllText(redirectPath, "");
                    }
                }
                continue;
            }

            // 4. LOWEST PRIORITY: External Programs
            // If nothing else claimed the command, try to run it as a file in the PATH
            ExecuteExternal(command, redirectType, redirectPath, appendMode);
        }
    }
    // --- HELPER METHODS ---
    public static bool FileExistsAndExecutable(string name, out string fullpath)
    {
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] paths = pathEnv.Split(Path.PathSeparator);

        foreach (string dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;

            string testPath = Path.Combine(dir, name);

            if (File.Exists(testPath))
            {
                if (OperatingSystem.IsWindows())
                {
                    fullpath = testPath;
                    return true;
                }
                else
                {
                    UnixFileMode mode = File.GetUnixFileMode(testPath);
                    bool isExecutable = (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;

                    if (isExecutable)
                    {
                        fullpath = testPath;
                        return true;
                    }
                }
            }
        }

        fullpath = string.Empty;
        return false;
    }
    public static bool FileExistsAndExecutable(string name)
    {
        return FileExistsAndExecutable(name, out _);
    }
    private static string? ExecuteBuiltin(string command)
    {
        if (command.StartsWith("echo "))
        {
            string text = command.Substring(5).Trim().Trim('\'', '"');
            return text.Replace("\\n", "\n") + "\n";
        }

        if (command == "pwd")
        {
            return Directory.GetCurrentDirectory() + "\n";
        }

        if (command.StartsWith("cd "))
        {
            string dir = command.Substring(3).Trim();
            try
            {
                if (dir == "~") dir = Environment.GetEnvironmentVariable("HOME") ?? "";
                else dir = Path.GetFullPath(dir);

                if (Directory.Exists(dir)) Directory.SetCurrentDirectory(dir);
                else Console.WriteLine($"cd: {dir}: No such file or directory");
            }
            catch (Exception e)
            {
                Console.WriteLine($"cd: {e.Message}");
            }
            return ""; // Success, but no output string to pipe
        }

        if (command.StartsWith("history"))
        {
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int countToShow = commandHistory.Count; // Default: show everything

            // Check if the user provided a number: "history 5"
            if (parts.Length > 1 && int.TryParse(parts[1], out int n))
            {
                countToShow = n;
            }

            StringBuilder sb = new StringBuilder();

            // Calculate where to start. 
            // Example: 10 items total, show last 3 -> start at index 7.
            int startIndex = Math.Max(0, commandHistory.Count - countToShow);

            for (int i = startIndex; i < commandHistory.Count; i++)
            {
                // The display index is always i + 1
                sb.AppendLine($"  {i + 1}  {commandHistory[i]}");
            }
            return sb.ToString();
        }

        if (command.StartsWith("type "))
        {
            // 1. Get the target (e.g., "cat" from "type cat")
            string target = command.Substring(5).Trim();

            // 2. Check Builtins
            string[] builtins = { "type", "exit", "echo", "pwd", "cd", "history" };
            if (builtins.Contains(target)) return $"{target} is a shell builtin\n";

            // 3. Check PATH
            // Make sure your FileExistsAndExecutable is actually checking /bin/cat!
            if (FileExistsAndExecutable(target, out string path))
            {
                return $"{target} is {path}\n";
            }

            // 4. Fallback
            return $"{target}: not found\n";
        }

        return null; // Not a builtin
    }

    private static void ExecuteExternal(string command, int redirectType, string redirectPath, bool appendMode)
    {
        var (programName, arguments) = ParseCommand(command);

        if (FileExistsAndExecutable(programName))
        {
            // Use your StartProcess helper to keep it clean!
            bool redirectOutput = (redirectType == 1);
            bool redirectError = (redirectType == 2);

            Process process = new Process();
            process.StartInfo.FileName = programName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = redirectOutput;
            process.StartInfo.RedirectStandardError = redirectError;
            process.Start();

            if (redirectOutput)
            {
                string trappedOutput = process.StandardOutput.ReadToEnd();
                if (appendMode) File.AppendAllText(redirectPath, trappedOutput);
                else File.WriteAllText(redirectPath, trappedOutput);
            }
            else if (redirectError)
            {
                string trappedError = process.StandardError.ReadToEnd();
                if (appendMode) File.AppendAllText(redirectPath, trappedError);
                else File.WriteAllText(redirectPath, trappedError);
            }

            process.WaitForExit();
        }
        else
        {
            Console.WriteLine($"{command}: command not found");
        }
    }

    #region Old Code Before Refactoring

    // private static void RunExternalPipeline(string leftCommand, string rightCommand)
    // {
    //     // 1. Parse Left Command
    //     string[] leftParts = leftCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    //     string leftProgram = leftParts[0];
    //     string leftArgs = leftParts.Length > 1 ? string.Join(" ", leftParts[1..]) : "";

    //     // 2. Parse Right Command
    //     string[] rightParts = rightCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    //     string rightProgram = rightParts[0];
    //     string rightArgs = rightParts.Length > 1 ? string.Join(" ", rightParts[1..]) : "";

    //     if (!FileExistsAndExecutable(leftProgram) || !FileExistsAndExecutable(rightProgram))
    //     {
    //         Console.WriteLine("Command not found in pipeline");
    //         return;
    //     }

    //     // 4. Set up Left Process (The Talker)
    //     Process leftProcess = new Process();
    //     leftProcess.StartInfo.FileName = leftProgram;
    //     leftProcess.StartInfo.Arguments = leftArgs;
    //     leftProcess.StartInfo.UseShellExecute = false;
    //     leftProcess.StartInfo.RedirectStandardOutput = true; // We MUST redirect this to grab it

    //     // 5. Set up Right Process (The Listener)
    //     Process rightProcess = new Process();
    //     rightProcess.StartInfo.FileName = rightProgram;
    //     rightProcess.StartInfo.Arguments = rightArgs;
    //     rightProcess.StartInfo.UseShellExecute = false;
    //     rightProcess.StartInfo.RedirectStandardInput = true;  // We MUST redirect this to feed it

    //     // UPGRADE 1: Let the Right program talk directly to the screen! (No ReadToEnd needed)
    //     rightProcess.StartInfo.RedirectStandardOutput = false;

    //     // 6. Start them both simultaneously
    //     leftProcess.Start();
    //     rightProcess.Start();

    //     // 7. The Active Bucket Brigade
    //     Task.Run(() =>
    //     {
    //         try
    //         {
    //             byte[] buffer = new byte[4096];
    //             int bytesRead;

    //             // UPGRADE 2: Read chunks as they arrive, even if the bucket isn't full
    //             while ((bytesRead = leftProcess.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
    //             {
    //                 rightProcess.StandardInput.BaseStream.Write(buffer, 0, bytesRead);

    //                 // CRITICAL: Shove the data down the pipe immediately so 'head' gets it!
    //                 rightProcess.StandardInput.BaseStream.Flush();
    //             }
    //         }
    //         catch
    //         {
    //             // Catches the Broken Pipe if the Right process exits early
    //         }
    //         finally
    //         {
    //             rightProcess.StandardInput.Close(); // Slam the door shut
    //         }
    //     });

    //     // 8. Wait for the Right process to officially finish 
    //     // (It will exit natively once it hits its 5-line limit)
    //     rightProcess.WaitForExit();

    //     // 9. The Assassination
    //     if (!leftProcess.HasExited)
    //     {
    //         try { leftProcess.Kill(); } catch { } // Safely kill the infinite tail
    //     }
    // }

    // private static void ExecutePipeline(string leftCommand, string rightCommand)
    // {
    //     // Try to run the left and right as built-ins first
    //     string? leftBuiltinOutput = ExecuteBuiltin(leftCommand);
    //     bool rightIsBuiltin = IsBuiltin(rightCommand);

    //     // --- CASE 1: Built-in | External (e.g., echo "hi" | wc) ---
    //     if (leftBuiltinOutput != null && !rightIsBuiltin)
    //     {
    //         var (prog, args) = ParseCommand(rightCommand);
    //         Process rightProc = StartProcess(prog, args, redirectIn: true, redirectOut: false);

    //         // Directly shove the string from C# into the program's mouth
    //         rightProc.StandardInput.Write(leftBuiltinOutput);
    //         rightProc.StandardInput.Close();
    //         rightProc.WaitForExit();
    //     }
    //     // --- CASE 2: External | Built-in (e.g., ls | type exit) ---
    //     else if (leftBuiltinOutput == null && rightIsBuiltin)
    //     {
    //         var (prog, args) = ParseCommand(leftCommand);
    //         Process leftProc = StartProcess(prog, args, redirectIn: false, redirectOut: true);

    //         // We execute the built-in logic and ignore the left process's output
    //         // (Most built-ins like 'type' or 'cd' don't read from stdin)
    //         string? result = ExecuteBuiltin(rightCommand);
    //         Console.Write(result);

    //         leftProc.Kill(); // We don't need the left side anymore
    //     }
    //     // --- CASE 3: External | External (The "Bucket Brigade") ---
    //     else if (leftBuiltinOutput == null && !rightIsBuiltin)
    //     {
    //         // This is your existing code logic for two external processes
    //         RunExternalPipeline(leftCommand, rightCommand);
    //     }
    //     // --- CASE 4: Built-in | Built-in (e.g., echo "hi" | type pwd) ---
    //     else
    //     {
    //         string? result = ExecuteBuiltin(rightCommand);
    //         Console.Write(result);
    //     }
    // }

    #endregion

    private static void ExecuteMultiStagePipeline(string[] stages)
    {
        List<Process> processes = new List<Process>();
        Stream? previousOutput = null;

        for (int i = 0; i < stages.Length; i++)
        {
            string currentStage = stages[i];

            // Check if this specific stage is a built-in
            string? builtinResult = ExecuteBuiltin(currentStage);

            if (builtinResult != null)
            {
                // If it's the LAST stage (e.g., ls | type exit), we must print it!
                if (i == stages.Length - 1)
                {
                    Console.Write(builtinResult);
                }
                else
                {
                    // If it's a middle stage, pass its string to the next one
                    previousOutput = new MemoryStream(Encoding.UTF8.GetBytes(builtinResult));
                }
            }
            else
            {
                var (prog, args) = ParseCommand(currentStage);

                // 1. Determine the "Hoses" (Redirection)
                bool redirectIn = (previousOutput != null || i > 0);
                bool redirectOut = (i < stages.Length - 1);

                Process proc = StartProcess(prog, args, redirectIn, redirectOut);

                processes.Add(proc);

                // Stream data from previous stage into this one
                if (previousOutput != null)
                {
                    LinkStages(previousOutput, proc.StandardInput.BaseStream);
                }

                // Prepare the hose for the next iteration
                if (i < stages.Length - 1)
                {
                    previousOutput = proc.StandardOutput.BaseStream;
                }
            }
        }

        // Wait for the final process to finish
        if (processes.Count > 0)
        {
            processes.Last().WaitForExit();
        }

        // Cleanup infinite processes
        foreach (var p in processes)
        {
            if (!p.HasExited) try { p.Kill(); } catch { }
        }
    }
    private static string ReadCommand()
    {
        StringBuilder sb = new StringBuilder();
        bool previousKeyWasTab = false;

        while (true)
        {
            var keyInfo = Console.ReadKey(intercept: true);

            if (keyInfo.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return sb.ToString();
            }
            else if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0)
                {
                    sb.Length--;
                    Console.Write("\b \b");
                }
                previousKeyWasTab = false;
            }
            else if (keyInfo.Key == ConsoleKey.Tab)
            {
                string current = sb.ToString();
                int lastSpaceIndex = current.LastIndexOf(' ');

                // Delegate the heavy lifting to our new helper methods!
                if (lastSpaceIndex >= 0)
                {
                    HandleArgumentCompletion(current, lastSpaceIndex, sb, ref previousKeyWasTab);
                }
                else
                {
                    HandleCommandCompletion(current, sb, ref previousKeyWasTab);
                }
            }
            else
            {
                // Normal typing
                sb.Append(keyInfo.KeyChar);
                Console.Write(keyInfo.KeyChar);
                previousKeyWasTab = false;
            }
        }
    }
    private static void HandleCommandCompletion(string current, StringBuilder sb, ref bool previousKeyWasTab)
    {
        string[] builtins = { "echo", "exit", "type", "pwd", "cd" };
        var matches = builtins.Where(b => b.StartsWith(current)).ToList();

        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] paths = pathEnv.Split(Path.PathSeparator);

        foreach (string dir in paths)
        {
            if (Directory.Exists(dir))
            {
                try
                {
                    var filesInDir = Directory.GetFiles(dir);
                    foreach (string file in filesInDir)
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.StartsWith(current)) matches.Add(fileName);
                    }
                }
                catch { }
            }
        }

        matches = matches.Distinct().ToList();

        if (matches.Count == 1)
        {
            string match = matches[0];
            string remainder = match.Substring(current.Length) + " ";
            sb.Append(remainder);
            Console.Write(remainder);
            previousKeyWasTab = false;
        }
        else if (matches.Count > 1)
        {
            string lcp = GetLongestCommonPrefix(matches);

            if (lcp.Length > current.Length)
            {
                string remainder = lcp.Substring(current.Length);
                sb.Append(remainder);
                Console.Write(remainder);
                previousKeyWasTab = false;
            }
            else
            {
                if (!previousKeyWasTab)
                {
                    Console.Write("\a");
                    previousKeyWasTab = true;
                }
                else
                {
                    Console.WriteLine();
                    matches.Sort();
                    Console.WriteLine(string.Join("  ", matches));
                    Console.Write("$ " + current);
                    previousKeyWasTab = false;
                }
            }
        }
        else
        {
            Console.Write("\a");
            previousKeyWasTab = false;
        }
    }
    private static void HandleArgumentCompletion(string current, int lastSpaceIndex, StringBuilder sb, ref bool previousKeyWasTab)
    {
        string fullArg = current.Substring(lastSpaceIndex + 1);
        string searchDir = "";
        string filePrefix = fullArg;

        int lastSlashIndex = fullArg.LastIndexOf('/');
        if (lastSlashIndex >= 0)
        {
            searchDir = fullArg.Substring(0, lastSlashIndex + 1);
            filePrefix = fullArg.Substring(lastSlashIndex + 1);
        }

        string targetDir = Path.Combine(Directory.GetCurrentDirectory(), searchDir);
        var fileMatches = new List<string>();

        if (Directory.Exists(targetDir))
        {
            try
            {
                var entries = Directory.GetFileSystemEntries(targetDir);
                foreach (var entry in entries)
                {
                    string entryName = Path.GetFileName(entry);
                    if (entryName.StartsWith(filePrefix)) fileMatches.Add(entryName);
                }
            }
            catch { }
        }

        if (fileMatches.Count == 1)
        {
            string match = fileMatches[0];
            string remainder = match.Substring(filePrefix.Length);

            string fullMatchPath = Path.Combine(targetDir, match);
            remainder += Directory.Exists(fullMatchPath) ? "/" : " ";

            sb.Append(remainder);
            Console.Write(remainder);
            previousKeyWasTab = false;
        }
        else if (fileMatches.Count > 1)
        {
            string lcp = GetLongestCommonPrefix(fileMatches);

            if (lcp.Length > filePrefix.Length)
            {
                string remainder = lcp.Substring(filePrefix.Length);
                sb.Append(remainder);
                Console.Write(remainder);
                previousKeyWasTab = false;
            }
            else
            {
                if (!previousKeyWasTab)
                {
                    Console.Write("\a");
                    previousKeyWasTab = true;
                }
                else
                {
                    Console.WriteLine();
                    fileMatches.Sort();

                    var displayMatches = new List<string>();
                    foreach (var match in fileMatches)
                    {
                        string fullMatchPath = Path.Combine(targetDir, match);
                        displayMatches.Add(Directory.Exists(fullMatchPath) ? match + "/" : match);
                    }

                    Console.WriteLine(string.Join("  ", displayMatches));
                    Console.Write("$ " + current);
                    previousKeyWasTab = false;
                }
            }
        }
        else
        {
            Console.Write("\a");
            previousKeyWasTab = false;
        }
    }
    private static string GetLongestCommonPrefix(List<string> strs)
    {
        if (strs == null || strs.Count == 0) return "";

        // Start by assuming the first word is the prefix
        string prefix = strs[0];

        for (int i = 1; i < strs.Count; i++)
        {
            // If the next word doesn't start with our prefix, chop a letter off the end and try again
            while (!strs[i].StartsWith(prefix))
            {
                prefix = prefix.Substring(0, prefix.Length - 1);
                if (prefix == "") return "";
            }
        }
        return prefix;
    }

    private static bool IsBuiltin(string command)
    {
        string cmdName = command.Split(' ')[0];
        string[] builtins = { "echo", "exit", "type", "pwd", "cd", "history" };
        return builtins.Contains(cmdName);
    }

    private static (string prog, string args) ParseCommand(string cmd)
    {
        string[] parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return (parts[0], parts.Length > 1 ? string.Join(" ", parts[1..]) : "");
    }

    private static Process StartProcess(string prog, string args, bool redirectIn, bool redirectOut)
    {
        Process p = new Process();
        p.StartInfo.FileName = prog;
        p.StartInfo.Arguments = args;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardInput = redirectIn;
        p.StartInfo.RedirectStandardOutput = redirectOut;
        p.Start();
        return p;
    }

    private static void LinkStages(Stream source, Stream destination)
    {
        Task.Run(() =>
        {
            try
            {
                // The actual "pouring" of the data bucket
                source.CopyTo(destination);
                destination.Flush();
            }
            catch (Exception)
            {
                // Broken pipes are normal when the receiver (like 'head') quits early
            }
            finally
            {
                // CRITICAL: Always close the destination so the next program 
                // knows no more data is coming.
                destination.Close();
            }
        });
    }



}