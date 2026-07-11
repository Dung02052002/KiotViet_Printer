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

    // Ưu tiên parse sau keyword
    foreach (string key in Keywords)
    {
        string value = ParseAfterKeyword(text, key);

        if (!string.IsNullOrWhiteSpace(value))
            return value;
    }

    // Nếu không có keyword thì tìm token giống mã hàng
    MatchCollection matches = Regex.Matches(
        text,
        @"\b[A-Za-z]{1,10}[A-Za-z0-9\-]{2,30}\b");

    foreach (Match match in matches)
    {
        string value = match.Value.ToUpper();

        // phải có ít nhất 1 số
        if (!Regex.IsMatch(value, @"\d"))
            continue;

        // bỏ token chỉ là kích thước
        if (Regex.IsMatch(value, @"^\d"))
            continue;

        // bỏ cm
        if (value.EndsWith("CM"))
            continue;

        return value;
    }

    return "";
}

  private static string Normalize(string text)
{
    return Regex.Replace(text, @"\s+", " ").Trim();
}

    private static string ParseAfterKeyword(string text, string keyword)
{
    Match m = Regex.Match(
        text,
        $@"{Regex.Escape(keyword)}\s*[:\-]?\s*([A-Za-z0-9\-]+)",
        RegexOptions.IgnoreCase);

    if (!m.Success)
        return "";

    string value = Regex.Replace(
        m.Groups[1].Value,
        @"[^A-Za-z0-9\-]",
        "");

    if (!Regex.IsMatch(value, @"\d"))
        return "";

    return value.ToUpper();
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