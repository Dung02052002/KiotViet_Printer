using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Models.Labels;

namespace KiotVietLabelPrinter.Services.Labels.Handlers;

/// <summary>
/// Tem thường: chép nguyên dữ liệu file nguồn sang file data. Không có sửa
/// giá / sửa tên (màn hình in chỉ cho sửa ở Tem đầy đủ).
/// </summary>
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
            label.DataFilePath);

        _barTenderService.Print(label.TemplatePath);
    }
}
