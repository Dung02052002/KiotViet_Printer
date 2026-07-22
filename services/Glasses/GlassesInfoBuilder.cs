using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesInfoBuilder
{
    private const string GlassesTitle = "KÍNH MẮT";

    private static readonly string[] RightColumnLines =
    [
        "PP: Công ty TNHH Pucini Việt Nam",
        "Đ/c: Tầng 7, số 113-115 Lê",
        "Duẩn, P.Cửa Nam,",
        "Q.Hoàn Kiếm, TP.Hà Nội",
        "Xuất xứ: Trung Quốc",
        "Nhãn hiệu Pucini",
        "Thành phần: kim loại/nhựa",
        "HDSD: Bảo quản nơi khô ráo"
    ];

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

        // Tem kính dùng block text chuẩn cố định để luôn ra đúng mẫu,
        // không phụ thuộc nội dung Description từ file import.
        return BuildCombinedInfoBlock(baseCode, barcode);
    }

    public static string BuildTitle()
    {
        return GlassesTitle;
    }

    public static string BuildLeftColumn(
        string baseCode,
        string barcode)
    {
        return string.Join(
            Environment.NewLine,
            BuildLeftColumnLines(baseCode, barcode));
    }

    public static string BuildRightColumn()
    {
        return string.Join(Environment.NewLine, RightColumnLines);
    }

    private static string BuildCombinedInfoBlock(
        string baseCode,
        string barcode)
    {
        List<string> lines = [GlassesTitle];

        string[] leftLines = BuildLeftColumnLines(baseCode, barcode);
        int maxLines = Math.Max(leftLines.Length, RightColumnLines.Length);

        for (int index = 0; index < maxLines; index++)
        {
            string left = index < leftLines.Length ? leftLines[index] : "";
            string right = index < RightColumnLines.Length ? RightColumnLines[index] : "";

            if (string.IsNullOrWhiteSpace(right))
            {
                lines.Add(left);
                continue;
            }

            if (string.IsNullOrWhiteSpace(left))
            {
                lines.Add(right);
                continue;
            }

            lines.Add($"{left}\t{right}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string[] BuildLeftColumnLines(
        string baseCode,
        string barcode)
    {
        return
        [
            $"Mã hàng: {baseCode}",
            "Nhập từ: Công ty CP XNK",
            "Trung Quốc Đại Dương.",
            "Đ/c: Số 321, đ.Trường",
            "Chinh,P.Khương Trung,",
            "Q.Thanh Xuân,TP Hà Nội,Việt",
            "Nam",
            "Thông số kỹ thuật: 16*16*7",
            "Thông số kỹ thuật",
            $"Mã vạch: {barcode}"
        ];
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

    result.GlassesTitle =
        BuildTitle();

    result.GlassesInfoLeft =
        BuildLeftColumn(
            baseCode,
            result.BarcodeCode);

    result.GlassesInfoRight =
        BuildRightColumn();

    result.GlassesInfo =
        BuildInfo(
            product,
            baseCode,
            result.BarcodeCode);

    return result;
}
}