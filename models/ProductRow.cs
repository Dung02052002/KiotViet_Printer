namespace KiotVietLabelPrinter.Models;

public class ProductRow
{
    public string StoreName { get; set; } = "";
    public string Category { get; set; } = "";
    public string ProductCode { get; set; } = "";      // Mã hàng
    public string Barcode { get; set; } = "";          // Mã vạch
    public string ProductName { get; set; } = "";      // Tên hàng
    public string ProductNameWithAttr { get; set; } = ""; // Tên hàng (thuộc tính)
    public string Unit { get; set; } = "";
    public double Quantity { get; set; }
    public double Price { get; set; }
    public string Description { get; set; } = "";
    public string Attribute { get; set; } = "";
    public string Position { get; set; } = "";
}