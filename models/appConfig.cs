namespace KiotVietLabelPrinter.Models;

public class AppConfig
{
    public string BarTenderExe { get; set; } = "";
    public string PrinterName { get; set; } = "";

    // Cache kết quả phát hiện khả năng dùng /XMLScript= (Enterprise
    // Automation) — xem BarTenderCapabilityService. Null = chưa biết.
    // Cache CHỈ có hiệu lực khi cả đường dẫn BarTender.exe VÀ tên máy khớp
    // với máy hiện tại — để config vô tình copy sang máy khác (hoặc installer
    // ghi đè) không mang theo trạng thái sai.
    public string? BarTenderCapabilityExePath { get; set; }
    public string? BarTenderCapabilityMachine { get; set; }
    public bool? BarTenderXmlScriptSupported { get; set; }
    public string? BarTenderXmlScriptDetail { get; set; }
    public string LastFolder { get; set; } = "";
    public string LastExcelFile { get; set; } = "";
    public bool AutoOpenLastFolder { get; set; } = true;

    public bool RememberEmployee { get; set; } = true;
    public string DefaultEmployee { get; set; } = "";

    public List<LabelDefinition> Labels { get; set; } = new();

    // Danh mục CÔNG CỤ HÌNH ẢNH — tách riêng khỏi Labels vì đây không phải
    // một loại tem, dù cùng cơ chế "card lấy từ cấu hình".
    public List<ToolDefinition> Tools { get; set; } = new();
}