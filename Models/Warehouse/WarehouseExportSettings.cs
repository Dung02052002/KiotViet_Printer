namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>
/// Thiết lập được nhớ giữa các lần mở của riêng chức năng Phiếu xuất kho.
/// Lưu ở file riêng (Config/warehouse-export.json), KHÔNG nằm trong
/// AppConfig — máy in / font ở đây không bao giờ đụng tới cấu hình in tem.
/// </summary>
public sealed class WarehouseExportSettings
{
    public string TicketPrefix { get; set; } = "XK";
    public string PrinterName { get; set; } = "";
    public string TemplatePath { get; set; } = "";
    public string LastGoodsFolder { get; set; } = "";
    public string OutputFolder { get; set; } = "";

    public WarehouseFontApplyScope FontScope { get; set; } = WarehouseFontApplyScope.KeepTemplate;
    public string FontFamily { get; set; } = "";
    public double FontSize { get; set; }
}
