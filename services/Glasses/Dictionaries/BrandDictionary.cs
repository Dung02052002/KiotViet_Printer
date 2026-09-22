namespace KiotVietLabelPrinter.Services.Glasses.Dictionaries;

public static class BrandDictionary
{
    private static readonly HashSet<string> _brands =
    [
        "PUCINI",
        "BOLON",
        "RAYBAN",
        "GUCCI",
        "PRADA",
        "MOLSION",
        "EXFASH",
        "PARIM",
        "CHEMI",
        "ESSILOR",
        "ZEISS",
        "HOGA",
        "OUTDO",
        "EYEPLAY",
        "POLAROID"
    ];

    // Caller (GlassesLexer.DetectType) đã ToUpper() trước khi gọi — không cần
    // làm lại (tránh cấp phát thêm 1 string mỗi lần gọi trên hot path parse).
    public static bool Contains(string value)
    {
        return _brands.Contains(value);
    }
}