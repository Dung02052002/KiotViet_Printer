using System.Text;
using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services;

// Dùng chung cho tem kính (GlassesParser) và tem mã vạch (BarcodeParser).
//
// Thắt lưng là trường hợp đặc biệt: mã in tem được cắt THẲNG từ Tên hàng
// (giữ nguyên văn, chỉ trim đầu/cuối), không đi qua Lexer/Rule Engine và
// không bị NormalizeBaseCode viết hoa lại. Thứ tự ưu tiên bắt buộc:
//   1. "TLxx Mẫuxx"  -> "THẮT LƯNG NAM TL10 Mẫu01 - Da bò" -> "TL10 Mẫu01"
//      "TLxx - Mẫuxx" -> "THẮT LƯNG NAM TL12 - MẪU 1 (CHIẾC)" -> "TL12-MẪU 1"
//      (có gạch ngang chen giữa thì nối lại bằng "-", bỏ khoảng trắng hai
//      bên gạch; nếu không, MẪU 1/2/3/4 sẽ cùng ra "TL12" và trùng mã)
//   2. "PUTLxx"      -> "THẮT LƯNG PUTL01 - Da A"          -> "PUTL01"
//   3. "TLxx"        -> "THẮT LƯNG NAM TL10 - Da A - XIÊN" -> "TL10"
//   4. Là thắt lưng nhưng không có mã nào ở trên -> lấy TOÀN BỘ Tên hàng
//      (cột E, không kèm thuộc tính): "THẮT LƯNG NAM LOẠI XIÊN"
// CHỈ áp dụng khi Tên hàng có chữ "thắt lưng" (có dấu hoặc không dấu). Tên
// không phải thắt lưng trả về false ngay, để mọi sản phẩm khác đi đúng luồng
// parser cũ như trước, không bị quy tắc này chen vào.
public static class BeltCodeExtractor
{
    public const string RuleName = "BeltRule";

    // Không cho dính chữ/số phía trước để "PUTL01" không bị hiểu là "TL01",
    // còn tiền tố kiểu "PCN-" (ngăn bằng gạch ngang) thì vẫn bỏ qua được.
    private const string Start = @"(?<![\p{L}\d])";
    private const string End = @"(?![\p{L}\d])";

    private static readonly Regex TlMauRegex =
        new(Start + @"TL\d+\s+M[ẪẫAa]U\s*\d+" + End,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // "TL12 - MẪU 1": nhóm 1 = "TL12", nhóm 2 = "MẪU 1" (giữ nguyên văn).
    private static readonly Regex TlDashMauRegex =
        new(Start + @"(TL\d+)\s*-\s*(M[ẪẫAa]U\s*\d+)" + End,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PutlRegex =
        new(Start + @"PUTL[A-Z0-9]*" + End,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TlRegex =
        new(Start + @"TL\d+" + End,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BeltNameRegex =
        new(@"TH[ẮắĂăAa]T\s+L[ƯưUu]NG",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryExtract(
        string? productName,
        out string code,
        out string log)
    {
        return TryExtract(productName, null, out code, out log);
    }

    // productName: tên dùng để dò mã (thường là "Tên hàng (thuộc tính)",
    // cột F). fullName: Tên hàng gốc (cột E), chỉ dùng cho mức 4 — trống thì
    // lấy luôn productName.
    public static bool TryExtract(
        string? productName,
        string? fullName,
        out string code,
        out string log)
    {
        code = "";
        log = "";

        if (string.IsNullOrWhiteSpace(productName))
            return false;

        // KiotViet/Excel đôi khi trả tiếng Việt dạng tổ hợp (NFD): "Mẫu" =
        // "Ma" + dấu rời -> chuẩn về NFC để regex khớp được.
        string text = productName.Normalize(NormalizationForm.FormC).Trim();

        if (!BeltNameRegex.IsMatch(text))
            return false;

        Match match = TlMauRegex.Match(text);

        if (match.Success)
        {
            code = match.Value.Trim();
            log = $"BELT TLxx MẪUxx -> {code}";
            return true;
        }

        match = TlDashMauRegex.Match(text);

        if (match.Success)
        {
            code = $"{match.Groups[1].Value}-{match.Groups[2].Value}";
            log = $"BELT TLxx - MẪUxx -> {code}";
            return true;
        }

        match = PutlRegex.Match(text);

        if (match.Success)
        {
            code = match.Value.Trim();
            log = $"BELT PUTLxx -> {code}";
            return true;
        }

        match = TlRegex.Match(text);

        if (match.Success)
        {
            code = match.Value.Trim();
            log = $"BELT TLxx -> {code}";
            return true;
        }

        code = !string.IsNullOrWhiteSpace(fullName)
            ? fullName.Normalize(NormalizationForm.FormC).Trim()
            : text;
        log = $"BELT không có mã, lấy toàn bộ tên -> {code}";
        return true;
    }
}
