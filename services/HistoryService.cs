using System.Text.Json;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class HistoryService
{
    // Dùng chung cho mọi instance (FormMain, FormPreview, FormHistory đều tự
    // new HistoryService) — tránh 2 lần ghi chồng lên cùng một file.
    private static readonly object FileLock = new();

    private readonly string _historyFile;

    public HistoryService()
    {
        _historyFile = Path.Combine(Application.StartupPath, "Data", "history.json");

        // Không được làm hỏng việc mở app chỉ vì không tạo được file lịch sử
        // (Add() sẽ tự tạo lại khi ghi).
        try
        {
            string? folder = Path.GetDirectoryName(_historyFile);
            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            if (!File.Exists(_historyFile))
            {
                File.WriteAllText(_historyFile, "[]");
            }
        }
        catch
        {
            // bỏ qua
        }
    }

    /// <summary>
    /// Đọc lịch sử để HIỂN THỊ. File hỏng/không đọc được → danh sách rỗng
    /// (không ném lỗi), nhưng KHÔNG dùng kết quả này để ghi đè file — xem Add().
    /// </summary>
    public List<PrintHistory> GetAll()
    {
        try
        {
            lock (FileLock)
            {
                return ReadStrict() ?? new List<PrintHistory>();
            }
        }
        catch
        {
            return new List<PrintHistory>();
        }
    }

    public void Add(PrintHistory item)
    {
        lock (FileLock)
        {
            List<PrintHistory> items;

            try
            {
                items = ReadStrict() ?? new List<PrintHistory>();
            }
            catch (JsonException)
            {
                // File lịch sử hỏng: giữ lại bản cũ (có thể khôi phục bằng
                // tay) rồi mới bắt đầu file mới — tuyệt đối không ghi đè mất.
                // Nếu không đổi tên được thì File.Move ném lỗi ra ngoài, lịch
                // sử cũ vẫn nguyên.
                File.Move(_historyFile, $"{_historyFile}.bad-{DateTime.Now:yyyyMMdd_HHmmss}");
                items = new List<PrintHistory>();
            }

            // IOException (file đang bị khoá...) được ném ra cho nơi gọi — KHÔNG
            // được coi như "lịch sử rỗng" rồi ghi đè.

            items.Add(item);
            WriteAtomic(items);
        }
    }

    public void SaveAll(List<PrintHistory> items)
    {
        lock (FileLock)
        {
            WriteAtomic(items);
        }
    }

    /// <summary>
    /// null = chưa có file / file rỗng. Ném JsonException khi file hỏng, ném
    /// IOException khi không đọc được.
    /// </summary>
    private List<PrintHistory>? ReadStrict()
    {
        if (!File.Exists(_historyFile))
            return null;

        string json = File.ReadAllText(_historyFile);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<List<PrintHistory>>(json);
    }

    // Ghi ra file tạm rồi thay thế: mất điện / tắt app giữa lúc ghi không để
    // lại file lịch sử bị cụt.
    private void WriteAtomic(List<PrintHistory> items)
    {
        string? folder = Path.GetDirectoryName(_historyFile);
        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string json = JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        string tempPath = _historyFile + ".tmp";

        using (FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        using (StreamWriter writer = new(fs, new System.Text.UTF8Encoding(false)))
        {
            writer.Write(json);
            writer.Flush();
            fs.Flush(true);
        }

        if (File.Exists(_historyFile))
            File.Replace(tempPath, _historyFile, null, ignoreMetadataErrors: true);
        else
            File.Move(tempPath, _historyFile);
    }
}
