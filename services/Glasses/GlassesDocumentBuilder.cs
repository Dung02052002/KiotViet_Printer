using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Models.Glasses;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesDocumentBuilder
{
    public static GlassesDocument Build(
        ProductRow product,
        string employeeCode)
    {
        //-------------------------------------------------
        // Parse BaseCode
        //-------------------------------------------------

  GlassesParser parser = new();

GlassesParserResult parse =
    parser.Parse(product);

BarcodeParseResult barcode =
    GlassesInfoBuilder.Build(
        product,
        parse.BaseCode,
        employeeCode);

return new GlassesDocument
{
    Product = product,

    BaseCode = barcode.BaseCode,

    Barcode = barcode.BarcodeCode,

    AttributeText = barcode.AttributeText,

    GlassesInfo = barcode.GlassesInfo
};
    }
}