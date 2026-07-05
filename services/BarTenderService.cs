using System.Diagnostics;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class BarTenderService
{
    public void Print(string btwFile)
    {
        var config = ConfigService.Instance.Config;

        if (string.IsNullOrWhiteSpace(config.BarTenderExe))
            throw new Exception("Chưa cấu hình đường dẫn BarTender.");

        if (!File.Exists(config.BarTenderExe))
            throw new Exception("Không tìm thấy bartend.exe.");

        if (!File.Exists(btwFile))
            throw new Exception("Không tìm thấy file tem:\n" + btwFile);

        Process.Start(new ProcessStartInfo
        {
            FileName = config.BarTenderExe,
            Arguments = $"/F=\"{btwFile}\" /P /X",
            UseShellExecute = true
        });
    }
}