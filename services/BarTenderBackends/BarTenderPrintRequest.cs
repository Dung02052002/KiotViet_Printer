namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Mô tả một lệnh in gửi cho BarTender — độc lập với cơ chế thực thi
/// (command line chuẩn hay XMLScript). Print service dựng object này rồi
/// giao cho backend phù hợp (xem <see cref="IBarTenderPrintBackend"/>).
/// </summary>
public sealed class BarTenderPrintRequest
{
    public required string BarTenderExe { get; init; }

    public required string TemplatePath { get; init; }

    public required string PrinterName { get; init; }

    /// <summary>
    /// Dữ liệu động cần bơm thẳng vào template qua Named Sub-String. CHỈ có
    /// khi in tem kính (tiêu đề / block thông tin đã format sẵn). Khi khác
    /// null &amp; có phần tử → lệnh in BẮT BUỘC phải dùng XMLScript (Enterprise
    /// Automation). Khi rỗng/null → dữ liệu đã nằm trong file data mà template
    /// .btw liên kết, in được bằng command line chuẩn trên MỌI edition.
    /// </summary>
    public IReadOnlyDictionary<string, string>? NamedSubStrings { get; init; }

    public bool RequiresNamedSubStrings =>
        NamedSubStrings is { Count: > 0 };

    public string DescribeNamedSubStrings()
    {
        if (!RequiresNamedSubStrings)
            return "(không có)";

        return string.Join(
            ", ",
            NamedSubStrings!.Select(kv =>
            {
                string v = kv.Value ?? string.Empty;
                if (v.Length > 40)
                    v = v.Substring(0, 40) + "…";
                v = v.Replace("\r", " ").Replace("\n", " ");
                return $"{kv.Key}=\"{v}\"";
            }));
    }
}
