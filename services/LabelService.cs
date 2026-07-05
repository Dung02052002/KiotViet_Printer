using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class LabelService
{
    private readonly ExcelService _excelService = new();
    private readonly HistoryService _historyService = new();
    private readonly LabelHandlerFactory _handlerFactory = new();
    private readonly LabelCatalogService _catalogService = new();

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
        string labelCode,
        string employeeCode)
    {
        List<ProductRow> products = ReadProducts(sourceExcelFile);

        LabelDefinition label = _catalogService.GetByCode(labelCode);
        var handler = _handlerFactory.GetHandler(label.HandlerType);

        return handler.BuildPreview(products, label, employeeCode);
    }

    public int Print(
        string sourceExcelFile,
        string labelCode,
        string employeeCode)
    {
        List<ProductRow> products = ReadProducts(sourceExcelFile);

        LabelDefinition label = _catalogService.GetByCode(labelCode);
        var handler = _handlerFactory.GetHandler(label.HandlerType);

        handler.PrepareDataAndPrint(products, label, employeeCode);

        _historyService.Add(new PrintHistory
        {
            PrintTime = DateTime.Now,
            SourceExcelFile = sourceExcelFile,
            LabelCode = label.Code,
            LabelName = label.Name,
            EmployeeCode = employeeCode,
            ProductCount = products.Count,
            TotalLabels = products.Sum(x => x.Quantity),
            MachineName = Environment.MachineName,
            UserName = Environment.UserName
        });

        return products.Count;
    }
}