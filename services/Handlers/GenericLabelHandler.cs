using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Interfaces;

namespace KiotVietLabelPrinter.Services.Handlers;

public class GenericLabelHandler : ILabelHandler
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();

    public string HandlerType => "FULL";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        List<PreviewRow> rows = new();

        foreach (ProductRow item in products)
        {
            rows.Add(new PreviewRow
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
            });
        }

        return rows;
    }

    public void PrepareDataAndPrint(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(label.SourceExcelFile))
            throw new Exception("Không tìm thấy file Excel nguồn.");

        _excelService.WriteGenericLabelData(
            label.SourceExcelFile,
            label.DataFilePath);

        _barTenderService.Print(label.TemplatePath);
    }
}