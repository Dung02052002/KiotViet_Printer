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
        string text = !string.IsNullOrWhiteSpace(product?.Description)
            ? product!.Description.Trim()
            : product?.ProductNameWithAttr?.Trim() ?? "";

        if (!string.IsNullOrWhiteSpace(text))
            return text;

        baseCode = baseCode?.Trim() ?? "";
        barcode = barcode?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        if (string.IsNullOrWhiteSpace(barcode) ||
            string.Equals(barcode, baseCode, StringComparison.OrdinalIgnoreCase))
            return baseCode;

        return $"{baseCode} ({barcode})";
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