using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class LabelService
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();
    private readonly PreviewService _previewService = new();

    public List<ProductRow> ReadProducts(string sourceExcelFile)
    {
        if (string.IsNullOrWhiteSpace(sourceExcelFile))
            throw new Exception("Vui lòng chọn file Excel KiotViet.");

        if (!File.Exists(sourceExcelFile))
            throw new Exception("Không tìm thấy file Excel KiotViet.");

        List<ProductRow> products = _excelService.ReadProducts(sourceExcelFile);

        if (products.Count == 0)
            throw new Exception("Không có dữ liệu sản phẩm trong file Excel.");

        return products;
    }

    public List<PreviewRow> BuildPreview(
        string sourceExcelFile,
        bool printFull,
        bool printBarcode,
        string employeeCode)
    {
        List<ProductRow> products = ReadProducts(sourceExcelFile);

        return _previewService.BuildPreviewRows(
            products,
            printFull,
            printBarcode,
            employeeCode);
    }

    public int Print(
        string sourceExcelFile,
        bool printFull,
        bool printBarcode,
        string employeeCode)
    {
        var config = ConfigService.Instance.Config;
        List<ProductRow> products = ReadProducts(sourceExcelFile);

        if (printFull)
        {
            _excelService.WriteFullLabelData(products, config.FullLabel.Data);
            _barTenderService.Print(config.FullLabel.Template);
        }

        if (printBarcode)
        {
            _excelService.WriteBarcodeLabelData(
                products,
                config.BarcodeLabel.Data,
                employeeCode);

            _barTenderService.Print(config.BarcodeLabel.Template);
        }

        return products.Count;
    }
}