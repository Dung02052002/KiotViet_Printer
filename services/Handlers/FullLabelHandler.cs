using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Interfaces;

namespace KiotVietLabelPrinter.Services.Handlers;

public class FullLabelHandler : ILabelHandler
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();

    public string HandlerType => "FULL";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        return products.Select(item => new PreviewRow
        {
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            ProductNameWithAttr = item.ProductNameWithAttr,
            ParsedBarcodeCode = item.ProductCode,
            FinalBarcodeCode = item.ProductCode,
            Quantity = item.Quantity,
            Price = item.Price,
            IsFullLabel = true,
            IsBarcodeLabel = false
        }).ToList();
    }

    public void PrepareDataAndPrint(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        _excelService.WriteGenericLabelData(products, label);
        _barTenderService.Print(label.TemplatePath);
    }
}