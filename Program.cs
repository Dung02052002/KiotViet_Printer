using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.Storage;
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
        InstallGlobalErrorHandlers();

        ConfigService.Instance.Load();

        if (ConfigService.Instance.LoadWarning is string configWarning)
        {
            MessageBox.Show(configWarning, "Cấu hình",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ConfigService.Instance.ClearLoadWarning();
        }

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

    // Lưới an toàn cuối cùng: lỗi không lường trước ở bất kỳ event handler /
    // async void nào được ghi log và báo bằng thông báo dễ hiểu — thay vì hộp
    // thoại lỗi .NET thô (UI thread) hoặc app tự tắt không để lại dấu vết
    // (luồng nền). Các luồng nghiệp vụ vẫn tự bắt lỗi như cũ; đây chỉ là
    // chỗ hứng những gì lọt qua.
    private static void InstallGlobalErrorHandlers()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) =>
        {
            LogUnhandled("UI", e.Exception);
            MessageBox.Show(
                "Đã xảy ra lỗi không mong muốn, thao tác vừa rồi chưa hoàn tất.\n" +
                "Dữ liệu đang nhập vẫn còn — bạn có thể thử lại.\n\n" +
                $"Chi tiết: {e.Exception.Message}\n\n" +
                "(Chi tiết kỹ thuật đã được ghi vào thư mục logs.)",
                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        // Lỗi ở luồng nền: .NET bắt buộc tắt tiến trình, chỉ kịp ghi log.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogUnhandled(e.IsTerminating ? "BACKGROUND (fatal)" : "BACKGROUND", e.ExceptionObject as Exception);

        // Task bị lỗi mà không ai await — ghi log, không làm sập app.
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogUnhandled("TASK", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogUnhandled(string source, Exception? ex)
    {
        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);
            File.AppendAllText(
                Path.Combine(folder, $"app-errors_{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] UNHANDLED {source} ({AppInfo.Version})" + Environment.NewLine +
                (ex?.ToString() ?? "(không có thông tin exception)") + Environment.NewLine + Environment.NewLine);
        }
        catch (Exception logEx) when (logEx is IOException or UnauthorizedAccessException)
        {
            // Không ghi được log (thư mục chỉ đọc / ổ đầy): đang ở trong bộ xử
            // lý lỗi nên không còn chỗ nào khác để báo — bỏ qua để không ném
            // thêm lỗi chồng lên lỗi gốc.
        }
    }
}
