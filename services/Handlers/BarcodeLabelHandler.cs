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
        // Dùng đúng file nguồn người dùng vừa chọn
        // label.SourceExcelFile phải được set từ luồng gọi bên ngoài,
        // hoặc bạn sửa chỗ gọi handler để truyền sourceFile vào đây nếu cần.
        throw new NotImplementedException("BarcodeLabelHandler hiện không dùng trực tiếp. Hãy gọi ExcelService.CopyToBarTenderData(...) từ luồng in chính.");
    }
}