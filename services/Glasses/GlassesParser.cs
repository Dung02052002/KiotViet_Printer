using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesParser
{
    public static string Parse(ProductRow product)
    {
        string code = Parse(product.ProductNameWithAttr);

        if (!string.IsNullOrWhiteSpace(code))
            return code;

        code = Parse(product.ProductName);

        if (!string.IsNullOrWhiteSpace(code))
            return code;

        return product.ProductCode?.Trim().ToUpper() ?? "";
    }

    public static string Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        text = Regex.Replace(text.ToUpper(), @"\s+", " ").Trim();

        Match match;

        //-------------------------------------------------------
        // CASE 1
        // MODEL XY35096
        // MODEL:P850-01
        // MÃ SP
        // CODE
        //-------------------------------------------------------

        match = Regex.Match(
            text,
            @"(?:MODEL|MÃ SP|MÃ|CODE|MS)\s*[:\-]?\s*([A-Z0-9\-]+)");

        if (match.Success)
            return match.Groups[1].Value;

        //-------------------------------------------------------
        // CASE 2
        // KÍNH PUCINI 2113-MK108
        // ==> 2113
        //-------------------------------------------------------

        match = Regex.Match(
            text,
            @"\b(\d+)-MK\d+\b");

        if (match.Success)
            return match.Groups[1].Value;

        //-------------------------------------------------------
        // CASE 3
        // KÍNH PUCINI MK113 - P8312
        // ==> P8312
        //-------------------------------------------------------

        match = Regex.Match(
            text,
            @"MK\d+\s*-\s*([A-Z]\d+)");

        if (match.Success)
            return match.Groups[1].Value;

        //-------------------------------------------------------
        // CASE 4
        // KÍNH PUCINI MK061 - BLACK
        // ==> MK061
        //-------------------------------------------------------

        match = Regex.Match(
            text,
            @"\b(MK\d+)\b");

        if (match.Success)
            return match.Groups[1].Value;

        //-------------------------------------------------------
        // CASE 5
        // KM-05-8820
        //-------------------------------------------------------

        match = Regex.Match(
            text,
            @"\b([A-Z]{2,6}-\d{2}-\d{2,10})\b");

        if (match.Success)
            return match.Groups[1].Value;

        //-------------------------------------------------------
        // CASE 6
        // XY35096
        //-------------------------------------------------------

        match = Regex.Match(
            text,
            @"\b([A-Z]{2,6}\d{3,10})\b");

        if (match.Success)
            return match.Groups[1].Value;

        return "";
    }
}