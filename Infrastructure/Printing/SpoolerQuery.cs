namespace KiotVietLabelPrinter.Infrastructure.Printing;

/// <summary>
/// Chạy một lệnh hỏi Print Spooler (liệt kê máy in, máy in mặc định,
/// PrinterSettings.IsValid...) ở luồng nền, có giới hạn thời gian.
///
/// Các API này gọi spooler ĐỒNG BỘ: trên máy có máy in mạng / máy in đang
/// tắt / driver lỗi, chúng có thể treo vô thời hạn. Chạy trên UI thread lúc
/// dựng cửa sổ thì cửa sổ chính không bao giờ hiện — tiến trình nằm trong
/// Background processes và giữ mutex chặn mọi lần mở lại (đã gặp thực tế:
/// startup.log kẹt ở "Tạo FormMain"). Hết giờ thì ném TimeoutException; luồng
/// nền bị treo bị bỏ lại, UI vẫn dùng được.
/// </summary>
public static class SpoolerQuery
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public static Task<T> RunAsync<T>(Func<T> query) =>
        Task.Run(query).WaitAsync(Timeout);
}
