namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>
/// Kết quả xuất phiếu. Chỉ được tạo sau khi file đã được MỞ LẠI và kiểm tra
/// — <see cref="VerifiedChecks"/> liệt kê đúng những gì đã kiểm tra được.
/// </summary>
public sealed class WarehouseExportResult
{
    public required string OutputPath { get; init; }
    public required int RowCount { get; init; }

    /// <summary>Số dòng hàng đã thêm (dương) / bớt (âm) so với file mẫu.</summary>
    public required int RowDelta { get; init; }

    public required List<string> VerifiedChecks { get; init; }

    /// <summary>Tên sheet phiếu trong file kết quả (sheet được xem trước và in).</summary>
    public required string SheetName { get; init; }

    /// <summary>Cấu trúc phiếu dò lại trên chính file kết quả (đúng số dòng hàng đã ghi).</summary>
    public required WarehouseTemplateLayout Layout { get; init; }

    /// <summary>Dấu file ngay sau khi xuất — file bị sửa bên ngoài sau đó thì không in bản này.</summary>
    public required WarehouseFileStamp Stamp { get; init; }
}
