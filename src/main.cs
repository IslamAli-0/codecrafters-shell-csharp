using System.Diagnostics;    
    
    class Program
    {
        static void Main()
        {
            // TODO: Uncomment the code below to pass the first stage
            while(true){
                Console.Write("$ ");
                string? command = String.Empty;
                command = Console.ReadLine();
                if (command.Equals("exit"))
                {
                    break;
                }else if (command.StartsWith("echo"))
                {
                    command = command.Substring(5);
                    Console.WriteLine(command);
                    continue;
                }else if (command.StartsWith("type "))
                {
                    // 1. Extract the target and clean up any stray whitespace
                    string target = command.Substring(5).Trim(); 
                    
                    if (target == "type" || target == "exit" || target == "echo")
                    {
                        Console.WriteLine($"{target} is a shell builtin");
                    }
                    else
                    {
                        string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                        string[] paths = pathEnv.Split(Path.PathSeparator);
                        bool isFound = false;

                        foreach (string dir in paths)
                        {
                            // Skip empty directory strings just in case
                            if (string.IsNullOrWhiteSpace(dir)) continue; 

                            string fullPath = Path.Combine(dir, target);
                            
                            if (File.Exists(fullPath))
                            {
                                // 2. Check if the file is actually an executable
                                UnixFileMode mode = File.GetUnixFileMode(fullPath);
                                bool isExecutable = (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;

                                if (isExecutable)
                                {
                                    Console.WriteLine($"{target} is {fullPath}");
                                    isFound = true;
                                    break; 
                                }
                            }
                        }

                        if (!isFound)
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

                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                string[] paths = pathEnv.Split(Path.PathSeparator);
                bool isFound = false;
                string executablePath = string.Empty;

                    foreach (string dir in paths)
                    {
                        // Skip empty directory strings just in case
                        if (string.IsNullOrWhiteSpace(dir)) continue; 

                        string fullPath = Path.Combine(dir,programName);
                            
                        if (File.Exists(fullPath))
                        {
                            // 2. Check if the file is actually an executable
                            UnixFileMode mode = File.GetUnixFileMode(fullPath);
                            bool isExecutable = (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;

                            if (isExecutable)
                            {
                                isFound = true;
                                executablePath = fullPath;
                                break; // We found it, stop searching!
                            }
                        }
                    }

                if (isFound)
                {
                    Process process = new Process();
                    process.StartInfo.FileName = programName; // The full path to custom_exe
                    process.StartInfo.Arguments = arguments;     // The arguments (e.g., "alice")
                        
                    // This is required so it runs inside your shell's output window
                    process.StartInfo.UseShellExecute = false;   
                        
                    process.Start();       // Run it!
                    process.WaitForExit(); // Don't print the next "$ " prompt until it finishes
                }
                else
                {
                    // If we didn't find it in the PATH, print the standard error
                    Console.WriteLine($"{command}: command not found");
                }
            }
        }
    }

        }
    
