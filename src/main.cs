class Program
{
    static void Main()
    {
        // TODO: Uncomment the code below to pass the first stage
        while(true){
            string command = String.Empty;
            Console.Write("$ ");
            command = Console.ReadLine();
            Console.Write($"{command}: command not found");
        }

    }
}
