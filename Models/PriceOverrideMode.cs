namespace KiotVietLabelPrinter.Models;

// 3 chế độ giá bán khi in Tem đầy đủ (xem Form/FormMain.cs, services/ExcelService.cs).
public enum PriceOverrideMode
{
    Keep = 0,       // Giữ nguyên giá - dùng nguyên giá trong file Excel
    Uniform = 1,    // Sửa tất cả cùng 1 giá - ghi đè giá của mọi sản phẩm
    PerProduct = 2  // Sửa giá một vài sản phẩm - chỉ ghi đè sản phẩm đã chọn
}
