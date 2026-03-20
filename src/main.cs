class Program
{
    static void Main()
    {
        // TODO: Uncomment the code below to pass the first stage
        Console.Write("$ ");
        while(true){
            string command = String.Empty;
            command = Console.ReadLine();
            Console.Write($"{command}: command not found");
        }

    }
}
