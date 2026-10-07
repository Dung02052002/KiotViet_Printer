namespace KiotVietLabelPrinter.Services.Glasses.Dictionaries;

public static class PcnDictionary
{
    public static bool Contains(string value)
    {
        value = value.ToUpper();

        return value.StartsWith("PCN");
    }
}