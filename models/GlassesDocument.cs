using KiotVietLabelPrinter.Models;

public class GlassesDocument
{
    public ProductRow Product { get; set; } = null!;

    public string BaseCode { get; set; } = "";

    public string Barcode { get; set; } = "";

    public string AttributeText { get; set; } = "";

    public string GlassesInfo { get; set; } = "";

    public string GlassesTitle { get; set; } = "";

    public string GlassesInfoLeft { get; set; } = "";

    public string GlassesInfoRight { get; set; } = "";
}