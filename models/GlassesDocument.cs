namespace KiotVietLabelPrinter.Models;

public class GlassesDocument
{
    public ProductRow Product { get; set; } = null!;

    public string BaseCode { get; set; } = "";

    public string Barcode { get; set; } = "";

    public string GlassesInfo { get; set; } = "";
}