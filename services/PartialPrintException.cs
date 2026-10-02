using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

/// <summary>
/// Lệnh in chỉ in được MỘT PHẦN (VD tem kính in từng mã, vài mã lỗi).
/// Các mã trong PrintedProducts đã thực sự được gửi in — phải ghi lịch sử
/// cho chúng, nếu không lịch sử sẽ thiếu dù tem đã ra máy in.
/// </summary>
public class PartialPrintException : Exception
{
    public IReadOnlyList<ProductRow> PrintedProducts { get; }

    public PartialPrintException(string message, IReadOnlyList<ProductRow> printedProducts)
        : base(message)
    {
        PrintedProducts = printedProducts;
    }
}
