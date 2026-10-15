using KiotVietLabelPrinter.Models.Barcodes;
using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Models.Labels;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesDocumentBuilder
{
    /// <summary>
    /// Dựng dữ liệu một tem kính: parse mã từ tên hàng, ghép mã vạch = mã +
    /// <paramref name="colorCode"/> (ô "mã nhân viên" trên màn hình in tem
    /// kính được dùng làm mã màu), rồi dựng các khối chữ cho BarTender.
    /// </summary>
    public static GlassesDocument Build(
        ProductRow product,
        string colorCode)
    {
        GlassesParserResult parse = new GlassesParser().Parse(product);

        BarcodeParseResult barcode =
            GlassesInfoBuilder.Build(
                product,
                parse.BaseCode,
                colorCode);

        return new GlassesDocument
        {
            Product = product,
            BaseCode = barcode.BaseCode,
            Barcode = barcode.BarcodeCode,
            AttributeText = barcode.AttributeText,
            GlassesInfo = barcode.GlassesInfo,
            GlassesTitle = barcode.GlassesTitle,
            GlassesInfoLeft = barcode.GlassesInfoLeft,
            GlassesInfoRight = barcode.GlassesInfoRight
        };
    }
}
