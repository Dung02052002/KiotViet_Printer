using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.Services.BackgroundRemoval;

namespace KiotVietLabelPrinter;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    private const int AttachParentProcess = -1;

    [STAThread]
    static int Main(string[] args)
    {
        // Chế độ CLI test/benchmark cho tính năng xóa nền — không mở UI, không
        // đụng single-instance mutex (để chạy song song lúc GUI đang mở).
        if (BgRemovalCli.ShouldRun(args))
        {
            AttachConsole(AttachParentProcess);
            return BgRemovalCli.Run(args);
        }

        // Chế độ CLI kiểm tra backend BarTender: in ra CHÍNH XÁC dòng lệnh sẽ
        // gửi cho BarTender cho từng loại tem — KHÔNG thực thi, KHÔNG mở UI.
        // Dùng để xác nhận "Tem đầy đủ" không còn chạm /XMLScript.
        if (BarTenderSelfTest.ShouldRun(args))
        {
            AttachConsole(AttachParentProcess);
            ConfigService.Instance.Load();
            return BarTenderSelfTest.Run();
        }

        // Ten mutex phai khop voi AppMutex trong installer.iss de Inno Setup
        // co the phat hien va dong app dang chay truoc khi cai/ghi de.
        using var mutex = new Mutex(true, "KiotVietLabelPrinterProV2Mutex", out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show("Ung dung dang chay roi.", "In Tem KiotViet",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        ApplicationConfiguration.Initialize();

        ConfigService.Instance.Load();

        // Ghi log khởi động: version + đường dẫn .exe thật + BaseDirectory —
        // để xác nhận một máy đang chạy ĐÚNG build (không phải bản .exe cũ ở
        // Desktop / bin cũ / publish cũ). Xem logs/app-startup.log.
        AppInfo.WriteStartupLog(
            $"BarTenderExe     : {ConfigService.Instance.Config.BarTenderExe}" + Environment.NewLine +
            $"Configured print : {ConfigService.Instance.Config.PrinterName}" + Environment.NewLine +
            $"XmlScript cache  : {BarTenderCapabilityService.Instance.XmlScriptSupported?.ToString() ?? "chưa biết"}");

        Application.Run(new FormMain());
        return 0;
    }
}
