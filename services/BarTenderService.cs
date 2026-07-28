using System.Diagnostics;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace KiotVietLabelPrinter.Services;

public class BarTenderService
{
    private static readonly SemaphoreSlim PrintGate = new(1, 1);

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
        string bartenderExe = ConfigService.Instance.Config.BarTenderExe;

        if (string.IsNullOrWhiteSpace(bartenderExe))
            throw new Exception("Chưa cấu hình đường dẫn BarTender.exe.");

        if (!File.Exists(bartenderExe))
            throw new Exception($"Không tìm thấy BarTender.exe:\n{bartenderExe}");

        if (string.IsNullOrWhiteSpace(btwFile))
            throw new Exception("Đường dẫn file tem đang rỗng.");

        if (!File.Exists(btwFile))
            throw new Exception($"Không tìm thấy file tem:\n{btwFile}");

        string printerName = GetDefaultPrinterName();

        string xmlPath = CreatePrintXmlNearApp(
            btwFile,
            namedSubStrings,
            printerName);

        string processName =
            Path.GetFileNameWithoutExtension(bartenderExe);

        bool hasRunningBarTender =
            Process.GetProcessesByName(processName).Length > 0;

        // Nếu BarTender đã mở sẵn (có thể đang mở nhiều file), chỉ gửi
        // XML để instance hiện tại xử lý; không dùng /X vì /X yêu cầu đóng
        // app và có thể bị kẹt bởi các tài liệu đang mở/chờ xác nhận.
        string arguments = $"/XMLScript=\"{xmlPath}\"";

        if (!hasRunningBarTender)
            arguments += " /X";

        // Popup để biết chắc app đang chạy đúng code mới
        // MessageBox.Show($"XML vừa tạo:\n{xmlPath}", "DEBUG BarTender XML");

        ProcessStartInfo psi = new()
        {
            FileName = bartenderExe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using Process? process = Process.Start(psi);

        if (process == null)
            throw new Exception("Không thể gửi lệnh in tới BarTender.");

        bool exited = process.WaitForExit(30000);

        // Khi BarTender đã mở sẵn, tiến trình handoff có thể thoát chậm.
        // Chờ thêm để đảm bảo lệnh in hiện tại hoàn tất trước khi tem kế tiếp chạy.
        if (!exited && hasRunningBarTender)
            exited = process.WaitForExit(90000);

        if (!exited)
            throw new Exception(
                "BarTender xử lý lệnh in quá thời gian chờ (30 giây).\n\n" +
                $"Template: {btwFile}\n" +
                $"Printer: {printerName}\n" +
                $"XML: {xmlPath}\n\n" +
                "Vui lòng kiểm tra BarTender có đang bị treo hoặc đang chờ hộp thoại xác nhận.");

        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0 || !string.IsNullOrWhiteSpace(stdErr))
        {
            string xmlContent = SafeReadAllText(xmlPath);

            throw new Exception(
                "BarTender báo lỗi khi xử lý XML in.\n\n" +
                $"Template: {btwFile}\n" +
                $"Printer: {printerName}\n" +
                $"XML: {xmlPath}\n" +
                $"ExitCode: {process.ExitCode}\n\n" +
                $"STDERR:\n{stdErr}\n\n" +
                $"STDOUT:\n{stdOut}\n\n" +
                $"Nội dung XML:\n{xmlContent}");
        }

        // process.WaitForExit ở trên chỉ đảm bảo tiến trình HAND-OFF lệnh
        // /XMLScript đã thoát — khi BarTender đã mở sẵn, tiến trình đó chỉ
        // chuyển lệnh cho instance đang chạy rồi thoát gần như ngay lập
        // tức, KHÔNG chờ BarTender in xong thật sự. Nếu code tiếp tục ghi
        // đè file data / gửi lệnh in kế tiếp trong lúc BarTender vẫn còn
        // đang tạo/spool số lượng lớn nhãn của lệnh này, dữ liệu dùng chung
        // (đặc biệt là tem kính: 1 dòng data ghi đè liên tục mỗi mã) sẽ bị
        // ghi đè giữa chừng → thiếu tem hoặc lẫn mã. Chờ hàng đợi máy in
        // thật sự rỗng trở lại trước khi trả quyền điều khiển, để lệnh in
        // kế tiếp (nếu có, ví dụ vòng lặp in nhiều mã tem kính) chỉ bắt đầu
        // sau khi lệnh này đã spool xong hoàn toàn.
        string confirmStatus = WaitForPrintJobToFinish(printerName);

        // Đệm ngắn giữa 2 lệnh in giúp tránh va chạm dữ liệu file data
        // với template đang xử lý ngay sau khi job trước vừa kết thúc.
        Thread.Sleep(150);

