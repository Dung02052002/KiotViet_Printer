namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Ghi lại CHÍNH XÁC file thực thi + tham số dòng lệnh gửi cho BarTender,
/// TRƯỚC mỗi lần Process.Start. Mục đích: khi còn nghi ngờ "vẫn còn code path
/// gọi /XMLScript", mở thẳng file này để thấy dòng lệnh cuối cùng thật sự là gì.
///
/// File: &lt;thư mục app&gt;\logs\bartender-command.log
/// </summary>
public static class BarTenderCommandLog
{
    private static readonly object WriteLock = new();

    public static string LogFilePath =>
        Path.Combine(AppContext.BaseDirectory, "logs", "bartender-command.log");

    public static void Write(string message)
    {
        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";

            lock (WriteLock)
            {
                File.AppendAllText(LogFilePath, line);
            }
        }
        catch
        {
            // Ghi log là phụ trợ — không được làm gián đoạn luồng in.
        }
    }

    /// <summary>
    /// Ghi block chuẩn ngay trước khi chạy BarTender. Format cố định để dễ
    /// tìm bằng mắt / grep:
    ///
    ///   BARTENDER EXECUTABLE: ...
    ///   BARTENDER ARGUMENTS : ...
    /// </summary>
    public static void WriteCommandBlock(
        string backendName,
        string executable,
        string arguments,
        string templatePath,
        string printerName,
        string namedSubStrings)
    {
        string block =
            $"---- PRINT via {backendName} ----" + Environment.NewLine +
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] BARTENDER EXECUTABLE: {executable}" + Environment.NewLine +
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] BARTENDER ARGUMENTS : {arguments}" + Environment.NewLine +
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] LABEL TEMPLATE      : {templatePath}" + Environment.NewLine +
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] PRINTER            : {printerName}" + Environment.NewLine +
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] NAMED SUBSTRINGS   : {namedSubStrings}" + Environment.NewLine;

        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);

            lock (WriteLock)
            {
                File.AppendAllText(LogFilePath, block);
            }
        }
        catch
        {
            // bỏ qua
        }
    }
}
