using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public static class BarcodeParser
{
    public static string Parse(string text, string fallbackCode = "")
    {
        return ParseFull(text, fallbackCode).BarcodeCode;
    }

    public static BarcodeParseResult ParseFull(string text, string fallbackCode = "")
    {
        BarcodeParseResult result = new();

        if (string.IsNullOrWhiteSpace(text))
        {
            result.BarcodeCode = fallbackCode ?? "";
            result.AttributeText = "";
            return result;
        }

        string input = text.Trim();

        // =========================
        // 1) LẤY THUỘC TÍNH
        // =========================
        // Ưu tiên lấy phần trong ngoặc cuối cùng
        // ví dụ: "... mã UGQ2307 (BROWN 40 GC)" => "(BROWN 40 GC)"
        Match attrMatch = Regex.Match(input, @"(\([^()]+\))\s*$");
        if (attrMatch.Success)
        {
            result.AttributeText = attrMatch.Groups[1].Value.Trim();
        }

        // =========================
        // 2) LẤY MÃ
        // =========================

        // Trường hợp 1: có chữ "mã" / "model"
        Match codeByKeyword = Regex.Match(
            input,
            @"(?i)(?:mã|model)\s*[:\-]?\s*([A-Z0-9]+)",
            RegexOptions.IgnoreCase);

        if (codeByKeyword.Success)
        {
            result.BarcodeCode = codeByKeyword.Groups[1].Value.Trim().ToUpper();
        }

        // Trường hợp 2: fallback bằng cách tìm cụm chữ+số kiểu UGQ2307, MK285...
        if (string.IsNullOrWhiteSpace(result.BarcodeCode))
        {
            Match codePattern = Regex.Match(
                input,
                @"\b[A-Z]{1,10}\d{2,10}\b",
                RegexOptions.IgnoreCase);

            if (codePattern.Success)
                result.BarcodeCode = codePattern.Value.Trim().ToUpper();
        }

        // Trường hợp 3: fallback cuối
        if (string.IsNullOrWhiteSpace(result.BarcodeCode))
            result.BarcodeCode = fallbackCode?.Trim() ?? "";

        // Nếu chưa có thuộc tính, thử lấy phần ngoặc ở cuối ProductNameWithAttr
        if (string.IsNullOrWhiteSpace(result.AttributeText))
        {
            Match lastParen = Regex.Match(input, @"(\([^()]+\))\s*$");
            if (lastParen.Success)
                result.AttributeText = lastParen.Groups[1].Value.Trim();
        }

        return result;
    }
}