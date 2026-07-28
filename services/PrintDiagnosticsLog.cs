namespace KiotVietLabelPrinter.Services;

// Ghi lại kết quả mỗi lần gọi BarTenderService.Print để chẩn đoán các lần
// "báo in thành công nhưng không có tem ra" (BarTender bị kẹt hộp thoại,
// hand-off không được xử lý...). Chỉ ghi log, không làm thay đổi hành vi in.
public static class PrintDiagnosticsLog
{
    private static readonly object WriteLock = new();

    public static void Write(string message)
    {
        try
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "print_logs");
            Directory.CreateDirectory(folder);

            string filePath = Path.Combine(folder, $"print_{DateTime.Now:yyyyMMdd}.log");
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";

            lock (WriteLock)
            {
                File.AppendAllText(filePath, line);
            }
        }
        catch
        {
            // Ghi log là phụ trợ — không được làm gián đoạn luồng in nếu ghi lỗi.
        }
    }
}
