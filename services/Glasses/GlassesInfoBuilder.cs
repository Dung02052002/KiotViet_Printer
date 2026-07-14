namespace KiotVietLabelPrinter.Services.Glasses;

public static class GlassesInfoBuilder
{
    public static string Build(
        string baseCode,
        string barcode)
    {
        baseCode = baseCode?.Trim() ?? "";
        barcode = barcode?.Trim() ?? "";

        return
            "       KÍNH MẮT\r\n" +
            $"Mã hàng:{baseCode}\r\n" +
            "Nhập từ: Công ty CP XNK Trung Quốc Đại Dương\r\n" +
            "Đ/c: Số 321, d.Trường Chinh,P.Khương Trung,\r\n" +
            "Q.Thanh Xuân,TP Hà Nội,Việt Nam\r\n" +
            "Thông số kỹ thuật: 16*16*7\r\n" +
            $"Mã vạch:{barcode}";
    }

    public static string BuildBarcode(
        string baseCode,
        string employeeCode)
    {
        baseCode = baseCode?.Trim() ?? "";
        employeeCode = employeeCode?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(baseCode))
            return "";

        if (string.IsNullOrWhiteSpace(employeeCode))
            return baseCode;

        return baseCode + employeeCode;
    }
}