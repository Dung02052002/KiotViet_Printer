namespace KiotVietLabelPrinter.Services.Glasses.Dictionaries;

public static class ColorDictionary
{
    private static readonly HashSet<string> _colors =
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
        "BEIGE",
        "YELLOW",
        "PURPLE",
        "ORANGE",
        "SILVER",
        "GOLD"
    ];

    public static bool Contains(string value)
    {
        return _colors.Contains(value.ToUpper());
    }
}