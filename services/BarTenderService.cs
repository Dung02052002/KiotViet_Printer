using System.Diagnostics;
using System.Text;

namespace KiotVietLabelPrinter.Services;

public class BarTenderService
{
    public void Print(string btwFile)
    {
        string bartenderExe = ConfigService.Instance.Config.BarTenderExe;

        if (string.IsNullOrWhiteSpace(bartenderExe))
            throw new Exception("Chưa cấu hình đường dẫn BarTender.exe.");

        if (!File.Exists(bartenderExe))
            throw new Exception($"Không tìm thấy BarTender.exe:\n{bartenderExe}");

        if (string.IsNullOrWhiteSpace(btwFile) || !File.Exists(btwFile))
            throw new Exception($"Không tìm thấy file tem:\n{btwFile}");

        string xmlPath = CreatePrintXml(btwFile);

        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = bartenderExe,
                Arguments = $"/XMLScript=\"{xmlPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using Process? process = Process.Start(psi);

            if (process == null)
                throw new Exception("Không thể gửi lệnh in tới BarTender.");

            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"BarTender trả về mã lỗi: {process.ExitCode}");
        }
        finally
        {
            TryDelete(xmlPath);
        }
    }

    private static string CreatePrintXml(string btwFile)
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "KiotVietLabelPrinter");
        Directory.CreateDirectory(tempFolder);

        string xmlPath = Path.Combine(
            tempFolder,
            $"print_{DateTime.Now:yyyyMMdd_HHmmss_fff}.xml");

        string xml = $"""
<?xml version="1.0" encoding="utf-8"?>
<XMLScript Version="2.0">
  <Command>
    <Print>
      <Format>{EscapeXml(btwFile)}</Format>
      <PrintSetup>
        <IdenticalCopiesOfLabel>1</IdenticalCopiesOfLabel>
      </PrintSetup>
    </Print>
  </Command>
</XMLScript>
""";

        File.WriteAllText(xmlPath, xml, Encoding.UTF8);
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