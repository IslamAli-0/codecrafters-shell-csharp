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

            if (command == "exit")
            {
                break;
            }
            else if (command == "pwd")
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
                continue;
            }
            else if (command.StartsWith("echo"))
            {
                command = command.Substring(5);
                Console.WriteLine(command);
                continue;
            }
            else if (command.StartsWith("type "))
            {
                // 1. Extract the target and clean up any stray whitespace
                string target = command.Substring(5).Trim();

                if (target == "type" || target == "exit" || target == "echo")
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

                string programName = parts[0]; // e.g., "custom_exe_1234"

                // Grab everything after the program name to pass as arguments
                string arguments = parts.Length > 1 ? string.Join(" ", parts[1..]) : "";

                bool ExistsAndExecutable = FileExistsAndExecutable(programName);

                if (ExistsAndExecutable)
                {
                    Process process = new Process();
                    process.StartInfo.FileName = programName; // custom_exe
                    process.StartInfo.Arguments = arguments;     // The arguments (e.g., "alice")

                    // This is required so it runs inside your shell's output window
                    process.StartInfo.UseShellExecute = false;

                    process.Start();       // Run it!
                    process.WaitForExit(); // Don't print the next "$ " prompt until it [finishes
                }
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

