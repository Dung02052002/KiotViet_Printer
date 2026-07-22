using System.Text.RegularExpressions;
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

    // Ưu tiên lấy nguyên văn nội dung đã có sẵn trong Excel (cột Mô tả,
    // hoặc Tên hàng hiển thị/thuộc tính) — đây là đoạn text đầy đủ
    // (KÍNH MÁT / Mã hàng / Nhập từ / Đvc / Thông số kỹ thuật / Mã vạch...)
    // do người nhập liệu soạn sẵn cho từng sản phẩm.
    // Chỉ khi không có nội dung nào mới rơi về fallback hiển thị mã, và
    // trong trường hợp đó KHÔNG lặp lại mã trong ngoặc nếu barcode trùng
    // hệt baseCode (không có mã NV/màu) — vẫn giữ định dạng "mã (mã vạch)"
    // cho trường hợp barcode khác baseCode.
    public static string BuildInfo(
        ProductRow product,
        string baseCode,
        string barcode)
    {
        baseCode = baseCode?.Trim() ?? "";
        barcode = barcode?.Trim() ?? "";

        string text = !string.IsNullOrWhiteSpace(product?.Description)
            ? product!.Description.Trim()
            : product?.ProductNameWithAttr?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(text))
        {
            // Giữ nguyên toàn bộ nội dung Mô tả (thông tin công ty, xuất xứ,
            // nhập từ, thông số kỹ thuật...) — chỉ thay riêng phần mã nằm
            // trên dòng "Mã hàng" và "Mã vạch" bằng mã ĐÃ PARSE (baseCode/
            // barcode), tránh in ra mã gốc thô của KiotViet không khớp với
            // mã vạch thực sự được encode trên hình.
            return ReplaceCodeLines(text, baseCode, barcode);
        }

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        if (string.IsNullOrWhiteSpace(barcode) ||
            string.Equals(barcode, baseCode, StringComparison.OrdinalIgnoreCase))
            return baseCode;

        return $"{baseCode} ({barcode})";
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

    public static BarcodeParseResult Build(
    ProductRow product,
    string baseCode,
    string employeeCode)
{
    BarcodeParseResult result = new();

    result.BaseCode = baseCode;

    result.BarcodeCode =
        BuildBarcode(
            baseCode,
            employeeCode);

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