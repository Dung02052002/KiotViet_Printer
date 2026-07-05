using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Interfaces;

namespace KiotVietLabelPrinter.Services.Handlers;

public class BarcodeLabelHandler : ILabelHandler
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();

    public string HandlerType => "BARCODE";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        List<PreviewRow> rows = new();

        foreach (var item in products)
        {
            string parsed = BarcodeParser.Parse(
                item.ProductNameWithAttr,
                item.ProductCode);

            string finalCode = parsed;

            if (label.AppendEmployeeCode &&
                !string.IsNullOrWhiteSpace(employeeCode))
            {
                finalCode = $"{parsed}-{employeeCode.Trim()}";
            }

            rows.Add(new PreviewRow
            {
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                ProductNameWithAttr = item.ProductNameWithAttr,
                ParsedBarcodeCode = parsed,
                FinalBarcodeCode = finalCode,
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
        _excelService.WriteBarcodeLikeData(products, label, employeeCode);
        _barTenderService.Print(label.TemplatePath);
    }
}