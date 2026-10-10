namespace KiotVietLabelPrinter.Models.Warehouse;

public enum WarehouseRowStatus
{
    Ok,

    /// <summary>Xuất được nhưng người dùng nên xem lại (đơn vị mơ hồ, mã lặp cùng tên...).</summary>
    Warning,

    /// <summary>Không được xuất phiếu khi còn dòng ở trạng thái này.</summary>
    Error
}

/// <summary>
/// Một dòng hàng trên phiếu, sau khi đối chiếu tên + xác định đơn vị + kiểm
/// tra số lượng. Tên hàng mới / đơn vị / số lượng có thể được người dùng sửa
/// ở bảng xem trước trước khi xuất.
/// </summary>
public sealed class WarehouseProductRow
{
    public required int SourceRowNumber { get; init; }
    public string ProductCode { get; init; } = "";

    /// <summary>Tên hàng đúng như trong file hàng hóa.</summary>
    public string SourceName { get; init; } = "";

    /// <summary>Tên hàng sẽ ghi lên phiếu.</summary>
    public string TicketName { get; set; } = "";

    public string Unit { get; set; } = "";
    public double? RequestedQuantity { get; set; }
    public double? ActualQuantity { get; set; }

    public WarehouseRowStatus Status { get; set; }

    /// <summary>Lý do cảnh báo / lỗi, mỗi lý do một mục (tiếng Việt, hiển thị trực tiếp).</summary>
    public List<string> Messages { get; } = new();

    /// <summary>
    /// Lỗi/cảnh báo đến từ việc đối chiếu cả file (mã trùng...) — không tính
    /// lại được khi người dùng sửa một ô nên được giữ riêng.
    /// </summary>
    public List<(WarehouseRowStatus Level, string Message)> MatchIssues { get; } = new();
}
