using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Models.Labels;

namespace KiotVietLabelPrinter.Services.Labels.Handlers;

/// <summary>
/// Tem đầy đủ: chép nguyên dữ liệu file nguồn sang file data, có thể ghi đè
/// giá bán / tên hàng theo lựa chọn trên màn hình in (không đụng file nguồn).
/// </summary>
public class FullLabelHandler : ILabelHandler
{
    private readonly ExcelService _excelService = new();
    private readonly BarTenderService _barTenderService = new();

    public string HandlerType => "FULL";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        return LabelHandlerSupport.BuildPlainPreview(products);
    }

    public void PrepareDataAndPrint(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode,
        PriceOverride? priceOverride = null,
        Dictionary<string, string>? nameOverrides = null)
    {
        LabelHandlerSupport.EnsureFilesExist(label, "file data tem");

        _excelService.WriteGenericLabelData(
            label.SourceExcelFile,
            label.DataFilePath,
            priceOverride,
            nameOverrides);

        _barTenderService.Print(label.TemplatePath);
    }
}
