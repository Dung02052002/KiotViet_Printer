using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Glasses;
using KiotVietLabelPrinter.Services.Interfaces;

namespace KiotVietLabelPrinter.Services.Handlers;

public class GlassesLabelHandler : ILabelHandler
{
    private readonly GlassesPrintService _printService = new();

    public string HandlerType => "GLASSES";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        string colorCode = employeeCode;

        List<PreviewRow> rows = new();

        foreach (ProductRow item in products)
{
    GlassesParser parser = new();

var parse = parser.Parse(item);

string baseCode = parse.BaseCode;

string barcode =
    GlassesInfoBuilder.BuildBarcode(
        baseCode,
        colorCode);

rows.Add(new PreviewRow
{
    ProductCode = item.ProductCode,
    ProductName = item.ProductName,
    ProductNameWithAttr = item.ProductNameWithAttr,

    ParsedBarcodeCode = baseCode,
    FinalBarcodeCode = barcode,

    Quantity = item.Quantity,
    Price = item.Price,

    IsFullLabel = false,
    IsBarcodeLabel = true
});
}

        return rows;
    }

    public void PrepareDataAndPrint(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        string colorCode = employeeCode;

        if (string.IsNullOrWhiteSpace(label.DataFilePath))
            throw new Exception("Chưa cấu hình file Data.");

        if (string.IsNullOrWhiteSpace(label.TemplatePath))
            throw new Exception("Chưa cấu hình file BarTender.");

        if (!File.Exists(label.DataFilePath))
            throw new Exception(
                $"Không tìm thấy file Data:\n{label.DataFilePath}");

        if (!File.Exists(label.TemplatePath))
            throw new Exception(
                $"Không tìm thấy file BarTender:\n{label.TemplatePath}");

        _printService.Print(
            products,
            label,
            colorCode);
    }
}