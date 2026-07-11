using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Interfaces;

namespace KiotVietLabelPrinter.Services.Handlers;

public class GlassesLabelHandler : ILabelHandler
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();

    public string HandlerType => "GLASSES";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        List<PreviewRow> rows = new();

        foreach (ProductRow item in products)
        {
            string baseCode = ParseGlassesBaseCode(item);
            string finalBarcode = BuildGlassesBarcode(baseCode, employeeCode);

            rows.Add(new PreviewRow
            {
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                ProductNameWithAttr = item.ProductNameWithAttr,
                ParsedBarcodeCode = baseCode,
                FinalBarcodeCode = finalBarcode,
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
            throw new Exception($"Không tìm thấy file data tem kính:\n{label.DataFilePath}");

        if (string.IsNullOrWhiteSpace(label.TemplatePath) || !File.Exists(label.TemplatePath))
            throw new Exception($"Không tìm thấy file template BarTender:\n{label.TemplatePath}");

        // Phase 1:
        // Vẫn copy data như tem thường, KHÔNG sửa file data để chèn mã kính.
        _excelService.CopyToBarTenderData(
            label.SourceExcelFile,
            label.DataFilePath,
            false);

        // Chỉ bơm GLASSES_INFO cho tem đầu tiên / lô in hiện tại.
        // Nếu bạn in 1 file excel nhiều sản phẩm thì block này sẽ lấy sản phẩm đầu tiên làm info.
        // Đây là phase 1 đúng theo yêu cầu "chỉ sửa GLASSES_INFO, không sửa data file".
        ProductRow first = products.FirstOrDefault()
            ?? throw new Exception("Không có sản phẩm nào để in.");

        string baseCode = ParseGlassesBaseCode(first);
        string finalBarcode = BuildGlassesBarcode(baseCode, employeeCode);
        string glassesInfo = BuildGlassesInfo(baseCode, finalBarcode);

        Dictionary<string, string> namedSubStrings = new()
        {
            ["GLASSES_INFO"] = glassesInfo
        };

        _barTenderService.Print(label.TemplatePath, namedSubStrings);
    }
private static string ParseGlassesBaseCode(ProductRow item)
{
    // Ưu tiên cột Tên hàng (thuộc tính)
    string code = BarcodeParser.Parse(
        item.ProductNameWithAttr,
        item.ProductCode);

    if (!string.IsNullOrWhiteSpace(code))
        return code;

    // Nếu không có thì thử Tên hàng
    code = BarcodeParser.Parse(
        item.ProductName,
        item.ProductCode);

    return code;
}

    private static string BuildGlassesBarcode(string baseCode, string colorSuffix)
    {
        baseCode = baseCode?.Trim() ?? "";
        colorSuffix = colorSuffix?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        if (string.IsNullOrWhiteSpace(colorSuffix))
            return baseCode;

        return baseCode + colorSuffix;
    }

private static string BuildGlassesInfo(string baseCode, string barcode)
{
    return
        "       KÍNH MẮT\r\n" +
        $"Mã hàng:{baseCode}\r\n" +
        "Nhập từ: Công ty CP XNK Trung Quốc Đại Dương\r\n" +
        "Đ/c: Số 321, d.Trường Chinh,P.Khương Trung,\r\n" +
        "Q.Thanh Xuân,TP Hà Nội,Việt Nam\r\n" +
        "Thông số kỹ thuật: 16*16*7\r\n" +
        $"Mã vạch:{barcode}";
}
}