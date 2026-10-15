namespace KiotVietLabelPrinter.Models.Barcodes;

// Một tem của "Tạo mã vạch": Tên hàng (chỉ để hiển thị phía trên vạch) và Mã
// hàng (được mã hoá Code 128 NGUYÊN VĂN và in lại bằng chữ phía dưới vạch).
public sealed record BarcodeLabelItem(string Name, string Code);
