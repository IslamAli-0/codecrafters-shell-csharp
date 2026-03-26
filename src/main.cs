using System.Diagnostics;
using System.Text;
using System.IO;
using System;

class Program
{
    static void Main()
    {
        while (true)
        {
            Console.Write("$ ");
            // Use new raw method instead of Console.ReadLine()
            string command = ReadCommand();

            // --- NEW PIPELINE INTERCEPTOR ---
            if (command.Contains(" | "))
            {
                // Split the command in half at the pipe
                int pipeIndex = command.IndexOf(" | ");
                string leftCmd = command.Substring(0, pipeIndex).Trim();
                string rightCmd = command.Substring(pipeIndex + 3).Trim();

                ExecutePipeline(leftCmd, rightCmd);
                continue; // Skip the rest of the loop, the pipeline handled it!
            }

            if (string.IsNullOrWhiteSpace(command)) continue;

            // --- REDIRECTION INTERCEPTOR ---
            string redirectPath = string.Empty;
            int redirectType = 0; // 0 = none, 1 = stdout, 2 = stderr
            bool appendMode = false;

            int redirectIndex = -1;
            string operatorStr = "";

            // CRITICAL: Check for longest operators first!
            if (command.Contains(" 2>> ")) { redirectIndex = command.IndexOf(" 2>> "); operatorStr = " 2>> "; redirectType = 2; appendMode = true; }
            else if (command.Contains(" 1>> ")) { redirectIndex = command.IndexOf(" 1>> "); operatorStr = " 1>> "; redirectType = 1; appendMode = true; }
            else if (command.Contains(" >> ")) { redirectIndex = command.IndexOf(" >> "); operatorStr = " >> "; redirectType = 1; appendMode = true; }
            else if (command.Contains(" 1> ")) { redirectIndex = command.IndexOf(" 1> "); operatorStr = " 1> "; redirectType = 1; }
            else if (command.Contains(" 2> ")) { redirectIndex = command.IndexOf(" 2> "); operatorStr = " 2> "; redirectType = 2; }
            else if (command.Contains(" > ")) { redirectIndex = command.IndexOf(" > "); operatorStr = " > "; redirectType = 1; }

            // If we found ANY redirection operator, slice the string
            if (redirectIndex != -1)
            {
                redirectPath = command.Substring(redirectIndex + operatorStr.Length).Trim();
                command = command.Substring(0, redirectIndex).Trim();
            }
            // -------------------------------------

            if (command == "exit")
            {
                break;
            }
            else if (command == "pwd")
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
                continue;
            }
            else if (command.StartsWith("cd "))
            {
                string dir = command.Substring(3).Trim();
                if (dir == "~")
                {
                    dir = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;
                    Directory.SetCurrentDirectory(dir);
                }
                else if (dir.StartsWith("/"))
                {
                    if (Directory.Exists(dir))
                    {
                        Directory.SetCurrentDirectory(dir);
                    }
                    else
                    {
                        Console.WriteLine($"cd: {dir}: No such file or directory");
                    }
                }
                else
                {
                    dir = Path.GetFullPath(dir);
                    if (Directory.Exists(dir))
                    {
                        Directory.SetCurrentDirectory(dir);
                    }
                    else
                    {
                        Console.WriteLine($"cd: {dir}: No such file or directory");
                    }
                }
                continue;
            }
            else if (command.StartsWith("echo ")) // Added space here to ensure it only catches "echo "
            {
                string outputText = command.Substring(5).Trim().Trim('\'', '"');

                if (redirectType == 1)
                {
                    if (appendMode) File.AppendAllText(redirectPath, outputText + "\n");
                    else File.WriteAllText(redirectPath, outputText + "\n");
                }
                else
                {
                    Console.WriteLine(outputText);

                    if (redirectType == 2)
                    {
                        if (appendMode) File.AppendAllText(redirectPath, "");
                        else File.WriteAllText(redirectPath, "");
                    }
                }
                continue;
            }
            else if (command.StartsWith("type "))
            {
                string target = command.Substring(5).Trim();

                if (target == "type" || target == "exit" || target == "echo" || target == "pwd" || target == "cd")
                {
                    Console.WriteLine($"{target} is a shell builtin");
                }
                else
                {
                    string PathofFile;
                    bool ExistsAndExecutable = FileExistsAndExecutable(target, out PathofFile);

                    if (ExistsAndExecutable)
                    {
                        Console.WriteLine($"{target} is {PathofFile}");
                    }
                    else
                    {
                        Console.WriteLine($"{target}: not found");
                    }
                }
                continue;
            }
            else
            {
                string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                string programName = parts[0];
                string arguments = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";

                bool ExistsAndExecutable = FileExistsAndExecutable(programName);

                if (ExistsAndExecutable)
                {
                    Process process = new Process();
                    process.StartInfo.FileName = programName;
                    process.StartInfo.Arguments = arguments;
                    process.StartInfo.UseShellExecute = false;

                    // Set up the correct pipe trapping
                    if (redirectType == 1) process.StartInfo.RedirectStandardOutput = true;
                    else if (redirectType == 2) process.StartInfo.RedirectStandardError = true;

                    process.Start();

                    // Grab and save the trapped output
                    if (redirectType == 1)
                    {
                        string trappedOutput = process.StandardOutput.ReadToEnd();
                        if (appendMode) File.AppendAllText(redirectPath, trappedOutput);
                        else File.WriteAllText(redirectPath, trappedOutput);
                    }
                    else if (redirectType == 2)
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
            // Handle the echo \n escaped characters the tester is sending
            string text = command.Substring(5).Trim().Trim('\'', '"');
            return text.Replace("\\n", "\n") + "\n";
        }
        if (command == "pwd")
        {
            return Directory.GetCurrentDirectory() + "\n";
        }
        if (command.StartsWith("type "))
        {
            string target = command.Substring(5).Trim();
            string[] builtins = { "type", "exit", "echo", "pwd", "cd" };
            if (builtins.Contains(target)) return $"{target} is a shell builtin\n";

            if (FileExistsAndExecutable(target, out string path)) return $"{target} is {path}\n";
            return $"{target}: not found\n";
        }
        return null; // Not a builtin
    }
    private static void RunExternalPipeline(string leftCommand, string rightCommand)
    {
        // 1. Parse Left Command
        string[] leftParts = leftCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string leftProgram = leftParts[0];
        string leftArgs = leftParts.Length > 1 ? string.Join(" ", leftParts[1..]) : "";

        // 2. Parse Right Command
        string[] rightParts = rightCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string rightProgram = rightParts[0];
        string rightArgs = rightParts.Length > 1 ? string.Join(" ", rightParts[1..]) : "";

        if (!FileExistsAndExecutable(leftProgram) || !FileExistsAndExecutable(rightProgram))
        {
            Console.WriteLine("Command not found in pipeline");
            return;
        }

        // 4. Set up Left Process (The Talker)
        Process leftProcess = new Process();
        leftProcess.StartInfo.FileName = leftProgram;
        leftProcess.StartInfo.Arguments = leftArgs;
        leftProcess.StartInfo.UseShellExecute = false;
        leftProcess.StartInfo.RedirectStandardOutput = true; // We MUST redirect this to grab it

        // 5. Set up Right Process (The Listener)
        Process rightProcess = new Process();
        rightProcess.StartInfo.FileName = rightProgram;
        rightProcess.StartInfo.Arguments = rightArgs;
        rightProcess.StartInfo.UseShellExecute = false;
        rightProcess.StartInfo.RedirectStandardInput = true;  // We MUST redirect this to feed it

        // UPGRADE 1: Let the Right program talk directly to the screen! (No ReadToEnd needed)
        rightProcess.StartInfo.RedirectStandardOutput = false;

        // 6. Start them both simultaneously
        leftProcess.Start();
        rightProcess.Start();

        // 7. The Active Bucket Brigade
        Task.Run(() =>
        {
            try
            {
                byte[] buffer = new byte[4096];
                int bytesRead;

                // UPGRADE 2: Read chunks as they arrive, even if the bucket isn't full
                while ((bytesRead = leftProcess.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    rightProcess.StandardInput.BaseStream.Write(buffer, 0, bytesRead);

                    // CRITICAL: Shove the data down the pipe immediately so 'head' gets it!
                    rightProcess.StandardInput.BaseStream.Flush();
                }
            }
            catch
            {
                // Catches the Broken Pipe if the Right process exits early
            }
            finally
            {
                rightProcess.StandardInput.Close(); // Slam the door shut
            }
        });

        // 8. Wait for the Right process to officially finish 
        // (It will exit natively once it hits its 5-line limit)
        rightProcess.WaitForExit();

        // 9. The Assassination
        if (!leftProcess.HasExited)
        {
            try { leftProcess.Kill(); } catch { } // Safely kill the infinite tail
        }
    }

    private static void ExecutePipeline(string leftCommand, string rightCommand)
    {
        // Try to run the left and right as built-ins first
        string? leftBuiltinOutput = ExecuteBuiltin(leftCommand);
        bool rightIsBuiltin = IsBuiltin(rightCommand);

        // --- CASE 1: Built-in | External (e.g., echo "hi" | wc) ---
        if (leftBuiltinOutput != null && !rightIsBuiltin)
        {
            var (prog, args) = ParseCommand(rightCommand);
            Process rightProc = StartProcess(prog, args, redirectIn: true, redirectOut: false);

            // Directly shove the string from C# into the program's mouth
            rightProc.StandardInput.Write(leftBuiltinOutput);
            rightProc.StandardInput.Close();
            rightProc.WaitForExit();
        }
        // --- CASE 2: External | Built-in (e.g., ls | type exit) ---
        else if (leftBuiltinOutput == null && rightIsBuiltin)
        {
            var (prog, args) = ParseCommand(leftCommand);
            Process leftProc = StartProcess(prog, args, redirectIn: false, redirectOut: true);

            // We execute the built-in logic and ignore the left process's output
            // (Most built-ins like 'type' or 'cd' don't read from stdin)
            string? result = ExecuteBuiltin(rightCommand);
            Console.Write(result);

            leftProc.Kill(); // We don't need the left side anymore
        }
        // --- CASE 3: External | External (The "Bucket Brigade") ---
        else if (leftBuiltinOutput == null && !rightIsBuiltin)
        {
            // This is your existing code logic for two external processes
            RunExternalPipeline(leftCommand, rightCommand);
        }
        // --- CASE 4: Built-in | Built-in (e.g., echo "hi" | type pwd) ---
        else
        {
            string? result = ExecuteBuiltin(rightCommand);
            Console.Write(result);
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
        string cmd = command.Split(' ')[0];
        return new[] { "echo", "exit", "type", "pwd", "cd" }.Contains(cmd);
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

}