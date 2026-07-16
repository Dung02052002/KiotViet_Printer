namespace KiotVietLabelPrinter.Services.Glasses.Dictionaries;

public static class ColorDictionary
{
    private static readonly HashSet<string> Colors =
    [
        "BLACK",
        "WHITE",
        "GREY",
        "GRAY",
        "GREEN",
        "BLUE",
        "BROWN",
        "RED",
        "PINK",
        "YELLOW",
        "BEIGE",
        "PURPLE",
        "ORANGE",
        "GOLD",
        "SILVER"
    ];

    public static bool Contains(string value)
    {
        return Colors.Contains(value.ToUpper());
    }
}