        // Log chẩn đoán: giúp truy vết những lần "báo thành công nhưng
        // không có tem ra" — xem services/PrintDiagnosticsLog.cs.
        PrintDiagnosticsLog.Write(
            $"OK template={btwFile} printer={printerName} " +
            $"hasRunningBarTender={hasRunningBarTender} confirmStatus={confirmStatus} " +
            $"elapsedMs={printStopwatch.ElapsedMilliseconds}");
        }
        catch (Exception ex)
        {
            PrintDiagnosticsLog.Write(
                $"FAIL template={btwFile} elapsedMs={printStopwatch.ElapsedMilliseconds} error={ex.Message}");
            throw;
        }
        finally
        {
            PrintGate.Release();
        }
    }

    //---------------------------------------------------------
    // Chờ máy in xử lý xong job (dựa vào hàng đợi Windows Spooler)
    //---------------------------------------------------------

    // Giá trị trả về chỉ dùng để ghi log chẩn đoán — không làm thay đổi
    // luồng in hiện tại (xem PrintDiagnosticsLog).
    private static string WaitForPrintJobToFinish(
        string printerName,
        int startupGraceMs = 8000,
        int maxWaitMs = 600000,
        int pollIntervalMs = 200)
    {
        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            return "no-printer-handle"; // Không lấy được handle máy in — bỏ qua, không chặn luồng in

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            bool sawJob = false;

            // 1) Chờ job thực sự xuất hiện trong hàng đợi (BarTender vừa
            // nhận lệnh và bắt đầu tạo job). Nếu không thấy trong khoảng
            // grace này thì có thể job đã in xong quá nhanh (số lượng nhỏ)
            // — bỏ qua bước chờ rỗng để không trễ vô ích.
            while (sw.ElapsedMilliseconds < startupGraceMs)
            {
                if (GetQueuedJobCount(hPrinter) > 0)
                {
                    sawJob = true;
                    break;
                }

                Thread.Sleep(pollIntervalMs);
            }

            if (!sawJob)
                return "no-job-seen"; // đáng ngờ nếu lặp lại nhiều lần: có thể lệnh in đã bị "nuốt"

            // 2) Chờ hàng đợi rỗng trở lại = máy in đã nhận xong toàn bộ
            // số lượng nhãn của job này.
            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                if (GetQueuedJobCount(hPrinter) == 0)
                    return "confirmed-empty";

                Thread.Sleep(pollIntervalMs);
            }

            return "timeout-still-queued";
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    private static int GetQueuedJobCount(IntPtr hPrinter)
    {
        GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out uint neededBytes);

        if (neededBytes == 0)
            return 0;

        IntPtr buffer = Marshal.AllocHGlobal((int)neededBytes);

        try
        {
            if (!GetPrinter(hPrinter, 2, buffer, neededBytes, out _))
                return 0;

            PRINTER_INFO_2 info = Marshal.PtrToStructure<PRINTER_INFO_2>(buffer);

            return (int)info.cJobs;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_INFO_2
    {
        public string? pServerName;
        public string? pPrinterName;
        public string? pShareName;
        public string? pPortName;
        public string? pDriverName;
        public string? pComment;
        public string? pLocation;
        public IntPtr pDevMode;
        public string? pSepFile;
        public string? pPrintProcessor;
        public string? pDatatype;
        public string? pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, uint dwLevel, IntPtr pPrinter, uint cbBuf, out uint pcbNeeded);

    private static string CreatePrintXmlNearApp(
        string btwFile,
            Dictionary<string, string>? namedSubStrings,
            string printerName)
    {
        string appFolder = AppDomain.CurrentDomain.BaseDirectory;
        string debugFolder = Path.Combine(appFolder, "debug_xml");
        Directory.CreateDirectory(debugFolder);

        string xmlPath = Path.Combine(
            debugFolder,
            $"print_{DateTime.Now:yyyyMMdd_HHmmss_fff}.xml");

        StringBuilder sb = new();

        sb.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        sb.AppendLine("""<XMLScript Version="2.0">""");
        sb.AppendLine("""  <Command>""");
        sb.AppendLine("""    <Print>""");
        sb.AppendLine($"      <Format>{EscapeXml(btwFile)}</Format>");
        sb.AppendLine("""      <PrintSetup>""");
        sb.AppendLine($"        <Printer>{EscapeXml(printerName)}</Printer>");
        sb.AppendLine("""        <EnablePrompting>false</EnablePrompting>""");
        sb.AppendLine("""      </PrintSetup>""");

        if (namedSubStrings != null && namedSubStrings.Count > 0)
        {
            foreach (var kv in namedSubStrings)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                string value = kv.Value ?? string.Empty;

                sb.AppendLine($"      <NamedSubString Name=\"{EscapeXml(kv.Key)}\">");
                sb.AppendLine($"        <Value>{EscapeXml(value)}</Value>");
                sb.AppendLine("      </NamedSubString>");
            }
        }

        sb.AppendLine("""    </Print>""");
        sb.AppendLine("""  </Command>""");
        sb.AppendLine("""</XMLScript>""");

        string xml = sb.ToString();

        using (FileStream fs = new(xmlPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (StreamWriter writer = new(fs, new UTF8Encoding(false)))
        {
            writer.Write(xml);
            writer.Flush();
            fs.Flush(true);
        }

        File.WriteAllText(Path.Combine(debugFolder, "last_print_debug.xml"), xml, new UTF8Encoding(false));

        return xmlPath;
    }

    private static string GetDefaultPrinterName()
    {
        PrinterSettings settings = new();

        if (string.IsNullOrWhiteSpace(settings.PrinterName))
            throw new Exception("Không xác định được máy in mặc định của Windows.");

        if (!settings.IsValid)
            throw new Exception($"Máy in mặc định không hợp lệ: {settings.PrinterName}");

        return settings.PrinterName;
    }

    private static string EscapeXml(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? value;
    }

    private static string SafeReadAllText(string path)
    {
        try
        {
            if (!File.Exists(path))
                return "(Không tìm thấy file XML)";

            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"(Không đọc được file XML: {ex.Message})";
        }
    }
}