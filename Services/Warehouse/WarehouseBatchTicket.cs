using KiotVietLabelPrinter.Models.Warehouse;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>
/// Một phiếu trong file "Danh sách chi tiết chuyển hàng" có NHIỀU phiếu
/// chuyển: các dòng cùng mã chuyển hàng, đối chiếu riêng như một file hàng
/// hóa độc lập, xuất ra một file phiếu riêng và có trạng thái xuất / xem
/// trước riêng.
/// </summary>
public sealed class WarehouseBatchTicket
{
    /// <summary>Mã phiếu chuyển đúng như trong file (vd "TRF023522"); rỗng = các dòng thiếu mã.</summary>
    public required string TransferCode { get; init; }

    /// <summary>Ghi chú chuyển của phiếu (vd "BÌNH TÂN THÙNG 1") — chỉ để hiển thị.</summary>
    public string Note { get; init; } = "";

    /// <summary>Chi nhánh nhận — chỉ để hiển thị.</summary>
    public string Destination { get; init; } = "";

    /// <summary>Phiếu đang soạn: chỉ gồm các dòng của phiếu chuyển này.</summary>
    public required WarehouseDocument Document { get; init; }

    /// <summary>Tình trạng file đã xuất của riêng phiếu này.</summary>
    public WarehouseExportTracker Export { get; } = new();

    /// <summary>Mã phiếu xuất theo tiền tố đang chọn ("TRF023522" + "XK" → "XK023522").</summary>
    public TicketCodeResult TicketCode(string? prefix) => WarehouseTicketCodeService.FromTransferCode(TransferCode, prefix);

    /// <summary>Tổng số lượng thực xuất của các dòng đọc được số.</summary>
    public double TotalQuantity => Document.Rows.Sum(r => r.ActualQuantity ?? 0);
}
