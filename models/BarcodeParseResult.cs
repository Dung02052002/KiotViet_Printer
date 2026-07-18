namespace KiotVietLabelPrinter.Models;

public class BarcodeParseResult
{
    /// <summary>
    /// Barcode cuối cùng
    /// </summary>
    public string BarcodeCode { get; set; } = "";

    /// <summary>
    /// BaseCode parser tìm được
    /// </summary>
    public string BaseCode { get; set; } = "";

    /// <summary>
    /// Thuộc tính
    /// </summary>
    public string AttributeText { get; set; } = "";

    /// <summary>
    /// Chuỗi hiển thị
    /// </summary>
    public string GlassesInfo { get; set; } = "";
}