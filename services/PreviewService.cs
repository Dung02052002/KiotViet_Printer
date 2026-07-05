using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class PreviewService
{
    public List<PreviewRow> BuildPreviewRows(
        List<ProductRow> products,
        bool printFull,
        bool printBarcode,
        string employeeCode)
    {
        List<PreviewRow> rows = new();

        foreach (var item in products)
        {
            string parsedCode = BarcodeParser.Parse(
                item.ProductNameWithAttr,
                item.ProductCode);

            string finalBarcode = parsedCode;

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                finalBarcode = $"{parsedCode}-{employeeCode.Trim()}";
            }

            rows.Add(new PreviewRow
            {
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                ProductNameWithAttr = item.ProductNameWithAttr,
                ParsedBarcodeCode = parsedCode,
                FinalBarcodeCode = finalBarcode,
                Quantity = item.Quantity,
                Price = item.Price,
                IsFullLabel = printFull,
                IsBarcodeLabel = printBarcode
            });
        }

        return rows;
    }
}