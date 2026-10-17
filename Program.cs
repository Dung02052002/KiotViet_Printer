using System.Diagnostics;
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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SwRestore = 9;

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

        StartupLog.Begin();
        StartupLog.Stage("Main");

        // Bộ cài (installer.iss) không dựa vào mutex này: nó tìm tiến trình
        // theo tên .exe rồi tắt hẳn trước khi cài — kể cả tiến trình bị treo.
        using var mutex = new Mutex(true, "KiotVietLabelPrinterProV2Mutex", out bool isNewInstance);

        ApplicationConfiguration.Initialize();

        if (!isNewInstance)
        {
            HandleSecondInstance();
            return 0;
        }

        StartupLog.Stage("Đã giữ single-instance mutex");
        InstallGlobalErrorHandlers();

        // Chỉ ghi log, không can thiệp: nếu sau 30 giây cửa sổ chính vẫn chưa
        // hiện, log ghi lại giai đoạn đang kẹt (nạp cấu hình, dựng FormMain...).
        StartupLog.WatchForStall(TimeSpan.FromSeconds(30));

        // Lỗi trước khi message loop chạy (đọc cấu hình, dựng FormMain) không
        // đi qua Application.ThreadException: trước đây nó làm tiến trình sập
        // qua Windows Error Reporting mà không có thông báo dễ hiểu nào. Giờ
        // ghi log + báo lỗi + thoát hẳn, không để tiến trình chạy nền vô ích.
        try
        {
            RunApplication();
            return 0;
        }
        catch (Exception ex)
        {
            LogUnhandled($"STARTUP (giai đoạn: {StartupLog.LastStage})", ex);
            MessageBox.Show(
                "Không khởi động được phần mềm.\n\n" +
                $"Chi tiết: {ex.Message}\n\n" +
                $"Nhật ký khởi động: {StartupLog.FilePath ?? "(không ghi được)"}",
                "In Tem KiotViet", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void RunApplication()
    {
        StartupLog.Stage("Nạp cấu hình");
        ConfigService.Instance.Load();

        // Ghi log khởi động: version + đường dẫn .exe thật + BaseDirectory —
        // để xác nhận một máy đang chạy ĐÚNG build (không phải bản .exe cũ ở
        // Desktop / bin cũ / publish cũ). Xem logs/app-startup.log.
        AppInfo.WriteStartupLog(
            $"BarTenderExe     : {ConfigService.Instance.Config.BarTenderExe}" + Environment.NewLine +
            $"Configured print : {ConfigService.Instance.Config.PrinterName}" + Environment.NewLine +
            $"XmlScript cache  : {BarTenderCapabilityService.Instance.XmlScriptSupported?.ToString() ?? "chưa biết"}");

        // Không mở hộp thoại nào trước Application.Run: cảnh báo cấu hình
        // (ConfigService.LoadWarning) và việc hỏi cấu hình lần đầu do FormMain
        // hiển thị SAU khi cửa sổ chính đã hiện — xem FormMain.OnShown.
        StartupLog.Stage("Tạo FormMain");
        using FormMain form = new();

        StartupLog.Stage("Application.Run");
        Application.Run(form);
        StartupLog.Stage("Message loop kết thúc");
    }

    /// <summary>
    /// Một instance khác đang giữ mutex. Trước đây chỉ báo "Ung dung dang chay
    /// roi" rồi thoát — nếu instance cũ đang kẹt KHÔNG có cửa sổ (nằm trong
    /// Background processes) thì người dùng không mở được app và không biết
    /// phải tắt tiến trình nào. Giờ: có cửa sổ thì đưa nó lên trước; không có
    /// thì báo rõ PID để người dùng tự kết thúc. Không bao giờ tự kill tiến
    /// trình khác.
    /// </summary>
    private static void HandleSecondInstance()
    {
        StartupLog.Stage("Mutex đã bị instance khác giữ");

        Process[] others = FindOtherInstances();
        try
        {
            foreach (Process other in others)
            {
                if (TryActivateMainWindow(other))
                {
                    StartupLog.Stage($"Đã đưa cửa sổ của instance PID={other.Id} lên trước — thoát");
                    return;
                }
            }

            StartupLog.Stage("Instance đang chạy không có cửa sổ: " + DescribeProcesses(others));

            string details = others.Length == 0
                ? "Phần mềm đang chạy hoặc đang đóng. Vui lòng đợi vài giây rồi mở lại."
                : $"Phần mềm đã đang chạy (PID {string.Join(", ", others.Select(p => p.Id))}) nhưng không có cửa sổ " +
                  "nào hiển thị — có thể tiến trình đó đang bị treo.\n\n" +
                  "Mở Task Manager → tab Details, tìm đúng PID ở trên, chọn End task rồi mở lại phần mềm.";

            MessageBox.Show(
                details + $"\n\nNhật ký khởi động: {StartupLog.FilePath ?? "(không ghi được)"}",
                "In Tem KiotViet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally
        {
            foreach (Process other in others)
                other.Dispose();
        }
    }

    // Lọc theo tên tiến trình (= tên .exe), KHÔNG lọc theo đường dẫn: một bản
    // chép ở thư mục khác vẫn giữ CÙNG tên mutex nên cũng chặn instance mới —
    // phải liệt kê được nó thì người dùng mới biết tắt tiến trình nào.
    private static Process[] FindOtherInstances()
    {
        using Process current = Process.GetCurrentProcess();
        List<Process> others = new();

        foreach (Process process in Process.GetProcessesByName(current.ProcessName))
        {
            if (process.Id == current.Id)
                process.Dispose();
            else
                others.Add(process);
        }

        return others.ToArray();
    }

    private static bool TryActivateMainWindow(Process process)
    {
        try
        {
            IntPtr hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero)
                return false;

            if (IsIconic(hwnd))
                ShowWindow(hwnd, SwRestore);

            SetForegroundWindow(hwnd);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Tiến trình vừa thoát / thuộc phiên người dùng khác — coi như không có cửa sổ.
            return false;
        }
    }

    private static string DescribeProcesses(IEnumerable<Process> processes) =>
        string.Join("; ", processes.Select(p =>
        {
            try
            {
                return $"PID={p.Id} start={p.StartTime:yyyy-MM-dd HH:mm:ss} responding={p.Responding} path={p.MainModule?.FileName}";
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                return $"PID={p.Id} (không đọc được chi tiết: {ex.Message})";
            }
        }));

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
        // Ghi cả vào log khởi động ở %LOCALAPPDATA%: logs\ cạnh .exe có thể
        // không ghi được (app nằm trong Program Files / thư mục bị khoá).
        StartupLog.Error($"UNHANDLED {source}", ex);

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
