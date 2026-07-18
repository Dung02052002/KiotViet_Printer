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

string baseCode = parse.BaseCode;

string barcode =
    GlassesInfoBuilder.BuildBarcode(
        baseCode,
        employeeCode);

string attribute =
    GlassesInfoBuilder.BuildAttribute(product);

string info =
    GlassesInfoBuilder.BuildInfo(
        baseCode,
        barcode);

return new GlassesDocument
{
    Product = product,
    BaseCode = baseCode,
    Barcode = barcode,
    AttributeText = attribute,
    GlassesInfo = info
};
    }
}