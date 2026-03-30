using System.Diagnostics;
using System.Text;
using System.IO;
using System;

class Program
{

    private static List<string> commandHistory = new List<string>();

    private static int lastSavedHistoryIndex = 0;
    static void Main()
    {
        string? histFile = Environment.GetEnvironmentVariable("HISTFILE");

        if (!string.IsNullOrEmpty(histFile) && File.Exists(histFile))
        {
            string[] lines = File.ReadAllLines(histFile);
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    commandHistory.Add(line);
                }
            }

            lastSavedHistoryIndex = commandHistory.Count;
        }

        while (true)
        {
            Console.Write("$ ");
            string command = ReadCommand();

            if (string.IsNullOrWhiteSpace(command)) continue;

            commandHistory.Add(command);

            if (command.Contains("|"))
            {
                string[] stages = command.Split('|', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < stages.Length; i++) stages[i] = stages[i].Trim();

                ExecuteMultiStagePipeline(stages);
                continue;
            }
            string redirectPath = string.Empty;
            int redirectType = 0;
            bool appendMode = false;
            int redirectIndex = -1;
            string operatorStr = "";

            // Check longest operators first to avoid partial matches.
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

            if (command == "exit")
            {
                if (!string.IsNullOrEmpty(histFile))
                {
                    var newCommands = commandHistory.Skip(lastSavedHistoryIndex).ToList();
                    File.AppendAllLines(histFile, newCommands);
                }

                break;
            }
            string? builtinOutput = ExecuteBuiltin(command);
            if (builtinOutput != null)
            {
                if (redirectType == 1)
                {
                    if (appendMode) File.AppendAllText(redirectPath, builtinOutput);
                    else File.WriteAllText(redirectPath, builtinOutput);
                }
                else
                {
                    Console.Write(builtinOutput);

                    if (redirectType == 2)
                    {
                        if (appendMode) File.AppendAllText(redirectPath, "");
                        else File.WriteAllText(redirectPath, "");
                    }
                }
                continue;
            }

            ExecuteExternal(command, redirectType, redirectPath, appendMode);
        }
    }

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
        List<string> tokens = ParseInput(command);
        if (tokens.Count == 0) return null;

        string cmdName = tokens[0];

        if (cmdName == "echo")
        {
            string text = string.Join(" ", tokens.Skip(1));
            return text.Replace("\\n", "\n") + "\n";
        }

        if (cmdName == "pwd")
        {
            return Directory.GetCurrentDirectory() + "\n";
        }

        if (cmdName == "cd")
        {
            string dir = tokens.Count > 1 ? tokens[1] : "~";
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
            return "";
        }

        if (cmdName == "history")
        {
            if (tokens.Count >= 3 && tokens[1] == "-r")
            {
                string filePath = tokens[2];
                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath);
                    foreach (string line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line)) commandHistory.Add(line);
                    }
                }
                lastSavedHistoryIndex = commandHistory.Count;
                return "";
            }

            else if (tokens.Count >= 3 && tokens[1] == "-w")
            {
                string filePath = tokens[2];
                File.WriteAllLines(filePath, commandHistory);
                lastSavedHistoryIndex = commandHistory.Count;
                return "";
            }

            else if (tokens.Count >= 3 && tokens[1] == "-a")
            {
                string filePath = tokens[2];
                var newCommands = commandHistory.Skip(lastSavedHistoryIndex).ToList();
                File.AppendAllLines(filePath, newCommands);
                lastSavedHistoryIndex = commandHistory.Count;
                return "";
            }

            int countToShow = commandHistory.Count;
            if (tokens.Count > 1 && int.TryParse(tokens[1], out int n))
            {
                countToShow = n;
            }

            StringBuilder sb = new StringBuilder();
            int startIndex = Math.Max(0, commandHistory.Count - countToShow);

            for (int i = startIndex; i < commandHistory.Count; i++)
            {
                sb.AppendLine($"  {i + 1}  {commandHistory[i]}");
            }
            return sb.ToString();
        }

        if (cmdName == "type")
        {
            if (tokens.Count < 2) return "";
            string target = tokens[1];

            string[] builtins = { "type", "exit", "echo", "pwd", "cd", "history" };
            if (builtins.Contains(target)) return $"{target} is a shell builtin\n";

            if (FileExistsAndExecutable(target, out string path))
            {
                return $"{target} is {path}\n";
            }

            return $"{target}: not found\n";
        }

        return null;
    }

    private static List<string> ParseInput(string input)
    {
        List<string> args = new List<string>();
        StringBuilder currentArg = new StringBuilder();

        bool inSingleQuotes = false;
        bool inDoubleQuotes = false; // Track double quotes
        bool inArg = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            // Toggle single quotes (ONLY if we aren't currently inside double quotes)
            if (c == '\'' && !inDoubleQuotes)
            {
                inSingleQuotes = !inSingleQuotes;
                inArg = true;
            }
            // Toggle double quotes (ONLY if we aren't currently inside single quotes)
            else if (c == '"' && !inSingleQuotes)
            {
                inDoubleQuotes = !inDoubleQuotes;
                inArg = true;
            }
            // End of word (space) ONLY if we are outside both types of quotes
            else if (c == ' ' && !inSingleQuotes && !inDoubleQuotes)
            {
                if (inArg)
                {
                    args.Add(currentArg.ToString());
                    currentArg.Clear();
                    inArg = false;
                }
            }
            else
            {
                currentArg.Append(c);
                inArg = true;
            }
        }

        if (inArg)
        {
            args.Add(currentArg.ToString());
        }

        return args;
    }
    private static void ExecuteExternal(string command, int redirectType, string redirectPath, bool appendMode)
    {
        // Use the new Tokenizer
        List<string> tokens = ParseInput(command);
        if (tokens.Count == 0) return;

        string programName = tokens[0];
        List<string> arguments = tokens.Skip(1).ToList();

        if (FileExistsAndExecutable(programName))
        {
            bool redirectOutput = (redirectType == 1);
            bool redirectError = (redirectType == 2);

            Process process = new Process();
            process.StartInfo.FileName = programName;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = redirectOutput;
            process.StartInfo.RedirectStandardError = redirectError;

            // Use ArgumentList to safely pass the preserved strings
            foreach (string arg in arguments)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

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
            Console.WriteLine($"{programName}: command not found");
        }
    }

    private static void ExecuteMultiStagePipeline(string[] stages)
    {
        List<Process> processes = new List<Process>();
        Stream? previousOutput = null;

        for (int i = 0; i < stages.Length; i++)
        {
            string currentStage = stages[i];

            string? builtinResult = ExecuteBuiltin(currentStage);

            if (builtinResult != null)
            {
                if (i == stages.Length - 1)
                {
                    Console.Write(builtinResult);
                }
                else
                {
                    previousOutput = new MemoryStream(Encoding.UTF8.GetBytes(builtinResult));
                }
            }
            else
            {
                List<string> tokens = ParseInput(currentStage);

                if (tokens.Count == 0) continue;

                string prog = tokens[0];

                List<string> args = tokens.Skip(1).ToList();

                bool redirectIn = (previousOutput != null || i > 0);
                bool redirectOut = (i < stages.Length - 1);

                Process proc = StartProcess(prog, args, redirectIn, redirectOut);

                processes.Add(proc);

                if (previousOutput != null)
                {
                    LinkStages(previousOutput, proc.StandardInput.BaseStream);
                }

                if (i < stages.Length - 1)
                {
                    previousOutput = proc.StandardOutput.BaseStream;
                }
            }
        }

        if (processes.Count > 0)
        {
            processes.Last().WaitForExit();
        }

        foreach (var p in processes)
        {
            if (!p.HasExited) try { p.Kill(); } catch { }
        }
    }
    private static string ReadCommand()
    {
        StringBuilder sb = new StringBuilder();
        bool previousKeyWasTab = false;

        int historyPointer = commandHistory.Count;

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

                if (lastSpaceIndex >= 0)
                {
                    HandleArgumentCompletion(current, lastSpaceIndex, sb, ref previousKeyWasTab);
                }
                else
                {
                    HandleCommandCompletion(current, sb, ref previousKeyWasTab);
                }
            }
            else if (keyInfo.Key == ConsoleKey.UpArrow)
            {
                if (historyPointer > 0)
                {
                    historyPointer--;
                    string previousCommand = commandHistory[historyPointer];

                    while (sb.Length > 0)
                    {
                        Console.Write("\b \b");
                        sb.Length--;
                    }

                    sb.Append(previousCommand);
                    Console.Write(previousCommand);
                }
            }
            else if (keyInfo.Key == ConsoleKey.DownArrow)
            {
                if (historyPointer < commandHistory.Count - 1)
                {
                    historyPointer++;
                    string nextCommand = commandHistory[historyPointer];

                    while (sb.Length > 0) { Console.Write("\b \b"); sb.Length--; }
                    sb.Append(nextCommand);
                    Console.Write(nextCommand);
                }
                else if (historyPointer == commandHistory.Count - 1)
                {
                    historyPointer = commandHistory.Count;
                    while (sb.Length > 0) { Console.Write("\b \b"); sb.Length--; }
                }
            }
            else
            {
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

        string prefix = strs[0];

        for (int i = 1; i < strs.Count; i++)
        {
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

    private static Process StartProcess(string prog, List<string> args, bool redirectIn, bool redirectOut)
    {
        Process p = new Process();
        p.StartInfo.FileName = prog;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.RedirectStandardInput = redirectIn;
        p.StartInfo.RedirectStandardOutput = redirectOut;

        foreach (string arg in args)
        {
            p.StartInfo.ArgumentList.Add(arg);
        }

        p.Start();
        return p;
    }

    private static void LinkStages(Stream source, Stream destination)
    {
        Task.Run(() =>
        {
            try
            {
                source.CopyTo(destination);
                destination.Flush();
            }
            catch (Exception)
            {
                // Broken pipe is normal if a downstream process exits early.
            }
            finally
            {
                // Always close destination so the next stage sees EOF.
                destination.Close();
            }
        });
    }



}