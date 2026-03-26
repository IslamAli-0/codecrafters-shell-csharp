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

    private static void ExecutePipeline(string leftCommand, string rightCommand)
    {
        // 1. Parse Left Command
        string[] leftParts = leftCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string leftProgram = leftParts[0];
        string leftArgs = leftParts.Length > 1 ? string.Join(" ", leftParts[1..]) : "";

        // 2. Parse Right Command
        string[] rightParts = rightCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string rightProgram = rightParts[0];
        string rightArgs = rightParts.Length > 1 ? string.Join(" ", rightParts[1..]) : "";

        // 3. Verify both exist!
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
        leftProcess.StartInfo.RedirectStandardOutput = true; // "Don't print to screen!"

        // 5. Set up Right Process (The Listener)
        Process rightProcess = new Process();
        rightProcess.StartInfo.FileName = rightProgram;
        rightProcess.StartInfo.Arguments = rightArgs;
        rightProcess.StartInfo.UseShellExecute = false;
        rightProcess.StartInfo.RedirectStandardInput = true;  // "Listen to C#, not the keyboard!"
        rightProcess.StartInfo.RedirectStandardOutput = true; // "Send C# the final answer!"

        // 6. Start them both simultaneously
        leftProcess.Start();
        rightProcess.Start();

        // 7. The Bucket Brigade (Runs in the background so our shell doesn't freeze)
        Task.Run(() =>
        {
            try
            {
                // Instantly stream data from Left to Right as it generates
                leftProcess.StandardOutput.BaseStream.CopyTo(rightProcess.StandardInput.BaseStream);
            }
            catch
            {
                // This catches the "Broken Pipe" error if the Right process shuts down early!
            }
            finally
            {
                // CRITICAL: We MUST close the door. This tells the Right process "No more data is coming!"
                // If we don't do this, commands like 'wc' will sit frozen forever waiting for more words.
                rightProcess.StandardInput.Close();
            }
        });

        // 8. Grab the final answer from the Right process and print it
        string finalOutput = rightProcess.StandardOutput.ReadToEnd();
        Console.Write(finalOutput);

        // 9. Wait for the Right process to officially finish
        rightProcess.WaitForExit();

        // 10. The Assassination (Fixes the tail -f deadlock trap)
        if (!leftProcess.HasExited)
        {
            leftProcess.Kill();
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
}