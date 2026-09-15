using System.Reflection;

namespace KiotVietLabelPrinter.Services;

/// <summary>
/// Thông tin phiên bản / vị trí file thực thi — dùng để hiển thị trong app
/// và ghi log lúc khởi động, giúp xác nhận một máy đang chạy ĐÚNG build nào
/// (không phải một bản .exe cũ nằm ở Desktop / bin\Debug / publish cũ).
/// </summary>
public static class AppInfo
{
    private static readonly Assembly EntryAssembly =
        Assembly.GetEntryAssembly() ?? typeof(AppInfo).Assembly;

    /// <summary>Số phiên bản (từ &lt;Version&gt; trong .csproj), ví dụ "2.1.0".</summary>
    public static string Version { get; } = ResolveVersion();

    /// <summary>Thời điểm build (UTC) — nhúng vào assembly lúc compile.</summary>
    public static string BuildTimestampUtc { get; } = ResolveBuildTimestamp();

    /// <summary>Đường dẫn tuyệt đối tới file .exe đang chạy.</summary>
    public static string ExecutablePath =>
        Environment.ProcessPath ?? "(không xác định)";

    /// <summary>Thư mục gốc app đang nạp file (AppContext.BaseDirectory).</summary>
    public static string BaseDirectory => AppContext.BaseDirectory;

    /// <summary>Vị trí file assembly chính (.dll).</summary>
    public static string AssemblyLocation
    {
        get
        {
            try { return EntryAssembly.Location; }
            catch { return "(không xác định)"; }
        }
    }

    /// <summary>Nhãn ngắn để hiển thị trên UI, ví dụ "v2.1.0 · build 2026-09-10 08:15".</summary>
    public static string ShortLabel
    {
        get
        {
            string ts = BuildTimestampUtc;
            if (ts.Length >= 16)
                ts = ts.Substring(0, 16); // yyyy-MM-dd HH:mm
            return $"v{Version} · build {ts} UTC";
        }
    }

    /// <summary>Khối chẩn đoán đầy đủ — ghi vào logs/app-startup.log.</summary>
    public static string DiagnosticsBlock()
    {
        string[] lines =
        {
            $"App version      : {Version}",
            $"Build timestamp  : {BuildTimestampUtc} UTC",
            $"Executable       : {ExecutablePath}",
            $"BaseDirectory    : {BaseDirectory}",
            $"Assembly         : {AssemblyLocation}",
            $"Assembly version : {EntryAssembly.GetName().Version}",
            $"Machine / user   : {Environment.MachineName} / {Environment.UserName}",
            $"OS               : {Environment.OSVersion}",
            $".NET             : {Environment.Version}",
            $"Working dir      : {Environment.CurrentDirectory}",
        };

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Ghi khối chẩn đoán khởi động vào logs/app-startup.log (append). Gọi 1
    /// lần trong Program.Main sau khi đã nạp config.
    /// </summary>
    public static void WriteStartupLog(string extraContext)
    {
        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, "app-startup.log");

            string block =
                $"===== APP START {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====" + Environment.NewLine +
                DiagnosticsBlock() + Environment.NewLine +
                extraContext + Environment.NewLine +
                new string('-', 60) + Environment.NewLine;

            File.AppendAllText(path, block);
        }
        catch
        {
            // Ghi log khởi động là phụ trợ — không được chặn app mở lên.
        }
    }

    private static string ResolveVersion()
    {
        try
        {
            string? info = EntryAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(info))
            {
                // Bỏ hậu tố "+<git hash>" nếu SourceLink thêm vào.
                int plus = info.IndexOf('+');
                return plus > 0 ? info.Substring(0, plus) : info;
            }

            Version? v = EntryAssembly.GetName().Version;
            return v != null ? $"{v.Major}.{v.Minor}.{v.Build}" : "(không xác định)";
        }
        catch
        {
            return "(không xác định)";
        }
    }

    private static string ResolveBuildTimestamp()
    {
        // 1) Ưu tiên metadata nhúng lúc compile (chính xác nhất).
        try
        {
            foreach (AssemblyMetadataAttribute meta in
                     EntryAssembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (string.Equals(meta.Key, "BuildTimestampUtc", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(meta.Value))
                {
                    return meta.Value!;
                }
            }
        }
        catch
        {
            // rơi xuống fallback
        }

        // 2) Fallback: giờ sửa file .dll (xấp xỉ giờ build/publish).
        try
        {
            string location = EntryAssembly.Location;
            if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
                return File.GetLastWriteTimeUtc(location).ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch
        {
            // bỏ qua
        }

        return "(không xác định)";
    }
}
