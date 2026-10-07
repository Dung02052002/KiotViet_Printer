namespace KiotVietLabelPrinter.Models;

// Gói thông tin chỉnh giá bán khi in Tem đầy đủ. Không đụng tới file Excel
// nguồn - chỉ áp dụng khi ghi dữ liệu ra file data cho BarTender
// (xem ExcelService.CopyToBarTenderData).
public class PriceOverride
{
    public PriceOverrideMode Mode { get; set; } = PriceOverrideMode.Keep;

    // Dùng khi Mode == Uniform: áp dụng cho mọi sản phẩm.
    public double UniformPrice { get; set; }

    // Dùng khi Mode == PerProduct: chỉ những mã có trong đây mới bị ghi đè,
    // các mã còn lại giữ nguyên giá gốc.
    public Dictionary<string, double> ProductOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool TryResolve(string productCode, out double price)
    {
        switch (Mode)
        {
            case PriceOverrideMode.Uniform:
                price = UniformPrice;
                return true;

            case PriceOverrideMode.PerProduct:
                if (!string.IsNullOrWhiteSpace(productCode) &&
                    ProductOverrides.TryGetValue(productCode, out price))
                {
                    return true;
                }

                price = 0;
                return false;

            default:
                price = 0;
                return false;
        }
    }
}
