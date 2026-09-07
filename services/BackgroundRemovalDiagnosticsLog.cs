namespace KiotVietLabelPrinter.Services;

// Log riêng cho subsystem xóa nền ảnh — quan trọng nhất là ghi rõ ONNX
// Execution Provider nào THỰC SỰ chạy (DirectML hay fallback CPU), vì
// AppendExecutionProvider_DML không throw không có nghĩa là mọi node trong
// graph thực sự chạy trên GPU — xem BackgroundRemovalService.VerifyActiveProvider.
public static class BackgroundRemovalDiagnosticsLog
{
    private static readonly object WriteLock = new();

    public static string LogFolder => Path.Combine(AppContext.BaseDirectory, "bg_removal_logs");

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);

            string filePath = Path.Combine(LogFolder, $"bg_removal_{DateTime.Now:yyyyMMdd}.log");
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";

            lock (WriteLock)
            {
                File.AppendAllText(filePath, line);
            }
        }
        catch
        {
            // Ghi log là phụ trợ — không được làm gián đoạn xử lý nếu ghi lỗi.
        }
    }
}
