using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Một cách gửi lệnh in cho BarTender. Có 2 hiện thực:
///
/// 1) <see cref="StandardCommandLinePrintBackend"/> — dùng /F /PRN /P /X.
///    KHÔNG dùng XMLScript. Chạy trên mọi edition BarTender.
///
/// 2) <see cref="XmlScriptPrintBackend"/> — dùng /XMLScript=. CHỈ dùng khi
///    edition hỗ trợ (Enterprise Automation) và lệnh in cần Named Sub-String.
///
/// 3) <see cref="ComAutomationPrintBackend"/> — COM BarTender.Application,
///    instance riêng/ẩn. Ưu tiên khi có Automation, vì 2 backend trên chuyển
///    lệnh vào BarTender đang mở và làm nó đóng các file người dùng đang mở.
///
/// <see cref="BarTenderService"/> tự chọn backend dựa trên nội dung request.
/// </summary>
public interface IBarTenderPrintBackend
{
    /// <summary>Tên ngắn để ghi log / hiển thị (ví dụ "StandardCommandLine").</summary>
    string Name { get; }

    /// <summary>
    /// Mô tả CHÍNH XÁC dòng lệnh sẽ được gửi cho BarTender — KHÔNG thực thi.
    /// Dùng cho selftest và ghi log trước khi chạy.
    /// </summary>
    string DescribeCommand(BarTenderPrintRequest request);

    /// <summary>Thực hiện in. Ném Exception kèm thông báo rõ ràng nếu lỗi.</summary>
    void Print(BarTenderPrintRequest request, Stopwatch printStopwatch);
}
