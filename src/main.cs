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
                command = command.Substring(4);
                Console.WriteLine(command);
                continue;
            }
            Console.WriteLine($"{command}: command not found");
        }

    }
}
