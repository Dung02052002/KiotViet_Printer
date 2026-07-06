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

        foreach (ProductRow item in products)
        {
            BarcodeParseResult parsed = BarcodeParser.ParseFull(
                item.ProductNameWithAttr,
                item.ProductCode);

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
        string employeeCode)
    {
        if (string.IsNullOrWhiteSpace(label.SourceExcelFile) || !File.Exists(label.SourceExcelFile))
            throw new Exception($"Không tìm thấy file Excel nguồn:\n{label.SourceExcelFile}");

        if (string.IsNullOrWhiteSpace(label.DataFilePath) || !File.Exists(label.DataFilePath))
            throw new Exception($"Không tìm thấy file data tem mã vạch:\n{label.DataFilePath}");

        if (string.IsNullOrWhiteSpace(label.TemplatePath) || !File.Exists(label.TemplatePath))
            throw new Exception($"Không tìm thấy file template BarTender:\n{label.TemplatePath}");

        _excelService.WriteBarcodeLikeData(
            label.SourceExcelFile,
            label.DataFilePath,
            employeeCode);

        _barTenderService.Print(label.TemplatePath);
    }
}