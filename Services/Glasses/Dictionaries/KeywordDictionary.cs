namespace KiotVietLabelPrinter.Services.Glasses.Dictionaries;

public static class KeywordDictionary
{
    private static readonly HashSet<string> Words =
    [
        "MODEL",
        "MÃ",
        "MÃ SP",
        "MS",
        "CODE"
    ];

    public static bool Contains(string value)
    {
        return Words.Contains(value.ToUpper());
    }
}