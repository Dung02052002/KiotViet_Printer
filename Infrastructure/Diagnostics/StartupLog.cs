using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KiotVietLabelPrinter.Infrastructure.Diagnostics;

/// <summary>
/// Nhật ký vòng đời tiến trình theo từng giai đoạn (Main → mutex → nạp cấu
/// hình → tạo FormMain → Shown → đóng → thoát), để khi một máy báo "mở app
/// không thấy cửa sổ, tiến trình nằm trong Background processes" có thể biết
/// chính xác tiến trình (PID nào) dừng ở giai đoạn nào.
///
/// Ghi vào %LOCALAPPDATA%\KiotVietLabelPrinter\logs — luôn ghi được với quyền
/// người dùng thường, kể cả khi app bị chép vào Program Files hoặc thư mục cài
/// đặt (Desktop / OneDrive) đang bị khoá. Mọi lỗi ghi log đều bị nuốt: log
/// không bao giờ được làm app treo hay tắt thêm.
/// </summary>
public static class StartupLog
{
    private const string FileName = "startup.log";
    private const long MaxBytes = 1024 * 1024;

    private static readonly object WriteLock = new();
    private static readonly Stopwatch SinceStart = Stopwatch.StartNew();
    private static readonly int ProcessId = Environment.ProcessId;

    private static string? _filePath;
    private static string _lastStage = "(chưa bắt đầu)";
    private static System.Threading.Timer? _stallTimer;

    /// <summary>Đường dẫn file log đang dùng (null = không thư mục nào ghi được).</summary>
    public static string? FilePath => _filePath;

    /// <summary>Giai đoạn cuối cùng đã ghi — dùng khi báo lỗi cho người dùng.</summary>
    public static string LastStage => Volatile.Read(ref _lastStage);

    /// <summary>Gọi 1 lần ở đầu Main: chọn thư mục log và ghi khối thông tin tiến trình.</summary>
    public static void Begin()
    {
        _filePath = ResolveLogFile(CandidateFolders());
        if (_filePath != null)
            FileRetention.RotateIfLarger(_filePath, MaxBytes);

        // ProcessExit chạy cả khi Main return lẫn khi Application.Exit — dòng
        // này thiếu trong log nghĩa là tiến trình bị kill hoặc vẫn còn sống.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stage("Tiến trình kết thúc");

        Append(
            $"===== START PID={ProcessId} {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} =====" + Environment.NewLine +
            $"App version      : {AppInfo.Version} (build {AppInfo.BuildTimestampUtc} UTC)" + Environment.NewLine +
            $"Executable       : {AppInfo.ExecutablePath}" + Environment.NewLine +
            $"BaseDirectory    : {AppInfo.BaseDirectory}" + Environment.NewLine +
            $"Working dir      : {Environment.CurrentDirectory}" + Environment.NewLine +
            $".NET             : {RuntimeInformation.FrameworkDescription}" + Environment.NewLine +
            $"Kiến trúc        : process {RuntimeInformation.ProcessArchitecture} / OS {RuntimeInformation.OSArchitecture}" + Environment.NewLine +
            $"OS               : {Environment.OSVersion}" + Environment.NewLine +
            $"Windows session  : {Process.GetCurrentProcess().SessionId}" + Environment.NewLine);
    }

    /// <summary>Ghi một mốc giai đoạn, kèm số ms tính từ lúc tiến trình bắt đầu.</summary>
    public static void Stage(string stage)
    {
        Volatile.Write(ref _lastStage, stage);
        Append(FormatLine($"STAGE {stage}"));
    }

    public static void Error(string stage, Exception? ex)
    {
        Append(FormatLine($"ERROR tại \"{stage}\"") + Environment.NewLine +
               (ex?.ToString() ?? "(không có thông tin exception)") + Environment.NewLine);
    }

    /// <summary>
    /// Hẹn giờ MỘT lần: nếu sau <paramref name="after"/> cửa sổ chính vẫn chưa
    /// hiển thị (<see cref="MarkUiReady"/> chưa được gọi) thì ghi lại giai đoạn
    /// đang kẹt. Chỉ ghi log — không kill, không restart, không đoán lỗi.
    /// </summary>
    public static void WatchForStall(TimeSpan after)
    {
        _stallTimer = new System.Threading.Timer(
            _ => Append(FormatLine($"STALL sau {after.TotalSeconds:0}s cửa sổ chính vẫn chưa hiển thị — đang kẹt ở giai đoạn: {LastStage}")),
            null, after, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Cửa sổ chính đã hiển thị: ghi mốc và huỷ hẹn giờ phát hiện kẹt.</summary>
    public static void MarkUiReady()
    {
        Interlocked.Exchange(ref _stallTimer, null)?.Dispose();
        Stage("Cửa sổ chính đã hiển thị (Shown)");
    }

    // Thứ tự ưu tiên: LocalAppData (chuẩn, ghi được) → thư mục cạnh .exe (bản
    // cũ vẫn ghi ở đây) → %TEMP% (phương án cuối).
    private static IEnumerable<string> CandidateFolders()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
            yield return Path.Combine(localAppData, "KiotVietLabelPrinter", "logs");

        yield return Path.Combine(AppContext.BaseDirectory, "logs");
        yield return Path.Combine(Path.GetTempPath(), "KiotVietLabelPrinter", "logs");
    }

    /// <summary>Thư mục đầu tiên tạo + ghi thử được; null nếu không thư mục nào ghi được.</summary>
    internal static string? ResolveLogFile(IEnumerable<string> folders)
    {
        foreach (string folder in folders)
        {
            try
            {
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, FileName);
                // Ghi thử 0 byte: thư mục tồn tại nhưng chỉ đọc thì phải bỏ qua ngay
                // ở đây, không phải tới lần ghi đầu tiên mới phát hiện.
                File.AppendAllText(path, string.Empty);
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                Debug.WriteLine($"Không ghi được log khởi động vào {folder}: {ex.Message}");
            }
        }

        return null;
    }

    private static string FormatLine(string message) =>
        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] PID={ProcessId} +{SinceStart.ElapsedMilliseconds}ms T{Environment.CurrentManagedThreadId} {message}";

    private static void Append(string text)
    {
        string? path = _filePath;
        if (path == null)
            return;

        try
        {
            lock (WriteLock)
                File.AppendAllText(path, text + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Không ghi được log khởi động: {ex.Message}");
        }
    }
}
