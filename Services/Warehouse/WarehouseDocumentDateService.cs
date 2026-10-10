using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>
/// Ngày lập phiếu trên file phiếu mẫu. Trong mọi mẫu thực tế ngày là MỘT Ô
/// CHỮ dạng "Ngày  22  tháng   9     năm 2026" (không phải ô kiểu ngày của
/// Excel): các khoảng trắng giữa chữ và số là chỗ căn cho đẹp trên bản in. Vì
/// vậy chỉ thay ba con số ngày / tháng / năm, giữ nguyên từng ký tự còn lại
/// (chữ "Ngày", "tháng", "năm", khoảng trắng, phần chữ đứng trước như "Hà
/// Nội, ngày") — style của ô không bị đụng tới.
/// </summary>
public static partial class WarehouseDocumentDateService
{
    /// <summary>Định dạng hiển thị ngày trên màn hình (kiểu Việt Nam).</summary>
    public const string DisplayFormat = "dd/MM/yyyy";

    // Năm luôn 4 chữ số trên phiếu; giới hạn này chặn nhầm năm (vd chọn 1900)
    // mà vẫn đủ rộng cho phiếu lập bù / lập trước.
    public static readonly DateTime MinDate = new(2000, 1, 1);
    public static readonly DateTime MaxDate = new(2099, 12, 31);

    /// <summary>Ô chữ có đúng dạng "Ngày … tháng … năm …" để ghi ngày lập phiếu không.</summary>
    public static bool IsDateText(string? text) => Match(text) != null;

    /// <summary>
    /// Ngày đang ghi trong ô mẫu, null nếu ô để trống chỗ điền ("Ngày … tháng
    /// … năm …") hoặc không phải ngày có thật.
    /// </summary>
    public static DateTime? TryRead(string? text)
    {
        Match? m = Match(text);
        if (m == null ||
            !int.TryParse(m.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int day) ||
            !int.TryParse(m.Groups["month"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int month) ||
            !int.TryParse(m.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int year) ||
            year < 1 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
            return null;

        return new DateTime(year, month, day);
    }

    /// <summary>
    /// Chữ mới cho ô ngày: giống hệt chữ của ô mẫu, chỉ thay ngày / tháng /
    /// năm bằng <paramref name="date"/> (bỏ phần giờ). Ngày, tháng viết không
    /// có số 0 đứng đầu như các mẫu thật ("Ngày 9 tháng 9"), trừ khi chính mẫu
    /// viết có số 0 ("Ngày 09").
    /// </summary>
    /// <exception cref="WarehouseException">Ô mẫu không có dạng "Ngày … tháng … năm …".</exception>
    public static string Compose(string templateText, DateTime date)
    {
        Match m = Match(templateText)
                  ?? throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                      $"Ô ngày lập phiếu \"{templateText}\" không có dạng \"Ngày … tháng … năm …\".");

        return new StringBuilder()
            .Append(WithTrailingSpace(m.Groups["lead"].Value))
            .Append(Number(date.Day, m.Groups["day"].Value))
            .Append(Spaced(m.Groups["month_label"].Value))
            .Append(Number(date.Month, m.Groups["month"].Value))
            .Append(Spaced(m.Groups["year_label"].Value))
            .Append(date.Year.ToString("0000", CultureInfo.InvariantCulture))
            .Append(m.Groups["tail"].Value)
            .ToString();
    }

    /// <summary>Lỗi của ngày lập phiếu (null = hợp lệ).</summary>
    public static string? Validate(DateTime date)
    {
        if (date.Date < MinDate || date.Date > MaxDate)
            return $"Ngày lập phiếu {date:dd/MM/yyyy} không hợp lệ — chọn ngày từ {MinDate:dd/MM/yyyy} đến {MaxDate:dd/MM/yyyy}.";
        return null;
    }

    private static Match? Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Chữ tiếng Việt có thể lưu ở dạng tổ hợp (NFD) — chuẩn hoá để "ngày"
        // gõ kiểu nào cũng nhận ra.
        Match m = DatePattern().Match(text.Normalize(NormalizationForm.FormC));
        return m.Success ? m : null;
    }

    private static string Number(int value, string templateToken) =>
        templateToken.Length == 2 && templateToken[0] == '0'
            ? value.ToString("00", CultureInfo.InvariantCulture)
            : value.ToString(CultureInfo.InvariantCulture);

    // Mẫu để trống chỗ điền mà không có khoảng trắng ("Ngày…tháng") thì vẫn
    // tách số khỏi chữ một khoảng.
    private static string WithTrailingSpace(string text) => text.Length > 0 && !char.IsWhiteSpace(text[^1]) ? text + " " : text;

    private static string Spaced(string label)
    {
        string text = WithTrailingSpace(label);
        return char.IsWhiteSpace(text[0]) ? text : " " + text;
    }

    // lead: phần chữ tới hết "Ngày" + khoảng trắng (cho phép chữ đứng trước, vd "Hà Nội, ngày").
    // Mỗi số có thể là chữ số hoặc chỗ trống để điền ("...", "…", "___").
    [GeneratedRegex(
        @"^(?<lead>(?:.*?[\s,])?ngày\s*)(?<day>\d{1,2}|[.…_]*)(?<month_label>\s*tháng\s*)(?<month>\d{1,2}|[.…_]*)" +
        @"(?<year_label>\s*năm\s*)(?<year>\d{4}|[.…_]*)(?<tail>\s*)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex DatePattern();
}
