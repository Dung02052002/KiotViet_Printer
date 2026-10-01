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

    // Regex tĩnh dựng sẵn 1 lần (thay vì Regex.Match(text, pattern) mỗi dòng sản
    // phẩm) — .NET chỉ cache 15 pattern gần nhất/tiến trình cho overload tĩnh, số
    // pattern dùng trong class này đã xấp xỉ giới hạn đó nên rất dễ bị đẩy khỏi
    // cache khi chạy xen với các Regex khác trong app, gây biên dịch lại liên tục.
    private static readonly Regex[] KeywordRegexes = BuildKeywordRegexes();
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex NonCodeCharsRegex = new(@"[^A-Za-z0-9\-]", RegexOptions.Compiled);
    private static readonly Regex HasDigitRegex = new(@"\d", RegexOptions.Compiled);
    private static readonly Regex TokenCandidateRegex =
        new(@"\b[A-Za-z]{1,10}[A-Za-z0-9\-]{2,30}\b", RegexOptions.Compiled);
    private static readonly Regex StartsWithDigitRegex = new(@"^\d", RegexOptions.Compiled);
    private static readonly Regex TrailingParenRegex = new(@"(\([^()]+\))\s*$", RegexOptions.Compiled);

    private static Regex[] BuildKeywordRegexes()
    {
        Regex[] regexes = new Regex[Keywords.Length];
        for (int i = 0; i < Keywords.Length; i++)
        {
            // Từ khoá phải đứng thành từ riêng: không có chữ/số dính phía trước
            // và không có chữ dính phía sau (vẫn cho số dính sau, VD "model6282").
            // Thiếu ranh giới này "nam" khớp cả trong "Vietnam"/"Dynamo"/
            // "Panama" → parse ra mã sai (VD "Dynamo D500" → "OD500").
            regexes[i] = new Regex(
                $@"(?<![\p{{L}}\p{{M}}\p{{N}}]){Regex.Escape(Keywords[i])}(?![\p{{L}}\p{{M}}])\s*[:\-]?\s*([A-Za-z0-9\-]+)(?:\s+([A-Za-z0-9\-]+))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
        return regexes;
    }

    // productName: Tên hàng gốc (cột E), chỉ dùng cho thắt lưng không có mã
    // (lấy toàn bộ tên làm mã in tem, xem BeltCodeExtractor).
    public static string Parse(string text, string fallbackCode = "", string productName = "")
    {
        return ParseFull(text, fallbackCode, productName).BarcodeCode;
    }

    public static BarcodeParseResult ParseFull(string text, string fallbackCode = "", string productName = "")
    {
        BarcodeParseResult result = new();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.BarcodeCode = fallbackCode?.Trim() ?? "";
            result.AttributeText = "";
            return result;
        }

        // Thắt lưng: cắt thẳng mã từ Tên hàng, giữ nguyên văn (xem
        // BeltCodeExtractor) — không để keyword "nam"/"nữ" bên dưới nhặt
        // nhầm "THẮT LƯNG NAM TL14 MẪU 1" thành "TL14".
        if (BeltCodeExtractor.TryExtract(text, productName, out string beltCode, out _))
        {
            result.BarcodeCode = beltCode;
            result.AttributeText = ExtractAttributeFallback(text);
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
    for (int i = 0; i < Keywords.Length; i++)
    {
        string value = ParseAfterKeyword(text, KeywordRegexes[i]);

        if (!string.IsNullOrWhiteSpace(value))
            return value;
    }

    // Nếu không có keyword thì tìm token giống mã hàng
    MatchCollection matches = TokenCandidateRegex.Matches(text);

    foreach (Match match in matches)
    {
        string value = match.Value.ToUpper();

        // phải có ít nhất 1 số
        if (!HasDigitRegex.IsMatch(value))
            continue;

        // bỏ token chỉ là kích thước
        if (StartsWithDigitRegex.IsMatch(value))
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
    return WhitespaceRegex.Replace(text, " ").Trim();
}

    private static string ParseAfterKeyword(string text, Regex keywordRegex)
{
    Match m = keywordRegex.Match(text);

    if (!m.Success)
        return "";

    string first = NonCodeCharsRegex.Replace(m.Groups[1].Value, "");

    if (HasDigitRegex.IsMatch(first))
        return first.ToUpper();

    // Mã bị tách làm 2 phần bởi khoảng trắng (VD "model BD 6337"),
    // phần đầu không có số nên ghép thêm từ kế tiếp nếu từ đó có số.
    if (first.Length is >= 1 and <= 5 && m.Groups[2].Success)
    {
        string second = NonCodeCharsRegex.Replace(m.Groups[2].Value, "");

        if (HasDigitRegex.IsMatch(second))
            return (first + second).ToUpper();
    }

    return "";
}

    private static string ExtractAttributeFallback(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        // Ưu tiên ngoặc cuối
        Match m = TrailingParenRegex.Match(text);
        if (m.Success)
            return m.Groups[1].Value.Trim();

        return "";
    }
}