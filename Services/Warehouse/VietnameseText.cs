using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>
/// Chuẩn hóa chữ tiếng Việt CÓ KIỂM SOÁT, chỉ dùng để SO SÁNH (tiêu đề cột,
/// từ khóa đơn vị, mã hàng) — không bao giờ dùng để ghi đè dữ liệu gốc.
/// </summary>
public static partial class VietnameseText
{
    /// <summary>Gộp khoảng trắng liên tiếp thành một, bỏ khoảng trắng đầu/cuối.</summary>
    public static string CollapseSpaces(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "" : Whitespace().Replace(text.Trim(), " ");

    /// <summary>Chữ thường + gộp khoảng trắng, GIỮ dấu (NFC).</summary>
    public static string Lower(string? text) =>
        CollapseSpaces(text).Normalize(NormalizationForm.FormC).ToLower(CultureInfo.GetCultureInfo("vi-VN"));

    /// <summary>Chữ thường, gộp khoảng trắng, BỎ dấu ("Đơn vị tính" → "don vi tinh").</summary>
    public static string Fold(string? text)
    {
        string lower = Lower(text);
        StringBuilder sb = new(lower.Length);
        foreach (char c in lower.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(c == 'đ' ? 'd' : c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Chữ có dấu tiếng Việt nào không (ngoài bảng chữ ASCII). Xét trên dạng
    /// NFC: ở dạng tổ hợp (NFD) dấu là ký tự rời không phải chữ cái, tên có
    /// dấu sẽ bị nhầm là tên không dấu.
    /// </summary>
    public static bool HasDiacritics(string? text) =>
        !string.IsNullOrEmpty(text) && text.Normalize(NormalizationForm.FormC).Any(c => c > 127 && char.IsLetter(c));

    /// <summary>
    /// Tách thành các "từ" (chuỗi chữ/số liền nhau) — để khớp từ khóa theo
    /// ranh giới từ, tránh "dép" khớp nhầm trong một từ dài hơn.
    /// </summary>
    public static List<string> Words(string text) =>
        WordPattern().Matches(text).Select(m => m.Value).ToList();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
