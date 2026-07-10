using System.Diagnostics;
using System.Text;

namespace KiotVietLabelPrinter.Services;

public class BarTenderService
{
    public void Print(string btwFile)
    {
        Print(btwFile, null);
    }

    /// <summary>
    /// In file btw, có thể truyền thêm NamedSubStrings cho BarTender.
    /// Ví dụ:
    /// GLASSES_INFO = "...";
    /// </summary>
    public void Print(string btwFile, Dictionary<string, string>? namedSubStrings)
    {
        string bartenderExe = ConfigService.Instance.Config.BarTenderExe;

        if (string.IsNullOrWhiteSpace(bartenderExe))
            throw new Exception("Chưa cấu hình đường dẫn BarTender.exe.");

        if (!File.Exists(bartenderExe))
            throw new Exception($"Không tìm thấy BarTender.exe:\n{bartenderExe}");

        if (string.IsNullOrWhiteSpace(btwFile) || !File.Exists(btwFile))
            throw new Exception($"Không tìm thấy file tem:\n{btwFile}");

        string xmlPath = CreatePrintXml(btwFile, namedSubStrings);

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = bartenderExe,
                Arguments = $"/XMLScript=\"{xmlPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using Process? process = Process.Start(psi);

            if (process == null)
                throw new Exception("Không thể gửi lệnh in tới BarTender.");

            bool exited = process.WaitForExit(3000); // tối đa 3 giây

            if (!exited)
            {
                // coi như đã gửi lệnh in, không làm app bị treo/crash
                return;
            }

            string stdErr = process.StandardError.ReadToEnd();

            if (!string.IsNullOrWhiteSpace(stdErr))
            {
                throw new Exception("BarTender báo lỗi:\n" + stdErr.Trim());
            }
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    private static string CreatePrintXml(
        string btwFile,
        Dictionary<string, string>? namedSubStrings)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "KiotVietLabelPrinter");
        Directory.CreateDirectory(tempFolder);

        string xmlPath = Path.Combine(
            tempFolder,
            $"print_{DateTime.Now:yyyyMMdd_HHmmss_fff}.xml");

        StringBuilder sb = new();

        sb.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        sb.AppendLine("""<XMLScript Version="2.0">""");
        sb.AppendLine("""  <Command>""");
        sb.AppendLine("""    <Print>""");
        sb.AppendLine($"      <Format>{EscapeXml(btwFile)}</Format>");

        if (namedSubStrings != null && namedSubStrings.Count > 0)
        {
            sb.AppendLine("""      <NamedSubString>""");

            foreach (var kv in namedSubStrings)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                sb.AppendLine("""        <SubString>""");
                sb.AppendLine($"          <Name>{EscapeXml(kv.Key)}</Name>");
                sb.AppendLine($"          <Value>{EscapeXml(kv.Value ?? "")}</Value>");
                sb.AppendLine("""        </SubString>""");
            }

            sb.AppendLine("""      </NamedSubString>""");
        }

        sb.AppendLine("""    </Print>""");
        sb.AppendLine("""  </Command>""");
        sb.AppendLine("""</XMLScript>""");

        File.WriteAllText(xmlPath, sb.ToString(), Encoding.UTF8);
        return xmlPath;
    }

    private static string EscapeXml(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? value;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // bỏ qua lỗi xóa file tạm
        }
    }
}