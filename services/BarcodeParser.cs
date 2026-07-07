using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public static class BarcodeParser
{
    // Các từ khóa bắt đầu của mã
    private static readonly string[] Keywords =
    {
        "model",
        "mã sp",
        "mã",
        "code",
        "ms",
        "nữ",
        "nam",
        "pucini",
        "cao cấp",
        "kim loại"
    };

    // Các từ kết thúc
    private static readonly string[] StopWords =
    {
        "black",
        "brown",
        "red",
        "blue",
        "pink",
        "white",
        "grey",
        "gray",
        "green",
        "da",
        "chiếc",
        "cm",
        "kt",
        "chất",
        "liệu",
        "hiệu"
    };

    public static string Parse(string text, string fallbackCode = "")
    {
        return ParseFull(text, fallbackCode).BarcodeCode;
    }

    public static BarcodeParseResult ParseFull(string text, string fallbackCode = "")
    {
        BarcodeParseResult result = new();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.BarcodeCode = fallbackCode?.Trim() ?? "";
            result.AttributeText = "";
            return result;
        }

        string normalized = Normalize(text);

        // =========================
        // 1) Parse mã theo logic tool cũ
        // =========================
        string parsedCode = ParseOldLogic(normalized);

        if (!string.IsNullOrWhiteSpace(parsedCode))
            result.BarcodeCode = parsedCode;
        else
            result.BarcodeCode = fallbackCode?.Trim() ?? "";

        // =========================
        // 2) Parse thuộc tính nhẹ (fallback thôi)
        // chủ yếu để ExcelService tự ưu tiên cột K/L
        // =========================
        result.AttributeText = ExtractAttributeFallback(text);

        return result;
    }

    private static string ParseOldLogic(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Ưu tiên theo từ khóa
        foreach (var key in Keywords)
        {
            string value = ParseAfterKeyword(text, key);

            if (!string.IsNullOrEmpty(value))
                return value;
        }

        // Nếu không có từ khóa thì tìm mã đầu tiên có chữ + số
        Match m = Regex.Match(text, @"\b[A-Za-z]*\d+[A-Za-z0-9\-]*\b");

        if (m.Success)
            return m.Value;

        return "";
    }

    private static string Normalize(string text)
    {
        return text.Replace("\r", " ")
                   .Replace("\n", " ")
                   .Replace("(", " ")
                   .Replace(")", " ")
                   .Replace(",", " ")
                   .Replace(";", " ")
                   .Trim();
    }

    private static string ParseAfterKeyword(string text, string keyword)
    {
        int index = text.ToLower().IndexOf(keyword);

        if (index < 0)
            return "";

        string remain = text[(index + keyword.Length)..];
        remain = remain.Trim();

        while (remain.StartsWith(":") || remain.StartsWith("-"))
        {
            remain = remain[1..].Trim();
        }

        var words = remain.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (string word in words)
        {
            string lower = word.ToLower();

            if (StopWords.Contains(lower))
                break;

            string value = Regex.Replace(word, @"[^A-Za-z0-9\-]", "");

            if (Regex.IsMatch(value, @"\d"))
                return value;
        }

        return "";
    }

    private static string ExtractAttributeFallback(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Ưu tiên ngoặc cuối
        Match m = Regex.Match(text, @"(\([^()]+\))\s*$");
        if (m.Success)
            return m.Groups[1].Value.Trim();

        return "";
    }
}