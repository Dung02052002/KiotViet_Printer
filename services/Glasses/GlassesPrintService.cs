using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesPrintService
{
    private readonly GlassesExcelService _excelService = new();
    private readonly GlassesBarTenderService  _barTenderService = new();

    public void Print(
        List<ProductRow> products,
        LabelDefinition label,
        string colorCode)
    {
        if (products == null || products.Count == 0)
            throw new Exception("Không có sản phẩm để in.");

        if (string.IsNullOrWhiteSpace(label.DataFilePath))
            throw new Exception("Chưa cấu hình file Data của tem kính.");

        if (string.IsNullOrWhiteSpace(label.TemplatePath))
            throw new Exception("Chưa cấu hình file BarTender.");

        if (!File.Exists(label.DataFilePath))
            throw new Exception($"Không tìm thấy file:\n{label.DataFilePath}");

        if (!File.Exists(label.TemplatePath))
            throw new Exception($"Không tìm thấy file:\n{label.TemplatePath}");

        //============================
        // 1 sản phẩm
        //============================

        if (products.Count == 1)
        {
            PrintSingle(
                products[0],
                label,
                colorCode);

            return;
        }

        //============================
        // nhiều sản phẩm
        //============================

        foreach (ProductRow product in products)
        {
            PrintSingle(
                product,
                label,
                colorCode);
        }
    }

    private void PrintSingle(
    ProductRow product,
    LabelDefinition label,
    string employeeCode)
{
    GlassesDocument document =
        GlassesDocumentBuilder.Build(
            product,
            employeeCode);

    _excelService.WriteSingleProduct(
        document.Product,
        label.DataFilePath);

    _barTenderService.Print(
        label,
        document.GlassesInfo);
}
}