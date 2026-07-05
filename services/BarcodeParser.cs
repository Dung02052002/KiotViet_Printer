using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services;

public static class BarcodeParser
{
    public static string Parse(string text, string fallbackProductCode = "")
    {
        if (string.IsNullOrWhiteSpace(text))
            return fallbackProductCode;

        string input = text.Trim();

        // 1) Model XXXXX
        string? model = MatchAfterKeyword(input, @"\bModel\b");
        if (!string.IsNullOrWhiteSpace(model))
            return CleanCode(model);

        // 2) Mã XXXXX
        string? ma = MatchAfterKeyword(input, @"\bMã\b");
        if (!string.IsNullOrWhiteSpace(ma))
            return CleanCode(ma);

        // 3) Nam XXXXX
        string? nam = MatchAfterKeyword(input, @"\bNam\b");
        if (!string.IsNullOrWhiteSpace(nam))
            return CleanCode(nam);

        // 4) Nữ XXXXX
        string? nu = MatchAfterKeyword(input, @"\bNữ\b");
        if (!string.IsNullOrWhiteSpace(nu))
            return CleanCode(nu);

        // 5) Pucini XXXXX
        string? pucini = MatchAfterKeyword(input, @"\bPucini\b");
        if (!string.IsNullOrWhiteSpace(pucini))
            return CleanCode(pucini);

        return fallbackProductCode;
    }

    private static string? MatchAfterKeyword(string input, string keywordPattern)
    {
        // Lấy phần sau keyword cho tới dấu phẩy / ngoặc / xuống dòng
        var match = Regex.Match(
            input,
            $"{keywordPattern}\\s*[:\\-]?\\s*([^,\\r\\n\\(\\)]*)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
            return null;

        string value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string CleanCode(string value)
    {
        string result = value.Trim();

        // Cắt ở dấu phẩy nếu có
        int commaIndex = result.IndexOf(',');
        if (commaIndex >= 0)
            result = result[..commaIndex].Trim();

        // Cắt ở dấu ngoặc nếu có
        int bracketIndex = result.IndexOf('(');
        if (bracketIndex >= 0)
            result = result[..bracketIndex].Trim();

        // Lấy token đầu tiên nếu có nhiều khoảng trắng
        string[] parts = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
            return parts[0].Trim();

        return result;
    }
}