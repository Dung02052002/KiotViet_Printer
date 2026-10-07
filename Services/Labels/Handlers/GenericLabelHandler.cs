using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Labels.Handlers;

public class GenericLabelHandler : ILabelHandler
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();

    public string HandlerType => "GENERIC";

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
        string employeeCode,
        PriceOverride? priceOverride = null,
        Dictionary<string, string>? nameOverrides = null)
    {
        // Cùng các kiểm tra như FullLabelHandler: thiếu file data/template thì
        // báo đúng file nào, thay vì lỗi khó hiểu từ NPOI/BarTender sau đó.
        if (string.IsNullOrWhiteSpace(label.SourceExcelFile) || !File.Exists(label.SourceExcelFile))
            throw new Exception($"Không tìm thấy file Excel nguồn:\n{label.SourceExcelFile}");

        if (string.IsNullOrWhiteSpace(label.DataFilePath) || !File.Exists(label.DataFilePath))
            throw new Exception($"Không tìm thấy file data tem:\n{label.DataFilePath}");

        if (string.IsNullOrWhiteSpace(label.TemplatePath) || !File.Exists(label.TemplatePath))
            throw new Exception($"Không tìm thấy file template BarTender:\n{label.TemplatePath}");

        _excelService.WriteGenericLabelData(
            label.SourceExcelFile,
            label.DataFilePath);

        _barTenderService.Print(label.TemplatePath);
    }
}