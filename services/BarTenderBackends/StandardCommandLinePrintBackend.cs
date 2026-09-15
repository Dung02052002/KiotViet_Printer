using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Backend in KHÔNG dùng XMLScript. Dùng tham số dòng lệnh chuẩn của BarTender:
///
///     bartend.exe /F="template.btw" /PRN="Máy in" /P [/X]
///
///   /F  mở file template
///   /PRN chọn máy in
///   /P  in ngay (số lượng lấy theo cột số lượng trong file data mà template
///       .btw đã liên kết sẵn — dữ liệu được ExcelService ghi vào file đó
///       TRƯỚC khi gọi in)
///   /X  đóng BarTender sau khi in (chỉ thêm khi BarTender chưa mở sẵn)
///
/// Chạy được trên MỌI edition BarTender (Starter/Professional/Automation/
/// Enterprise). Không bao giờ tạo popup #3112.
///
/// Hạn chế: KHÔNG bơm được Named Sub-String. Chỉ dùng cho lệnh in mà toàn bộ
/// dữ liệu đã nằm trong file data (Tem đầy đủ, Tem mã vạch, Tem thường).
/// </summary>
public sealed class StandardCommandLinePrintBackend : IBarTenderPrintBackend
{
    public string Name => "StandardCommandLine";

    public string DescribeCommand(BarTenderPrintRequest request)
    {
        bool hasRunning = BarTenderProcess.IsBarTenderRunning(request.BarTenderExe);

        string args =
            $"/F=\"{request.TemplatePath}\" /PRN=\"{request.PrinterName}\" /P" +
            (hasRunning ? "" : " /X");

        return $"\"{request.BarTenderExe}\" {args}";
    }

    public void Print(BarTenderPrintRequest request, Stopwatch printStopwatch)
    {
        if (request.RequiresNamedSubStrings)
        {
            // Không bao giờ được xảy ra — BarTenderService chỉ chọn backend này
            // khi request KHÔNG có Named Sub-String. Chặn ở đây cho chắc để dữ
            // liệu động không bị "rơi" âm thầm.
            throw new InvalidOperationException(
                "StandardCommandLinePrintBackend không hỗ trợ Named Sub-String. " +
                "Đây là lỗi chọn backend — cần dùng XmlScriptPrintBackend.");
        }

        bool hasRunningBarTender = BarTenderProcess.IsBarTenderRunning(request.BarTenderExe);

        ProcessStartInfo psi = BarTenderProcess.BuildPsi(request.BarTenderExe);

        // Dùng ArgumentList để .NET tự escape — an toàn với đường dẫn có khoảng
        // trắng và tiếng Việt có dấu.
        psi.ArgumentList.Add($"/F={request.TemplatePath}");
        psi.ArgumentList.Add($"/PRN={request.PrinterName}");
        psi.ArgumentList.Add("/P");

        if (!hasRunningBarTender)
            psi.ArgumentList.Add("/X");

        BarTenderCommandLog.WriteCommandBlock(
            Name,
            psi.FileName,
            string.Join(" ", psi.ArgumentList),
            request.TemplatePath,
            request.PrinterName,
            request.DescribeNamedSubStrings());

        BarTenderProcess.RunResult result =
            BarTenderProcess.Run(psi, hasRunningBarTender);

        if (!result.Exited)
            throw new Exception(
                "BarTender xử lý lệnh in quá thời gian chờ (30 giây).\n\n" +
                $"Template: {request.TemplatePath}\n" +
                $"Printer: {request.PrinterName}\n\n" +
                "Vui lòng kiểm tra BarTender có đang bị treo hoặc đang chờ hộp thoại xác nhận.");

        if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.StdErr))
            throw new Exception(
                "BarTender báo lỗi khi in (chế độ command line chuẩn).\n\n" +
                $"Template: {request.TemplatePath}\n" +
                $"Printer: {request.PrinterName}\n" +
                $"ExitCode: {result.ExitCode}\n\n" +
                $"STDERR:\n{result.StdErr}\n\n" +
                $"STDOUT:\n{result.StdOut}");

        PrintCompletion.Confirm(
            request.PrinterName,
            request.TemplatePath,
            hasRunningBarTender,
            printStopwatch,
            Name);
    }
}
