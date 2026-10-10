namespace KiotVietLabelPrinter.Infrastructure.Diagnostics;

/// <summary>
/// Nhật ký của chức năng Phiếu xuất kho (logs/warehouse_yyyyMMdd.log): ghi
/// mỗi lần xuất/in và chi tiết kỹ thuật của lỗi — màn hình chỉ hiện thông báo
/// tiếng Việt, không hiện stack trace.
/// </summary>
public static class WarehouseLog
{
    private static readonly object WriteLock = new();

    public static void Info(string message) => Write("INFO ", message);

    public static void Error(string message, Exception ex) =>
        Write("ERROR", message + Environment.NewLine + ex);

    private static void Write(string level, string message)
    {
        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {level} {message}{Environment.NewLine}";

            lock (WriteLock)
            {
                File.AppendAllText(Path.Combine(folder, $"warehouse_{DateTime.Now:yyyyMMdd}.log"), line);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ghi log là phụ trợ: thư mục chỉ đọc / ổ đầy không được làm hỏng
            // thao tác xuất/in đang chạy.
            System.Diagnostics.Debug.WriteLine($"Không ghi được log phiếu xuất kho: {ex.Message}");
        }
    }
}
