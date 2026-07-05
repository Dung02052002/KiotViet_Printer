using System.Diagnostics;

namespace KiotVietLabelPrinter.Services;

public class BarTenderService
{
    public void Print(string btwFile)
    {
        string bartenderExe = ConfigService.Instance.Config.BarTenderExe;

        if (string.IsNullOrWhiteSpace(bartenderExe))
            throw new Exception("Chưa cấu hình đường dẫn BarTender.exe.");

        if (!File.Exists(bartenderExe))
            throw new Exception("Không tìm thấy BarTender.exe trong cấu hình.");

        if (!File.Exists(btwFile))
            throw new Exception($"Không tìm thấy file tem: {btwFile}");

        Process.Start(new ProcessStartInfo
        {
            FileName = bartenderExe,
            Arguments = $"/F=\"{btwFile}\" /P",
            UseShellExecute = true
        });
    }
}