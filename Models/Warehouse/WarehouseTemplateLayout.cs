namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>
/// Cấu trúc của file phiếu xuất kho mẫu, xác định bằng cách ĐỌC file mẫu
/// (tìm theo nội dung tiêu đề), không dùng địa chỉ ô cố định: các mẫu thực tế
/// (Kho HCM, các cửa hàng) khác nhau ở số dòng hàng mẫu, style và chiều cao
/// dòng, và người dùng có thể chỉnh mẫu (thêm dòng tiêu đề...) mà chức năng
/// vẫn tìm đúng ô "Số :" và bảng hàng.
/// Mọi chỉ số dòng/cột đều tính từ 0 (giống NPOI); hiển thị cho người dùng
/// thì cộng 1.
/// </summary>
public sealed class WarehouseTemplateLayout
{
    public required int SheetIndex { get; init; }
    public required string SheetName { get; init; }

    /// <summary>Ô chứa số phiếu, ví dụ "Số : XK023056".</summary>
    public required int TicketCodeRow { get; init; }
    public required int TicketCodeColumn { get; init; }

    /// <summary>Phần chữ đứng trước mã phiếu trong ô số phiếu ("Số : ") — giữ nguyên khi ghi.</summary>
    public required string TicketCodeLabel { get; init; }

    /// <summary>Mã phiếu đang có trong file mẫu (chỉ để hiển thị).</summary>
    public required string TemplateTicketCode { get; init; }

    public required int HeaderFirstRow { get; init; }
    public required int HeaderLastRow { get; init; }

    public required int NameColumn { get; init; }
    public required int UnitColumn { get; init; }
    public required int RequestedColumn { get; init; }
    public required int ActualColumn { get; init; }

    /// <summary>Dòng hàng hóa đầu tiên (ngay dưới tiêu đề bảng).</summary>
    public required int DataFirstRow { get; init; }

    /// <summary>Số dòng hàng hóa đang có trong file mẫu (ít nhất 1 — dòng này cung cấp định dạng).</summary>
    public required int DataRowCount { get; init; }

    public int DataLastRow => DataFirstRow + DataRowCount - 1;

    public int TableFirstColumn => Math.Min(Math.Min(NameColumn, UnitColumn), Math.Min(RequestedColumn, ActualColumn));
    public int TableLastColumn => Math.Max(Math.Max(NameColumn, UnitColumn), Math.Max(RequestedColumn, ActualColumn));

    /// <summary>Font của ô tên hàng ở dòng hàng đầu tiên — làm giá trị mặc định cho phần chọn font.</summary>
    public required string NameFontName { get; init; }
    public required double NameFontSize { get; init; }

    /// <summary>Vùng in của sheet trong file mẫu (null = không đặt vùng in).</summary>
    public string? PrintArea { get; init; }

    /// <summary>Thiết lập trang in của sheet phiếu: khổ giấy, hướng, lề, tỷ lệ...</summary>
    public required WarehousePageSetup Page { get; init; }

    /// <summary>".xlsx" hoặc ".xls" — file kết quả giữ đúng định dạng của file mẫu.</summary>
    public required string FileExtension { get; init; }
}
