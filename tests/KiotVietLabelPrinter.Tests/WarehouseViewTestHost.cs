using System.Runtime.ExceptionServices;
using KiotVietLabelPrinter.UI;
using KiotVietLabelPrinter.Views;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Các test mở cửa sổ thật chạy LẦN LƯỢT, không song song: các form cùng nằm ở
/// góc (0,0) và tranh nhau focus / kích hoạt / toạ độ màn hình, làm kết quả dàn
/// trang và lăn chuột sai ngẫu nhiên.
/// </summary>
[CollectionDefinition(WarehouseViewTestHost.Collection, DisableParallelization = true)]
public sealed class WarehouseWindowTestCollection
{
}

/// <summary>Chạy màn hình Phiếu xuất kho trong một form thật để test giao diện.</summary>
internal static class WarehouseViewTestHost
{
    public const string Collection = "Cửa sổ giao diện";

    /// <summary>Cho vòng lặp thông điệp xử lý xong các lần dàn trang / vẽ đang chờ.</summary>
    public static Task Settle() => Task.Delay(120);

    /// <summary>
    /// Chạy kịch bản trong một vòng lặp thông điệp thật (Application.Run) trên
    /// luồng STA — giống hệt app: các await của màn hình quay về đúng luồng giao
    /// diện. (Bơm thông điệp bằng DoEvents ngoài Application.Run thì không có
    /// SynchronizationContext của WinForms, phần sau await chạy nhầm sang luồng
    /// khác và test hỏng ngẫu nhiên.)
    /// </summary>
    public static void Run(int width, int height, Func<WarehouseExportView> createView, Func<Form, WarehouseExportView, Task> scenario)
    {
        Exception? error = null;
        Thread thread = new(() =>
        {
            using Form form = new()
            {
                StartPosition = FormStartPosition.Manual,
                Location = new Point(0, 0),
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                ClientSize = new Size(width, height),
                BackColor = AppTheme.Colors.Background
            };
            WarehouseExportView view = createView();
            form.Controls.Add(view);
            form.Shown += async (_, _) =>
            {
                try
                {
                    await Settle();
                    await scenario(form, view);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    form.Close();
                }
            };
            Application.Run(form);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromMinutes(2)))
            throw new TimeoutException("Kịch bản giao diện không kết thúc sau 2 phút.");
        if (error != null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }
}
