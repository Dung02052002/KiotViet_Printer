namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>
/// Một dòng đọc nguyên trạng từ file hàng hóa đầu vào (chưa đối chiếu, chưa
/// kiểm tra). Giữ cả chữ gốc của ô số lượng để báo lỗi đúng thứ người dùng thấy.
/// </summary>
public sealed class WarehouseGoodsLine
{
    /// <summary>Số dòng trong Excel (tính từ 1, đúng như Excel hiển thị).</summary>
    public required int SourceRowNumber { get; init; }

    public string ProductCode { get; init; } = "";
    public string ProductName { get; init; } = "";
    public string SourceUnit { get; init; } = "";

    /// <summary>Chữ của ô số lượng như Excel hiển thị.</summary>
    public string QuantityText { get; init; } = "";

    /// <summary>Số lượng đọc được; null = ô trống hoặc không phải số.</summary>
    public double? Quantity { get; init; }
}

/// <summary>Kết quả đọc file hàng hóa: các dòng + thông tin cột đã nhận diện.</summary>
public sealed class WarehouseGoodsFile
{
    public required string SheetName { get; init; }
    public required int HeaderRowNumber { get; init; }
    public required bool HasCodeColumn { get; init; }

    /// <summary>Tên cột số lượng đã dùng (vd "Số lượng chuyển").</summary>
    public required string QuantityColumnHeader { get; init; }

    public required List<WarehouseGoodsLine> Lines { get; init; }
}
