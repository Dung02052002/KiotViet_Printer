using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesDocumentBuilder
{
    public static GlassesDocument Build(
        ProductRow product,
        string employeeCode)
    {
        string baseCode =
            GlassesParser.Parse(product);

        string barcode =
            GlassesInfoBuilder.BuildBarcode(
                baseCode,
                employeeCode);

        string info =
            GlassesInfoBuilder.Build(
                baseCode,
                barcode);

        return new GlassesDocument
        {
            Product = product,
            BaseCode = baseCode,
            Barcode = barcode,
            GlassesInfo = info
        };
    }
}