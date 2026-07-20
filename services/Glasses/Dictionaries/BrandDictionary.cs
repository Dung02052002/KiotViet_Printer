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

    public static bool Contains(string value)
    {
        return _brands.Contains(value.ToUpper());
    }
}