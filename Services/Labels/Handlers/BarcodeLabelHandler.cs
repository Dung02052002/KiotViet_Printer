using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Models.Barcodes;
using KiotVietLabelPrinter.Models.Labels;
using KiotVietLabelPrinter.Services.Barcodes;

namespace KiotVietLabelPrinter.Services.Labels.Handlers;

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

        foreach (ProductRow item in products)
        {
            BarcodeParseResult parsed = BarcodeParser.ParseFull(
                item.ProductNameWithAttr,
                item.ProductCode,
                item.ProductName);

            string finalCode = parsed.BarcodeCode;

            if (string.IsNullOrWhiteSpace(finalCode))
                finalCode = item.ProductCode;

            if (!string.IsNullOrWhiteSpace(employeeCode))
                finalCode = $"{finalCode}-{employeeCode.Trim()}";

            rows.Add(new PreviewRow
            {
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                ProductNameWithAttr = item.ProductNameWithAttr,
                ParsedBarcodeCode = parsed.BarcodeCode,
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
        string employeeCode,
        PriceOverride? priceOverride = null,
        Dictionary<string, string>? nameOverrides = null)
    {
        LabelHandlerSupport.EnsureFilesExist(label, "file data tem mã vạch");

        _excelService.WriteBarcodeLikeData(
            label.SourceExcelFile,
            label.DataFilePath,
            employeeCode);

        _barTenderService.Print(label.TemplatePath);
    }
}