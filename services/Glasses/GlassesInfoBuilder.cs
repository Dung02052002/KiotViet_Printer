using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesInfoBuilder
{
    //---------------------------------------------------------
    // Barcode = mã parse + mã màu
    //---------------------------------------------------------

    public static string BuildBarcode(
        string baseCode,
        string colorCode)
    {
        baseCode = baseCode?.Trim().ToUpper() ?? "";
        colorCode = colorCode?.Trim().ToUpper() ?? "";

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        if (string.IsNullOrWhiteSpace(colorCode))
            return baseCode;

        return $"{baseCode}{NormalizeColorCode(colorCode)}";
    }

    //---------------------------------------------------------
    // Thuộc tính
    //---------------------------------------------------------

    public static string BuildAttribute(ProductRow product)
    {
        return product.ProductNameWithAttr?.Trim() ?? "";
    }

    //---------------------------------------------------------
    // Chuỗi GLASSES_INFO (runtime, không ghi vào file data)
    //---------------------------------------------------------
    public static string BuildInfo(
        ProductRow product,
        string baseCode,
        string barcode)
    {
        baseCode = baseCode?.Trim() ?? "";
        barcode = barcode?.Trim() ?? "";

        string text = product?.Description?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(text))
        {
            // Nếu có sẵn block mô tả thì chỉ thay dòng mã hàng/mã vạch bằng mã parse.
            // Nếu thiếu 1 trong 2 dòng thì bổ sung để đảm bảo GLASSES_INFO luôn đầy đủ.
            return EnsureRequiredLines(
                ReplaceCodeLines(text, baseCode, barcode),
                baseCode,
                barcode);
        }

        return BuildDefaultInfoBlock(baseCode, barcode);
    }

    private static string ReplaceCodeLines(
        string text,
        string baseCode,
        string barcode)
    {
        if (!string.IsNullOrWhiteSpace(baseCode))
        {
            text = Regex.Replace(
                text,
                @"(?im)^(\s*Mã\s*hàng\s*:?\s*).*$",
                m => m.Groups[1].Value + baseCode);
        }

        if (!string.IsNullOrWhiteSpace(barcode))
        {
            text = Regex.Replace(
                text,
                @"(?im)^(\s*Mã\s*vạch\s*:?\s*).*$",
                m => m.Groups[1].Value + barcode);
        }

        return text;
    }

    private static string EnsureRequiredLines(
        string text,
        string baseCode,
        string barcode)
    {
        if (!Regex.IsMatch(text, @"(?im)^\s*Mã\s*hàng\s*:?"))
        {
            text += Environment.NewLine + $"Mã hàng: {baseCode}";
        }

        if (!Regex.IsMatch(text, @"(?im)^\s*Mã\s*vạch\s*:?"))
        {
            text += Environment.NewLine + $"Mã vạch: {barcode}";
        }

        return text.TrimEnd();
    }

    private static string BuildDefaultInfoBlock(
        string baseCode,
        string barcode)
    {
        return string.Join(
            Environment.NewLine,
            "KÍNH MẮT",
            $"Mã hàng: {baseCode}",
            "Nhập từ: Công ty CP XNK",
            "Trung Quốc Đại Dương.",
            "Đ/c: Số 321, đ.Trường",
            "Chinh,P.Khương Trung,",
            "Q.Thanh Xuân,TP Hà Nội,Việt",
            "Nam",
            "Thông số kỹ thuật: 16*16*7",
            "Thông số kỹ thuật:",
            $"Mã vạch: {barcode}");
    }

    private static string NormalizeColorCode(string colorCode)
    {
        if (string.IsNullOrWhiteSpace(colorCode))
            return "";

        colorCode = colorCode.Trim().ToUpper();

        // Hỗ trợ nhập nhanh "1" thành "-1" theo thói quen nhập mã màu.
        if (!colorCode.StartsWith("-") &&
            Regex.IsMatch(colorCode, @"^[0-9]+$"))
        {
            return $"-{colorCode}";
        }

        return colorCode;
    }

    public static BarcodeParseResult Build(
    ProductRow product,
    string baseCode,
    string colorCode)
{
    BarcodeParseResult result = new();

    result.BaseCode = baseCode;

    result.BarcodeCode =
        BuildBarcode(
            baseCode,
            colorCode);

    result.AttributeText =
        BuildAttribute(product);

    result.GlassesInfo =
        BuildInfo(
            product,
            baseCode,
            result.BarcodeCode);

    return result;
}
}