public class AutocompleteHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; } = new char[] { ' ' };

    string[] matches = { "echo ", "exit " };

    public string[] GetSuggestions(string text, int index)
    {
        return matches.Where(word => word.StartsWith(text)).ToArray();
    }
}