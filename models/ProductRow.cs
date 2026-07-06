namespace KiotVietLabelPrinter.Models;

public class ProductRow
{
    public string StoreName { get; set; } = "";
    public string Category { get; set; } = "";

    public string ProductCode { get; set; } = "";          // C - Mã hàng
    public string Barcode { get; set; } = "";              // D - Mã vạch
    public string ProductName { get; set; } = "";          // E - Tên hàng
    public string ProductNameWithAttr { get; set; } = "";  // F - Tên hàng hiển thị / tên hàng (thuộc tính)

    public string Unit { get; set; } = "";                 // G - Đơn vị
    public double Quantity { get; set; }                   // H - Số lượng
    public double Price { get; set; }                      // I - Giá bán
    public string Description { get; set; } = "";          // J - Mô tả

    public string Attribute { get; set; } = "";            // K - Thuộc tính 1
    public string Attribute2 { get; set; } = "";           // L - Thuộc tính 2
    public string Position { get; set; } = "";             // M - Vị trí
}