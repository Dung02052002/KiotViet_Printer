using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public static class BarcodeParser
{
    private static readonly HashSet<string> InvalidCodeTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "model",
        "mã",
        "mã sp",
        "MA",
        "MA SP",
        "code",
        "ms",
        "NỮ",
        "NU",
        "NAM",
        "PUCINI",
        "CAO CẤP",
        "CAO CAP",
        "KIM LOẠI",
        "KIM LOAI"
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

        string input = NormalizeInput(text);

        // =========================
        // 1) LẤY THUỘC TÍNH
        // =========================
        // ưu tiên ngoặc cuối cùng
        Match attrMatch = Regex.Match(input, @"(\([^()]+\))\s*$");
        if (attrMatch.Success)
        {
            result.AttributeText = attrMatch.Groups[1].Value.Trim();
        }

        // =========================
        // 2) LẤY MÃ TỪ KEYWORD
        // =========================
        // hỗ trợ: mã, mã sp, model, code, ms
        // ví dụ:
        // "mã UGQ2307"
        // "model: MK285"
        // "mã sp - ABC123"
        var keywordMatches = Regex.Matches(
            input,
            @"(?ix)
            \b(?:mã\s*sp|ma\s*sp|mã|ma|model|code|ms)\b
            \s*[:\-]?\s*
            ([A-Z0-9\-_\.]{3,30})
            ",
            RegexOptions.IgnoreCase);

        foreach (Match m in keywordMatches)
        {
            if (!m.Success) continue;

            string candidate = CleanupCandidate(m.Groups[1].Value);

            if (IsValidBarcodeCode(candidate))
            {
                result.BarcodeCode = candidate;
                break;
            }
        }

        // =========================
        // 3) FALLBACK: tìm token dạng mã hàng trong toàn chuỗi
        // =========================
        if (string.IsNullOrWhiteSpace(result.BarcodeCode))
        {
            // ưu tiên token có cả chữ + số
            var tokenMatches = Regex.Matches(
                input,
                @"\b[A-Z0-9][A-Z0-9\-_\.]{3,29}\b",
                RegexOptions.IgnoreCase);

            foreach (Match m in tokenMatches)
            {
                string candidate = CleanupCandidate(m.Value);

                if (IsValidBarcodeCode(candidate))
                {
                    result.BarcodeCode = candidate;
                    break;
                }
            }
        }

        // =========================
        // 4) FALLBACK CUỐI
        // =========================
        if (string.IsNullOrWhiteSpace(result.BarcodeCode))
        {
            string fb = CleanupCandidate(fallbackCode);
            result.BarcodeCode = IsValidBarcodeCode(fb) ? fb : (fallbackCode?.Trim() ?? "");
        }

        // =========================
        // 5) Làm sạch thuộc tính
        // =========================
        result.AttributeText = CleanupAttribute(result.AttributeText);

        return result;
    }

    private static string NormalizeInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string s = text
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");

        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    private static string CleanupCandidate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string s = value.Trim().ToUpper();

        // bỏ ký tự rác đầu/cuối
        s = s.Trim(':', '-', '.', ',', ';', '_', ' ');

        return s;
    }

    private static string CleanupAttribute(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string s = value.Trim();

        // nếu muốn bỏ ngoặc thì uncomment:
        // s = s.Trim('(', ')');

        return s;
    }

    private static bool IsValidBarcodeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        code = code.Trim().ToUpper();

        if (code.Length < 4 || code.Length > 30)
            return false;

        if (InvalidCodeTokens.Contains(code))
            return false;

        // loại các text chỉ toàn số
        if (Regex.IsMatch(code, @"^\d+$"))
            return false;

        // bắt buộc có ít nhất 1 chữ và 1 số
        bool hasLetter = Regex.IsMatch(code, @"[A-Z]");
        bool hasDigit = Regex.IsMatch(code, @"\d");

        if (!hasLetter || !hasDigit)
            return false;

        // loại một số cụm mô tả thường gặp
        if (Regex.IsMatch(code, @"^(NAM|NU|NỮ|MODEL|CODE|MS)$", RegexOptions.IgnoreCase))
            return false;

        return true;
    }
}