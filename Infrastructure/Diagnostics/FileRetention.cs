namespace KiotVietLabelPrinter.Infrastructure.Diagnostics;

/// <summary>
/// Dọn file chẩn đoán / file tạm do chính app sinh ra (XML lệnh in, PDF xem
/// trước, log một file) để thư mục cài đặt và thư mục tạm không phình mãi.
/// Mọi thao tác là best-effort: không xoá / đổi tên được thì để lại, không
/// bao giờ làm hỏng thao tác in / xem trước đang chạy.
/// </summary>
public static class FileRetention
{
    /// <summary>Xoá các file khớp <paramref name="pattern"/> sửa lần cuối cách đây hơn <paramref name="maxAge"/>.</summary>
    public static void DeleteOlderThan(string folder, string pattern, TimeSpan maxAge)
    {
        if (!Directory.Exists(folder))
            return;

        DateTime cutoffUtc = DateTime.UtcNow - maxAge;

        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, pattern))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoffUtc)
                        File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Đang bị khoá (vd BarTender / trình xem PDF đang mở) — lần sau dọn.
                    System.Diagnostics.Debug.WriteLine($"Không xoá được {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Không duyệt được thư mục {folder}: {ex.Message}");
        }
    }

    /// <summary>
    /// Log ghi nối vào MỘT file cố định: vượt <paramref name="maxBytes"/> thì
    /// đổi tên thành "&lt;tên&gt;.old" (ghi đè bản .old trước) để bắt đầu file mới —
    /// luôn giữ được phần log gần nhất mà không tăng dung lượng vô hạn.
    /// </summary>
    public static void RotateIfLarger(string path, long maxBytes)
    {
        try
        {
            FileInfo info = new(path);
            if (info.Exists && info.Length > maxBytes)
                File.Move(path, path + ".old", overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Debug.WriteLine($"Không xoay vòng được log {path}: {ex.Message}");
        }
    }
}
