namespace KiotVietLabelPrinter.Models;

public class AppConfig
{
    public string BarTenderExe { get; set; } = "";
    public string PrinterName { get; set; } = "";
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