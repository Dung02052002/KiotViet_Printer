namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>Nhóm lỗi của chức năng Phiếu xuất kho — để màn hình chọn tiêu đề/hướng dẫn phù hợp.</summary>
public enum WarehouseErrorKind
{
    FileNotFound,
    WorkbookUnreadable,
    TemplateStructure,
    GoodsStructure,
    InvalidTicketCode,
    InvalidData,
    OutputNotWritable,
    OutputExists,
    OutputLocked,
    SaveFailed,
    VerificationFailed,
    PrinterNotFound,
    PrintFailed,
    ExcelNotAvailable,
    ExcelTimeout,
    PreviewFailed,

    /// <summary>File phiếu đã bị sửa / ghi đè sau khi xuất hoặc xem trước — phải xuất / xem lại.</summary>
    OutputChanged
}

/// <summary>
/// Lỗi nghiệp vụ có thông báo tiếng Việt hiển thị thẳng cho người dùng
/// (nêu nguyên nhân + cách khắc phục). Chi tiết kỹ thuật nằm ở InnerException
/// để ghi log, không đưa ra màn hình.
/// </summary>
public sealed class WarehouseException : Exception
{
    public WarehouseErrorKind Kind { get; }

    public WarehouseException(WarehouseErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }
}
