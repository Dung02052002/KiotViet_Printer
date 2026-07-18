using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesInfoBuilder
{
    //---------------------------------------------------------
    // Barcode
    //---------------------------------------------------------

    public static string BuildBarcode(
        string baseCode,
        string employeeCode)
    {
        baseCode = baseCode?.Trim().ToUpper() ?? "";
        employeeCode = employeeCode?.Trim().ToUpper() ?? "";

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        if (string.IsNullOrWhiteSpace(employeeCode))
            return baseCode;

        return $"{baseCode}{employeeCode}";
    }

    //---------------------------------------------------------
    // Thuộc tính
    //---------------------------------------------------------

    public static string BuildAttribute(ProductRow product)
    {
        return product.ProductNameWithAttr?.Trim() ?? "";
    }

    //---------------------------------------------------------
    // Chuỗi thông tin kính
    //---------------------------------------------------------

    public static string BuildInfo(
        string baseCode,
        string barcode)
    {
        baseCode = baseCode?.Trim() ?? "";
        barcode = barcode?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        return $"{baseCode} ({barcode})";
    }
}