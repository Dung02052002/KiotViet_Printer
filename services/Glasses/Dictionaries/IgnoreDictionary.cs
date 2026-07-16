namespace KiotVietLabelPrinter.Services.Glasses.Dictionaries;

public static class IgnoreDictionary
{
    private static readonly HashSet<string> Words =
    [
        "CHIẾC",
        "KINH",
        "KÍNH",
        "PUCINI",
        "THỜI",
        "TRANG",
        "CAO",
        "CẤP",
        "MẪU"
    ];

    public static bool Contains(string value)
    {
        return Words.Contains(value.ToUpper());
    }
}