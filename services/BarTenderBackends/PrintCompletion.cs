using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Sau khi tiến trình hand-off của BarTender thoát, chờ hàng đợi máy in thật
/// sự rỗng lại rồi mới trả quyền điều khiển — để lệnh in kế tiếp (ví dụ vòng
/// lặp in nhiều mã tem kính, mỗi mã ghi đè file data) không bắt đầu khi
/// BarTender còn đang spool lệnh trước → tránh "in thiếu mã / lẫn mã".
/// </summary>
internal static class PrintCompletion
{
    public static void Confirm(
        string printerName,
        string btwFile,
        bool hasRunningBarTender,
        Stopwatch printStopwatch,
        string backendName)
    {
        string confirmStatus = BarTenderProcess.WaitForPrintJobToFinish(printerName);

        // Đệm ngắn giữa 2 lệnh in.
        Thread.Sleep(150);

        PrintDiagnosticsLog.Write(
            $"OK backend={backendName} template={btwFile} printer={printerName} " +
            $"hasRunningBarTender={hasRunningBarTender} confirmStatus={confirmStatus} " +
            $"elapsedMs={printStopwatch.ElapsedMilliseconds}");

        BarTenderCommandLog.Write(
            $"RESULT ok backend={backendName} confirmStatus={confirmStatus} " +
            $"elapsedMs={printStopwatch.ElapsedMilliseconds}");
    }
}
