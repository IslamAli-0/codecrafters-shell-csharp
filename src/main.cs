class Program
{
    static void Main()
    {
        // TODO: Uncomment the code below to pass the first stage
        while(true){
            Console.Write("$ ");
            string command = String.Empty;
            command = Console.ReadLine();
            if (command.Equals("exit"))
            {
                break;
            }else if (command.StartsWith("echo"))
            {
                command = command.Substring(5);
                Console.WriteLine(command);
                continue;
            }else if (command.StartsWith("type"))
            {
                command = command.Substring(5);
                if (command == "type" || command == "exit" || command == "echo")
                {
                    Console.WriteLine($"{command} is a shell builtin");
                }
                else
                {
                    Console.WriteLine($"{command}: not found");
                }
                continue;
            }
            Console.WriteLine($"{command}: command not found");
        }

    }
}
