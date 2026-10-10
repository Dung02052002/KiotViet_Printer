namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>Mọi thứ cần để ghi một phiếu xuất kho từ file mẫu.</summary>
public sealed class WarehouseExportRequest
{
    public required string TemplatePath { get; init; }
    public required string OutputPath { get; init; }

    /// <summary>Mã phiếu hoàn chỉnh, đã chuẩn hóa (vd "XK23609").</summary>
    public required string TicketCode { get; init; }

    /// <summary>
    /// Ngày lập phiếu ghi lên phiếu (chỉ dùng phần ngày). Bắt buộc truyền từ
    /// màn hình: luồng xuất không tự lấy "hôm nay" lần nữa, để ngày người dùng
    /// đã chọn không bị thay giữa chừng (vd xuất lúc qua nửa đêm).
    /// </summary>
    public required DateTime DocumentDate { get; init; }

    public required IReadOnlyList<WarehouseProductRow> Rows { get; init; }
    public WarehouseFontSettings Font { get; init; } = WarehouseFontSettings.KeepTemplate();

    /// <summary>Thiết lập trang in người dùng đã đổi (mặc định: giữ nguyên như mẫu).</summary>
    public WarehousePageSettings Page { get; init; } = WarehousePageSettings.KeepTemplate();

    /// <summary>true = người dùng đã xác nhận ghi đè file kết quả đang tồn tại.</summary>
    public bool OverwriteExisting { get; init; }
}
