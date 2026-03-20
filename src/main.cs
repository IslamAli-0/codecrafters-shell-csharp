class Program
{
    static void Main()
    {
        // TODO: Uncomment the code below to pass the first stage
        while(true){
            Console.WriteLine("$ ");
            string command = String.Empty;
            command = Console.ReadLine();
            Console.WriteLine($"{command}: command not found");
        }

    }
}
