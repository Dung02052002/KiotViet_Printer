using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

public static class GlassesParser
{
    public static string Parse(ProductRow item)
    {
        return Parse(item.ProductNameWithAttr)
            ?? Parse(item.ProductName)
            ?? item.ProductCode;
    }

    private static string? Parse(string? text)
{
    if (string.IsNullOrWhiteSpace(text))
        return null;

    text = text.ToUpperInvariant();

    Match m;

    //====================================
    // CASE 1
    // MODEL XXX
    //====================================
    m = Regex.Match(
        text,
        @"(?:MODEL|MÃ SP|MÃ|CODE|MS)\s*[:\-]?\s*([A-Z0-9\-]+)",
        RegexOptions.IgnoreCase);

    if (m.Success)
        return m.Groups[1].Value.ToUpper();

    //====================================
    // CASE 2
    // 2113-MK108
    // => lấy 2113
    //====================================
    m = Regex.Match(
        text,
        @"\b(\d+)-MK\d+\b");

    if (m.Success)
        return m.Groups[1].Value;

    //====================================
    // CASE 3
    // MK113 - P8312
    // => lấy P8312
    //====================================
    m = Regex.Match(
        text,
        @"MK\d+\s*-\s*([A-Z]\d+)",
        RegexOptions.IgnoreCase);

    if (m.Success)
        return m.Groups[1].Value.ToUpper();

    //====================================
    // CASE 4
    // MK061
    //====================================
    m = Regex.Match(
        text,
        @"\b(MK\d+)\b",
        RegexOptions.IgnoreCase);

    if (m.Success)
        return m.Groups[1].Value.ToUpper();

    return null;
}
}