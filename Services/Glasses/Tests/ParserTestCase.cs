namespace KiotVietLabelPrinter.Services.Glasses.Tests;

public class ParserTestCase
{
    public string Name { get; set; } = "";

    public string Input { get; set; } = "";

    public string Expected { get; set; } = "";

    // Tên hàng gốc (cột E), tuỳ chọn. Có giá trị thì Input được coi là
    // "Tên hàng (thuộc tính)" (cột F) và test chạy qua Parse(ProductRow).
    public string ProductName { get; set; } = "";

    public override string ToString()
    {
        return Name;
    }
}