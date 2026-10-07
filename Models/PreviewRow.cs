namespace KiotVietLabelPrinter.Models;

public class PreviewRow
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductNameWithAttr { get; set; } = "";
    public string ParsedBarcodeCode { get; set; } = "";
    public string FinalBarcodeCode { get; set; } = "";
    public double Quantity { get; set; }
    public double Price { get; set; }

    public bool IsFullLabel { get; set; }
    public bool IsBarcodeLabel { get; set; }
}