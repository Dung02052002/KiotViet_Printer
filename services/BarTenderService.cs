using System.Diagnostics;
using System.Drawing.Printing;
using System.Text;
using System.Windows.Forms;

namespace KiotVietLabelPrinter.Services;

public class BarTenderService
{
    public void Print(string btwFile)
    {
        Print(btwFile, null);
    }

    public void Print(string btwFile, Dictionary<string, string>? namedSubStrings)
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

        if (!exited && hasRunningBarTender)
        {
            // Chế độ handoff: có instance BarTender đang chạy nhận lệnh in.
            // Tiến trình gọi lệnh có thể không thoát ngay, nhưng lệnh in vẫn
            // đã được gửi, nên không fail giả.
            return;
        }

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
    }

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