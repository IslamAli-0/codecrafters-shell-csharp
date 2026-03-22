using System.Diagnostics;

class Program
{
    static void Main()
    {
        // TODO: Uncomment the code below to pass the first stage
        while (true)
        {
            Console.Write("$ ");
            string? command = String.Empty;
            command = Console.ReadLine();

            if (command == null) continue;
            string redirectPath = string.Empty;

            // Check for both > and 1>
            int redirectIndex = command.IndexOf(" 1> ");
            string operatorStr = " 1> ";

            if (redirectIndex == -1)
            {
                redirectIndex = command.IndexOf(" > ");
                operatorStr = " > ";
            }

            // If we found a redirection operator
            if (redirectIndex != -1)
            {
                // Grab the file path (everything after the operator)
                redirectPath = command.Substring(redirectIndex + operatorStr.Length).Trim();

                // Chop off the redirection part so the rest of your shell just sees the normal command
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
                if (dir.StartsWith("/"))
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
            else if (command.StartsWith("echo"))
            {
                string outputText = command.Substring(5);

                if (redirectPath != string.Empty)
                {
                    // \n adds the required line break at the end of the file
                    File.WriteAllText(redirectPath, outputText + "\n");
                }
                else
                {
                    Console.WriteLine(outputText);
                }
                continue;
            }
            else if (command.StartsWith("type "))
            {
                // 1. Extract the target and clean up any stray whitespace
                string target = command.Substring(5).Trim();

                if (target == "type" || target == "exit" || target == "echo" || target == "pwd")
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
                // 1. Parse the command
                string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                string programName = parts[0];
                string arguments = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";

                bool ExistsAndExecutable = FileExistsAndExecutable(programName);

                // --- THIS IS THE STEP 3 CODE ---
                if (ExistsAndExecutable)
                {
                    Process process = new Process();
                    process.StartInfo.FileName = programName;
                    process.StartInfo.Arguments = arguments;

                    // This is required so it runs inside your shell's output window
                    process.StartInfo.UseShellExecute = false;

                    // NEW: If the user wants to redirect, trap the standard output inside C#
                    if (redirectPath != string.Empty)
                    {
                        process.StartInfo.RedirectStandardOutput = true;
                    }

                    process.Start();

                    // NEW: Grab that trapped output and save it to the file
                    if (redirectPath != string.Empty)
                    {
                        string trappedOutput = process.StandardOutput.ReadToEnd();
                        File.WriteAllText(redirectPath, trappedOutput);
                    }

                    process.WaitForExit();
                }
                // --- END OF STEP 3 CODE ---
                else
                {
                    // If we didn't find it in the PATH, print the standard error
                    Console.WriteLine($"{command}: command not found");
                }
            }
        }
    }

    // 1. The Main Worker Method
    public static bool FileExistsAndExecutable(string name, out string fullpath)
    {
        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] paths = pathEnv.Split(Path.PathSeparator);

        foreach (string dir in paths)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;

            string testPath = Path.Combine(dir, name);

            // On Windows, executables usually end in .exe, .bat, or .cmd. 
            // For testing your shell locally, we can just check if we are on Windows.
            if (File.Exists(testPath))
            {
                if (OperatingSystem.IsWindows())
                {
                    // If we are on Windows, just knowing the file exists is enough for now
                    fullpath = testPath;
                    return true;
                }
                else
                {
                    // If we are on Linux (CodeCrafters), do the strict permission check
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

    // 2. The Shortcut Method
    public static bool FileExistsAndExecutable(string name)
    {
        // The underscore '_' tells C#: "I know this method outputs a string, but I don't need it right now. Throw it away."
        return FileExistsAndExecutable(name, out _);
    }
}

