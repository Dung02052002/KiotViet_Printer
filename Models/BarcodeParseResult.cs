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

    public string GlassesTitle { get; set; } = "";

    public string GlassesInfoLeft { get; set; } = "";

    public string GlassesInfoRight { get; set; } = "";
}