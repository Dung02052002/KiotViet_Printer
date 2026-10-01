using System.Text;
using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services;

// Dùng chung cho tem kính (GlassesParser) và tem mã vạch (BarcodeParser).
//
// Thắt lưng là trường hợp đặc biệt: mã in tem được cắt THẲNG từ Tên hàng
// (giữ nguyên văn, chỉ trim đầu/cuối), không đi qua Lexer/Rule Engine và
// không bị NormalizeBaseCode viết hoa lại. Thứ tự ưu tiên bắt buộc:
//   1. "TLxx Mẫuxx"  -> "THẮT LƯNG NAM TL10 Mẫu01 - Da bò" -> "TL10 Mẫu01"
//   2. "PUTLxx"      -> "THẮT LƯNG PUTL01 - Da A"          -> "PUTL01"
//   3. "TLxx"        -> "THẮT LƯNG NAM TL10 - Da A - XIÊN" -> "TL10"
//   4. Là thắt lưng nhưng không có mã nào ở trên            -> "Thắt lưng"
// CHỈ áp dụng khi Tên hàng có chữ "thắt lưng" (có dấu hoặc không dấu). Tên
// không phải thắt lưng trả về false ngay, để mọi sản phẩm khác đi đúng luồng
// parser cũ như trước, không bị quy tắc này chen vào.
public static class BeltCodeExtractor
{
    public const string RuleName = "BeltRule";

    public const string NoCodeText = "Thắt lưng";

    // Không cho dính chữ/số phía trước để "PUTL01" không bị hiểu là "TL01",
    // còn tiền tố kiểu "PCN-" (ngăn bằng gạch ngang) thì vẫn bỏ qua được.
    private const string Start = @"(?<![\p{L}\d])";
    private const string End = @"(?![\p{L}\d])";

    private static readonly Regex TlMauRegex =
        new(Start + @"TL\d+\s+M[ẪẫAa]U\s*\d+" + End,
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

        code = NoCodeText;
        log = $"BELT không có mã -> {code}";
        return true;
    }
}
