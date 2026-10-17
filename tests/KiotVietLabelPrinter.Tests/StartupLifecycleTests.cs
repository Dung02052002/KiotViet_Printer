using System.Reflection;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Services.BackgroundRemoval;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class StartupLifecycleTests
{
    [Fact]
    public void StartupLog_ResolveLogFile_SkipsUnwritableFolder_AndUsesNextCandidate()
    {
        string dir = TestData.NewTempDir();

        // Một FILE trùng tên thư mục log: CreateDirectory ném IOException —
        // giống thư mục không tạo được / không có quyền ghi.
        string blocked = Path.Combine(dir, "blocked");
        File.WriteAllText(blocked, "không phải thư mục");
        string fallback = Path.Combine(dir, "fallback");

        string? path = StartupLog.ResolveLogFile([blocked, fallback]);

        Assert.Equal(Path.Combine(fallback, "startup.log"), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void StartupLog_ResolveLogFile_NoWritableFolder_ReturnsNull()
    {
        string dir = TestData.NewTempDir();
        string blocked = Path.Combine(dir, "blocked");
        File.WriteAllText(blocked, "x");

        Assert.Null(StartupLog.ResolveLogFile([blocked]));
    }

    // Đóng app lúc model ONNX đang nạp: luồng nền giữ _sessionLock trong
    // EnsureLoaded. Dispose (chạy trên UI thread khi FormMain đóng) trước đây
    // chờ khoá đó → cửa sổ đã đóng mà tiến trình vẫn sống trong nền.
    [Fact]
    public async Task BackgroundRemovalService_Dispose_DoesNotWaitForSessionLock()
    {
        BackgroundRemovalService service = new();
        object sessionLock = typeof(BackgroundRemovalService)
            .GetField("_sessionLock", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(service)!;

        using ManualResetEventSlim locked = new();
        using ManualResetEventSlim release = new();

        Thread holder = new(() =>
        {
            lock (sessionLock)
            {
                locked.Set();
                release.Wait(TimeSpan.FromSeconds(30));
            }
        }) { IsBackground = true };
        holder.Start();
        Assert.True(locked.Wait(TimeSpan.FromSeconds(5)));

        try
        {
            Task dispose = Task.Run(service.Dispose);
            Task finished = await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.True(finished == dispose, "Dispose bị chặn bởi _sessionLock.");
        }
        finally
        {
            release.Set();
            holder.Join(TimeSpan.FromSeconds(5));
        }

        // Sau khi đã yêu cầu dispose, mọi lần nạp model tiếp theo phải bị từ chối.
        Assert.Throws<ObjectDisposedException>(() => service.EnsureLoaded(QualityMode.Fast, InferenceDevice.Cpu));
    }
}
