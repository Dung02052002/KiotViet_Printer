using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>Kết quả chuẩn hóa mã phiếu: <see cref="Code"/> chỉ có giá trị khi hợp lệ.</summary>
public sealed record TicketCodeResult(string? Code, string? Error)
{
    public bool IsValid => Code != null;
}

/// <summary>
/// Ghép tiền tố vào mã phiếu người dùng nhập: "23609" → "XK23609". Mã phiếu
/// còn được dùng làm tên file kết quả nên chỉ chấp nhận chữ, số, '-' và '_'.
/// Không bao giờ tự sinh mã.
/// </summary>
public static partial class WarehouseTicketCodeService
{
    public const string DefaultPrefix = "XK";
    public const int MaxLength = 40;

    public static TicketCodeResult Normalize(string? input, string? prefix = DefaultPrefix)
    {
        string text = (input ?? "").Trim();
        string pre = (prefix ?? "").Trim();

        if (text.Length == 0)
            return new(null, "Chưa nhập mã phiếu.");

        if (pre.Length > 0 && !AllowedChars().IsMatch(pre))
            return new(null, $"Tiền tố \"{pre}\" chỉ được gồm chữ cái không dấu, số, '-' hoặc '_'.");

        if (!AllowedChars().IsMatch(text))
            return new(null, $"Mã phiếu \"{text}\" có ký tự không hợp lệ. Chỉ dùng chữ cái không dấu, số, '-' hoặc '_' (không có khoảng trắng).");

        // Người dùng gõ sẵn cả tiền tố ("XK23609" / "xk23609"): không ghép
        // thêm lần nữa, chỉ viết tiền tố đúng như cấu hình.
        string body = pre.Length > 0 && text.StartsWith(pre, StringComparison.OrdinalIgnoreCase)
            ? text[pre.Length..]
            : text;

        if (body.Length == 0)
            return new(null, $"Mã phiếu chỉ có tiền tố \"{pre}\", thiếu phần số phiếu.");

        string code = pre + body;
        if (code.Length > MaxLength)
            return new(null, $"Mã phiếu dài quá {MaxLength} ký tự.");

        return new(code, null);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_\-]+$")]
    private static partial Regex AllowedChars();
}
