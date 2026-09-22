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

    // Caller (GlassesLexer.DetectType) đã ToUpper() trước khi gọi — không cần
    // làm lại (tránh cấp phát thêm 1 string mỗi lần gọi trên hot path parse).
    public static bool Contains(string value)
    {
        return Colors.Contains(value);
    }
}