using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Backend in dùng /XMLScript= (BTXML Print Script). CHỈ được
/// <see cref="BarTenderService"/> chọn khi:
///   1) request có Named Sub-String (dữ liệu động không nằm trong file data), VÀ
///   2) edition BarTender KHÔNG bị xác nhận là thiếu Enterprise Automation.
///
/// Nếu trong lúc chạy phát hiện máy không hỗ trợ (timeout nghi do dialog #3112,
/// hoặc STDERR chứa dấu hiệu lỗi giới hạn edition) → đánh dấu vào
/// BarTenderCapabilityService để các lần sau KHÔNG thử lại, rồi ném thông báo
/// rõ ràng (không phải popup gốc của BarTender).
/// </summary>
public sealed class XmlScriptPrintBackend : IBarTenderPrintBackend
{
    public string Name => "XmlScript";

    public string DescribeCommand(BarTenderPrintRequest request)
    {
        bool hasRunning = BarTenderProcess.IsBarTenderRunning(request.BarTenderExe);

        string args =
            "/XMLScript=\"<debug_xml\\print_*.xml>\"" +
            (hasRunning ? "" : " /X");

        string xmlPreview = PrintXmlBuilder.BuildXml(
            request.TemplatePath,
            request.NamedSubStrings,
            request.PrinterName);

        return $"\"{request.BarTenderExe}\" {args}\n--- XML body ---\n{xmlPreview}";
    }

    public void Print(BarTenderPrintRequest request, Stopwatch printStopwatch)
    {
        BarTenderCapabilityService cap = BarTenderCapabilityService.Instance;

        string xmlPath = PrintXmlBuilder.WriteXmlNearApp(
            request.TemplatePath,
            request.NamedSubStrings,
            request.PrinterName);

        bool hasRunningBarTender = BarTenderProcess.IsBarTenderRunning(request.BarTenderExe);

        // Nếu BarTender đã mở sẵn, chỉ gửi XML cho instance hiện tại; không /X.
        string arguments = $"/XMLScript=\"{xmlPath}\"";
        if (!hasRunningBarTender)
            arguments += " /X";

        ProcessStartInfo psi = BarTenderProcess.BuildPsi(request.BarTenderExe);
        psi.Arguments = arguments;

        BarTenderCommandLog.WriteCommandBlock(
            Name,
            psi.FileName,
            psi.Arguments,
            request.TemplatePath,
            request.PrinterName,
            request.DescribeNamedSubStrings());

        // Lần đầu chưa biết edition: chờ ngắn hơn — nếu dialog #3112 bật lên
        // thì bị kill sau ~12s thay vì hiện suốt 30s.
        int primaryTimeoutMs = cap.XmlScriptSupported == null && !hasRunningBarTender
            ? 12000
            : 30000;

        BarTenderProcess.RunResult result =
            BarTenderProcess.Run(psi, hasRunningBarTender, primaryTimeoutMs);

        if (!result.Exited)
        {
            HandleTimeout(cap, request, xmlPath);
            return; // HandleTimeout luôn ném exception
        }

        if (result.ExitCode != 0 || !string.IsNullOrWhiteSpace(result.StdErr))
        {
            HandleProcessError(cap, request, xmlPath, result);
            return; // HandleProcessError luôn ném exception
        }

        // In thành công qua XMLScript ⇒ máy này chắc chắn hỗ trợ.
        if (cap.XmlScriptSupported != true)
            cap.MarkSupported();

        PrintCompletion.Confirm(
            request.PrinterName,
            request.TemplatePath,
            hasRunningBarTender,
            printStopwatch,
            Name);
    }

    private static void HandleTimeout(
        BarTenderCapabilityService cap,
        BarTenderPrintRequest request,
        string xmlPath)
    {
        // Trên máy KHÔNG hỗ trợ Enterprise Automation, timeout thường là do
        // dialog #3112 đang treo chờ bấm OK (BarTenderProcess.Run đã kill).
        if (cap.XmlScriptSupported == null)
        {
            cap.MarkUnsupported(
                "Timeout khi gọi /XMLScript= — nghi ngờ dialog giới hạn edition " +
                "(#3112) đang chờ xác nhận.");

            throw BarTenderCapabilityService.BuildEnterpriseRequiredException();
        }

        // Đã từng xác nhận hỗ trợ ⇒ timeout thật (BarTender treo lý do khác).
        throw new Exception(
            "BarTender xử lý lệnh in quá thời gian chờ (30 giây).\n\n" +
            $"Template: {request.TemplatePath}\n" +
            $"Printer: {request.PrinterName}\n" +
            $"XML: {xmlPath}\n\n" +
            "Vui lòng kiểm tra BarTender có đang bị treo hoặc đang chờ hộp thoại xác nhận.");
    }

    private static void HandleProcessError(
        BarTenderCapabilityService cap,
        BarTenderPrintRequest request,
        string xmlPath,
        BarTenderProcess.RunResult result)
    {
        if (cap.XmlScriptSupported != false &&
            BarTenderCapabilityService.LooksLikeEditionGateError(result.StdErr, result.StdOut))
        {
            cap.MarkUnsupported(
                "BarTender báo lỗi giới hạn edition khi dùng /XMLScript= (#3112): " +
                result.StdErr);

            throw BarTenderCapabilityService.BuildEnterpriseRequiredException();
        }

        string xmlContent = PrintXmlBuilder.SafeReadAllText(xmlPath);

        throw new Exception(
            "BarTender báo lỗi khi xử lý XML in.\n\n" +
            $"Template: {request.TemplatePath}\n" +
            $"Printer: {request.PrinterName}\n" +
            $"XML: {xmlPath}\n" +
            $"ExitCode: {result.ExitCode}\n\n" +
            $"STDERR:\n{result.StdErr}\n\n" +
            $"STDOUT:\n{result.StdOut}\n\n" +
            $"Nội dung XML:\n{xmlContent}");
    }
}
