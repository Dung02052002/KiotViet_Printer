namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>
/// Dấu của một file tại một thời điểm (giờ ghi + kích thước). Dùng để phát
/// hiện file phiếu bị sửa / ghi đè bên ngoài sau khi xuất hoặc sau khi xem
/// trước — khi đó không được in bản đã cũ.
/// </summary>
public sealed record WarehouseFileStamp(DateTime WriteTimeUtc, long Length)
{
    public static WarehouseFileStamp? Capture(string path)
    {
        FileInfo info = new(path);
        return info.Exists ? new WarehouseFileStamp(info.LastWriteTimeUtc, info.Length) : null;
    }

    public bool Matches(string path) => Capture(path) == this;
}

/// <summary>Yêu cầu dựng bản xem trước in cho một file phiếu đã xuất.</summary>
public sealed class WarehousePreviewRequest
{
    public required string WorkbookPath { get; init; }

    /// <summary>Cấu trúc phiếu TRÊN FILE KẾT QUẢ (đã tính số dòng hàng thực tế).</summary>
    public required WarehouseTemplateLayout Layout { get; init; }

    /// <summary>File mẫu — để so số trang của mẫu với số trang của phiếu.</summary>
    public string? TemplatePath { get; init; }

    /// <summary>Số dòng hàng trong file mẫu (để đánh giá số trang tăng có bất thường không).</summary>
    public int TemplateRowCount { get; init; }

    /// <summary>Máy in dùng để phân trang (null = máy in Excel đang dùng, tức máy mặc định).</summary>
    public string? PrinterName { get; init; }

    /// <summary>Thư mục chứa file PDF xem trước (file tạm, xoá khi đóng cửa sổ xem trước).</summary>
    public required string PdfFolder { get; init; }

    /// <summary>Font/cỡ chữ đang áp dụng (chỉ để hiển thị).</summary>
    public required string FontDescription { get; init; }
}

public enum WarehousePreviewIssueLevel
{
    /// <summary>Thông tin để người dùng biết (vd bảng kéo sang trang sau).</summary>
    Info,

    /// <summary>Có thể in ra không như mong muốn — nên xem kỹ trang được nêu.</summary>
    Warning
}

/// <summary>Một vấn đề bố cục phát hiện được; <see cref="Page"/> = trang liên quan (tính từ 1) nếu xác định được.</summary>
public sealed record WarehousePreviewIssue(WarehousePreviewIssueLevel Level, string Message, int? Page = null);

/// <summary>Một trang in: các dòng Excel (tính từ 1) nằm trên trang đó.</summary>
public sealed record WarehousePreviewPage(int Number, int FirstRow, int LastRow, string Content);

/// <summary>
/// Bản xem trước in của một file phiếu: file PDF do CHÍNH Excel xuất (cùng
/// bộ máy phân trang với lệnh in) + thông tin trang + cảnh báo bố cục.
/// </summary>
public sealed class WarehousePrintPreview
{
    public required string WorkbookPath { get; init; }

    /// <summary>Dấu file phiếu tại lúc dựng bản xem trước.</summary>
    public required WarehouseFileStamp SourceStamp { get; init; }

    public required string SheetName { get; init; }
    public required string PdfPath { get; init; }

    /// <summary>Số trang Excel tính khi in (PageSetup.Pages.Count).</summary>
    public required int PageCount { get; init; }

    /// <summary>Số trang của file mẫu với cùng máy in (null = không đo được).</summary>
    public int? TemplatePageCount { get; init; }

    /// <summary>Máy in Excel đã dùng để phân trang (tên Excel trả về).</summary>
    public required string PaginationPrinter { get; init; }

    public required WarehousePageSetup Page { get; init; }

    /// <summary>
    /// Khổ giấy THỰC TẾ của các trang Excel sẽ in (đo trên bản xem trước). Có
    /// thể khác khổ đặt trong file khi máy in không có khổ đó — Excel phân
    /// trang theo khổ của máy in.
    /// </summary>
    public required string PrintedPaper { get; init; }

    public required string FontDescription { get; init; }

    /// <summary>Các trang và dòng trên từng trang (rỗng nếu Excel không cho đọc vị trí ngắt trang).</summary>
    public required List<WarehousePreviewPage> Pages { get; init; }

    public required List<WarehousePreviewIssue> Issues { get; init; }

    public int WarningCount => Issues.Count(i => i.Level == WarehousePreviewIssueLevel.Warning);

    /// <summary>File phiếu vẫn đúng là bản đã xem trước (chưa bị sửa / ghi đè).</summary>
    public bool IsStillCurrent() => SourceStamp.Matches(WorkbookPath);
}
