using System.Diagnostics;
using System.Drawing.Printing;
using KiotVietLabelPrinter.Services.BarTenderBackends;

namespace KiotVietLabelPrinter.Services;

/// <summary>
/// Điều phối in BarTender. KHÔNG tự chạy tiến trình — chọn backend phù hợp
/// rồi giao việc:
///
///   - Không có Named Sub-String  → StandardCommandLinePrintBackend (/F /PRN /P /X).
///                                   Chạy trên MỌI edition, không bao giờ có popup #3112.
///   - Có Named Sub-String        → XmlScriptPrintBackend (/XMLScript=).
///                                   Chỉ khi edition hỗ trợ (Enterprise Automation);
///                                   nếu không → thông báo rõ ràng, KHÔNG gọi BarTender.
/// </summary>
public class BarTenderService
{
    private static readonly SemaphoreSlim PrintGate = new(1, 1);

    private readonly StandardCommandLinePrintBackend _standardBackend = new();
    private readonly XmlScriptPrintBackend _xmlScriptBackend = new();

    public void Print(string btwFile)
    {
        Print(btwFile, null);
    }

    public void Print(string btwFile, Dictionary<string, string>? namedSubStrings)
    {
        PrintGate.Wait();

        Stopwatch printStopwatch = Stopwatch.StartNew();

        try
        {
            BarTenderPrintRequest request = BuildRequest(btwFile, namedSubStrings);

            IBarTenderPrintBackend backend = SelectBackend(request);

            BarTenderCommandLog.Write(
                $"SELECT backend={backend.Name} template={request.TemplatePath} " +
                $"needsNamedSubStrings={request.RequiresNamedSubStrings} " +
                $"capability={DescribeCapability()}");

            backend.Print(request, printStopwatch);
        }
        catch (Exception ex)
        {
            PrintDiagnosticsLog.Write(
                $"FAIL template={btwFile} elapsedMs={printStopwatch.ElapsedMilliseconds} error={ex.Message}");
            BarTenderCommandLog.Write($"RESULT fail template={btwFile} error={ex.Message}");
            throw;
        }
        finally
        {
            PrintGate.Release();
        }
    }

    /// <summary>
    /// Trả về CHÍNH XÁC dòng lệnh sẽ được gửi cho BarTender cho một lệnh in —
    /// KHÔNG thực thi. Dùng cho selftest (--bartender-selftest).
    /// </summary>
    public string DescribePlannedCommand(
        string btwFile,
        Dictionary<string, string>? namedSubStrings,
        out string backendName)
    {
        BarTenderPrintRequest request = BuildRequest(btwFile, namedSubStrings, validateFiles: false);
        IBarTenderPrintBackend backend = SelectBackend(request, throwWhenUnsupported: false);
        backendName = backend.Name;
        return backend.DescribeCommand(request);
    }

    //---------------------------------------------------------

    private static BarTenderPrintRequest BuildRequest(
        string btwFile,
        Dictionary<string, string>? namedSubStrings,
        bool validateFiles = true)
    {
        string bartenderExe = ConfigService.Instance.Config.BarTenderExe;

        if (string.IsNullOrWhiteSpace(bartenderExe))
            throw new Exception("Chưa cấu hình đường dẫn BarTender.exe.");

        if (validateFiles && !File.Exists(bartenderExe))
            throw new Exception($"Không tìm thấy BarTender.exe:\n{bartenderExe}");

        if (string.IsNullOrWhiteSpace(btwFile))
            throw new Exception("Đường dẫn file tem đang rỗng.");

        if (validateFiles && !File.Exists(btwFile))
            throw new Exception($"Không tìm thấy file tem:\n{btwFile}");

        string printerName = validateFiles
            ? GetDefaultPrinterName()
            : ResolvePrinterNameBestEffort();

        return new BarTenderPrintRequest
        {
            BarTenderExe = bartenderExe,
            TemplatePath = btwFile,
            PrinterName = printerName,
            NamedSubStrings = namedSubStrings is { Count: > 0 }
                ? new Dictionary<string, string>(namedSubStrings)
                : null
        };
    }

    private IBarTenderPrintBackend SelectBackend(
        BarTenderPrintRequest request,
        bool throwWhenUnsupported = true)
    {
        // Lệnh in KHÔNG cần bơm dữ liệu động ⇒ luôn dùng command line chuẩn.
        // Đây là toàn bộ "Tem đầy đủ" / "Tem mã vạch" / "Tem thường": dữ liệu
        // đã được ExcelService ghi vào file data mà template .btw liên kết.
        // => KHÔNG bao giờ đụng /XMLScript ⇒ KHÔNG bao giờ có popup #3112.
        if (!request.RequiresNamedSubStrings)
            return _standardBackend;

        // Từ đây: request có Named Sub-String ⇒ bắt buộc /XMLScript=.
        BarTenderCapabilityService cap = BarTenderCapabilityService.Instance;

        // Dò edition qua registry (1 lần, cache lại) TRƯỚC khi thử — để máy
        // không có Enterprise Automation nhận thông báo rõ ràng ngay, không
        // phải chờ popup #3112 rồi timeout.
        cap.EnsureProbed(request.BarTenderExe);

        if (cap.XmlScriptSupported == false && throwWhenUnsupported)
            throw BarTenderCapabilityService.BuildEnterpriseRequiredException();

        return _xmlScriptBackend;
    }

    private static string DescribeCapability()
    {
        BarTenderCapabilityService cap = BarTenderCapabilityService.Instance;
        return cap.XmlScriptSupported switch
        {
            true => "xmlscript=supported",
            false => "xmlscript=unsupported",
            _ => "xmlscript=unknown"
        };
    }

    // Chỉ cho selftest/mô tả lệnh — không ném lỗi nếu chưa có máy in.
    private static string ResolvePrinterNameBestEffort()
    {
        try
        {
            return GetDefaultPrinterName();
        }
        catch
        {
            string configured = ConfigService.Instance.Config.PrinterName;
            return string.IsNullOrWhiteSpace(configured)
                ? "(máy in mặc định Windows)"
                : configured;
        }
    }

    private static string GetDefaultPrinterName()
    {
        string configuredPrinter = ConfigService.Instance.Config.PrinterName;

        if (!string.IsNullOrWhiteSpace(configuredPrinter))
        {
            PrinterSettings configuredSettings = new() { PrinterName = configuredPrinter };

            if (!configuredSettings.IsValid)
                throw new Exception($"Máy in đã cấu hình không hợp lệ hoặc không còn kết nối: {configuredPrinter}");

            return configuredPrinter;
        }

        PrinterSettings settings = new();

        if (string.IsNullOrWhiteSpace(settings.PrinterName))
            throw new Exception("Không xác định được máy in mặc định của Windows.");

        if (!settings.IsValid)
            throw new Exception($"Máy in mặc định không hợp lệ: {settings.PrinterName}");

        return settings.PrinterName;
    }
}
