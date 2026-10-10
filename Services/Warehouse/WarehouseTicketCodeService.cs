using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>Kết quả chuẩn hóa mã phiếu: <see cref="Code"/> chỉ có giá trị khi hợp lệ.</summary>
public sealed record TicketCodeResult(string? Code, string? Error)
{
    public bool IsValid => Code != null;

    public static TicketCodeResult Success(string code) => new(code, null);

    public static TicketCodeResult Failure(string error) => new(null, error);
}

/// <summary>
/// Chuẩn hóa mã phiếu người dùng nhập thành mã hoàn chỉnh:
/// <list type="bullet">
///   <item>Một mã: "23609" → "XK23609".</item>
///   <item>Nhiều mã (một phiếu gộp nhiều phiếu chuyển), ngăn bằng dấu phẩy:
///         "23609, 23610" → "XK23609,XK23610" — MỖI mã đều được ghép tiền tố.</item>
/// </list>
/// Mã hoàn chỉnh còn được dùng làm tên file kết quả nên mỗi mã chỉ gồm chữ
/// không dấu, số, '-', '_' và '.' (phiếu con: "23609.01" → "XK23609.01");
/// dấu phẩy chỉ đóng vai trò ngăn cách. Không bao giờ tự sinh mã.
/// </summary>
public static partial class WarehouseTicketCodeService
{
    public const string DefaultPrefix = "XK";

    /// <summary>Ký tự ngăn cách khi nhập nhiều mã.</summary>
    public const char Separator = ',';

    /// <summary>Độ dài tối đa của mã hoàn chỉnh (kể cả tiền tố và dấu phẩy) — giữ tên file kết quả ở mức hợp lý.</summary>
    public const int MaxLength = 100;

    public static TicketCodeResult Normalize(string? input, string? prefix = DefaultPrefix)
    {
        string text = (input ?? "").Trim();
        string pre = (prefix ?? "").Trim();

        if (text.Length == 0)
            return TicketCodeResult.Failure("Chưa nhập mã phiếu.");

        if (pre.Length > 0 && !AllowedChars().IsMatch(pre))
            return TicketCodeResult.Failure($"Tiền tố \"{pre}\" chỉ được gồm chữ cái không dấu, số, '-', '_' hoặc '.'.");

        // Khoảng trắng quanh dấu phẩy ("23609, 23610") là cách gõ tự nhiên nên
        // được bỏ qua; khoảng trắng nằm TRONG một mã vẫn bị coi là sai.
        string[] parts = text.Split(Separator, StringSplitOptions.TrimEntries);
        if (parts.Any(p => p.Length == 0))
            return TicketCodeResult.Failure($"Mã phiếu \"{text}\" có dấu phẩy thừa (đầu, cuối hoặc hai dấu phẩy liền nhau).");

        List<string> codes = new(parts.Length);
        foreach (string part in parts)
        {
            TicketCodeResult single = NormalizeSingle(part, pre);
            if (!single.IsValid)
                return single;

            // So sánh không phân biệt hoa thường: "XK1" và "xk1" là cùng một phiếu.
            if (codes.Contains(single.Code!, StringComparer.OrdinalIgnoreCase))
                return TicketCodeResult.Failure($"Mã phiếu \"{single.Code}\" bị nhập lặp lại.");

            codes.Add(single.Code!);
        }

        string code = string.Join(Separator, codes);
        if (code.Length > MaxLength)
            return TicketCodeResult.Failure($"Mã phiếu dài quá {MaxLength} ký tự.");

        return TicketCodeResult.Success(code);
    }

    /// <summary>Chuẩn hóa MỘT mã (không chứa dấu phẩy, đã cắt khoảng trắng hai đầu).</summary>
    private static TicketCodeResult NormalizeSingle(string text, string prefix)
    {
        if (!AllowedChars().IsMatch(text))
            return TicketCodeResult.Failure(
                $"Mã phiếu \"{text}\" không hợp lệ. Chỉ dùng chữ cái không dấu, số, '-', '_' hoặc '.' " +
                $"(không có khoảng trắng, dấu chấm không đứng đầu/cuối hay liền nhau); nhiều mã thì ngăn cách bằng dấu phẩy.");

        // Người dùng gõ sẵn cả tiền tố ("XK23609" / "xk23609"): không ghép
        // thêm lần nữa, chỉ viết tiền tố đúng như cấu hình.
        string body = prefix.Length > 0 && text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? text[prefix.Length..]
            : text;

        if (body.Length == 0)
            return TicketCodeResult.Failure($"Mã phiếu chỉ có tiền tố \"{prefix}\", thiếu phần số phiếu.");

        return TicketCodeResult.Success(prefix + body);
    }

    // Các nhóm chữ/số/'-'/'_' nối nhau bằng MỘT dấu chấm: "23609.01" hợp lệ,
    // ".1", "1." và "1..2" thì không — dấu chấm cuối tên file bị Windows tự cắt,
    // còn ".." dễ nhầm với thư mục cha.
    [GeneratedRegex(@"^[A-Za-z0-9_\-]+(?:\.[A-Za-z0-9_\-]+)*$")]
    private static partial Regex AllowedChars();
}
