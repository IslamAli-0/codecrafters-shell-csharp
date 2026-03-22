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
            // --- UPGRADED REDIRECTION INTERCEPTOR ---
            string redirectPath = string.Empty;
            int redirectType = 0; // 0 = none, 1 = stdout, 2 = stderr

            // 1. First, check for the new 2> operator
            int redirectIndex = command.IndexOf(" 2> ");
            string operatorStr = " 2> ";

            if (redirectIndex != -1)
            {
                redirectType = 2; // We are redirecting errors!
            }
            else
            {
                // 2. If it's not 2>, check for our old 1> and > operators
                redirectIndex = command.IndexOf(" 1> ");
                operatorStr = " 1> ";

                if (redirectIndex == -1)
                {
                    redirectIndex = command.IndexOf(" > ");
                    operatorStr = " > ";
                }

                if (redirectIndex != -1)
                {
                    redirectType = 1; // We are redirecting normal output!
                }
            }

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
                string outputText = command.Substring(5).Trim().Trim('\'', '"');

                if (redirectType == 1)
                {
                    // Redirecting stdout: write to file instead of screen
                    File.WriteAllText(redirectPath, outputText + "\n");
                }
                else
                {
                    // If no redirection OR if redirecting stderr (2>), stdout still prints to screen!
                    Console.WriteLine(outputText);

                    // If they redirected stderr, create the blank file since echo has no errors
                    if (redirectType == 2)
                    {
                        File.WriteAllText(redirectPath, "");
                    }
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
                    process.StartInfo.UseShellExecute = false;

                    // NEW: Check which pipe to trap based on what the user typed
                    if (redirectType == 1)
                    {
                        process.StartInfo.RedirectStandardOutput = true;
                    }
                    else if (redirectType == 2)
                    {
                        process.StartInfo.RedirectStandardError = true;
                    }

                    process.Start();

                    // NEW: Grab the correct trapped output and save it
                    if (redirectType == 1)
                    {
                        string trappedOutput = process.StandardOutput.ReadToEnd();
                        File.WriteAllText(redirectPath, trappedOutput);
                    }
                    else if (redirectType == 2)
                    {
                        string trappedError = process.StandardError.ReadToEnd();
                        File.WriteAllText(redirectPath, trappedError);
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

