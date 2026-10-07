namespace KiotVietLabelPrinter.Models;

public class LabelConfig
{
    /// <summary>
    /// Đường dẫn file .btw
    /// </summary>
    public string Template { get; set; } = "";

    /// <summary>
    /// Đường dẫn file Excel Data
    /// </summary>
    public string Data { get; set; } = "";
}