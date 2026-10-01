using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Sau khi tiến trình hand-off của BarTender thoát, chờ job của lệnh in này
/// rời hàng đợi máy in rồi mới trả quyền điều khiển — để lệnh in kế tiếp (ví
/// dụ vòng lặp in nhiều mã tem kính, mỗi mã ghi đè file data) không bắt đầu
/// khi BarTender còn đang spool lệnh trước → tránh "in thiếu mã / lẫn mã".
///
/// Máy in kẹt (tạm dừng / offline / hết giấy) thì KHÔNG báo lỗi (job đã nằm
/// trong hàng đợi, sẽ tự in khi máy in sẵn sàng — báo lỗi sẽ khiến người dùng
/// in lại → trùng tem) mà ghi cảnh báo vào <see cref="PrintWarnings"/>.
/// </summary>
internal static class PrintCompletion
{
    public static void Confirm(
        BarTenderPrintRequest request,
        bool hasRunningBarTender,
        Stopwatch printStopwatch,
        string backendName,
        int startupGraceMs = 8000)
    {
        BarTenderProcess.JobWaitResult wait = BarTenderProcess.WaitForPrintJobToFinish(
            request.PrinterName,
            request.PreexistingJobIds,
            Path.GetFileNameWithoutExtension(request.TemplatePath),
            startupGraceMs);

        if (wait.Warning != null)
            PrintWarnings.Add(wait.Warning);

        // Đệm ngắn giữa 2 lệnh in.
        Thread.Sleep(150);

        PrintDiagnosticsLog.Write(
            $"OK backend={backendName} template={request.TemplatePath} printer={request.PrinterName} " +
            $"hasRunningBarTender={hasRunningBarTender} confirmStatus={wait.Status} " +
            $"elapsedMs={printStopwatch.ElapsedMilliseconds}");

        BarTenderCommandLog.Write(
            $"RESULT ok backend={backendName} confirmStatus={wait.Status} " +
            $"elapsedMs={printStopwatch.ElapsedMilliseconds}");
    }
}

/// <summary>
/// Cảnh báo phát sinh trong một lệnh in nhưng KHÔNG làm lệnh in thất bại
/// (ví dụ máy in đang tạm dừng). Gom theo thread: toàn bộ một lệnh in
/// (LabelService.Print → handler → BarTenderService) chạy trên cùng một
/// thread nền.
/// </summary>
public static class PrintWarnings
{
    [ThreadStatic]
    private static List<string>? t_warnings;

    public static void Reset() => t_warnings = null;

    public static void Add(string warning)
    {
        t_warnings ??= new List<string>();

        if (!t_warnings.Contains(warning))
            t_warnings.Add(warning);
    }

    /// <summary>Lấy và xoá các cảnh báo đã gom.</summary>
    public static List<string> TakeAll()
    {
        List<string> result = t_warnings ?? new List<string>();
        t_warnings = null;
        return result;
    }
}
