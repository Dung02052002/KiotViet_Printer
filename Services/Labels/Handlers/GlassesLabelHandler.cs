using KiotVietLabelPrinter.Models.Labels;
using KiotVietLabelPrinter.Services.Glasses;

namespace KiotVietLabelPrinter.Services.Labels.Handlers;

/// <summary>
/// Tem kính: ô "mã nhân viên" trên màn hình in được dùng làm MÃ MÀU — mã vạch
/// = mã parse từ tên hàng + mã màu (xem GlassesInfoBuilder.BuildBarcode).
/// Có thể ghi đè giá bán theo lựa chọn trên màn hình in (giống Tem đầy đủ).
/// </summary>
public class GlassesLabelHandler : ILabelHandler
{
    private readonly GlassesPrintService _printService = new();

    public string HandlerType => "GLASSES";

    public List<PreviewRow> BuildPreview(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        string colorCode = employeeCode;
        GlassesParser parser = new();
        List<PreviewRow> rows = new();

        foreach (ProductRow item in products)
        {
            string baseCode = parser.Parse(item).BaseCode;

            rows.Add(new PreviewRow
            {
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                ProductNameWithAttr = item.ProductNameWithAttr,
                ParsedBarcodeCode = baseCode,
                FinalBarcodeCode = GlassesInfoBuilder.BuildBarcode(baseCode, colorCode),
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
        // Kiểm tra file data / template do GlassesPrintService.Print đảm nhận.
        _printService.Print(
            products,
            label,
            colorCode: employeeCode,
            priceOverride);
    }
}
