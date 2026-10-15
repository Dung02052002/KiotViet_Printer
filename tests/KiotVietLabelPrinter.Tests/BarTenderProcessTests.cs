using System.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.BarTender.Backends;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class BarTenderProcessTests
{
    // Tiến trình ghi nhiều hơn bộ đệm pipe ra stdout: trước đây Run() chỉ đọc
    // output SAU khi chờ thoát → tiến trình bị chặn ở lệnh ghi, bị coi là treo.
    [Fact]
    public void Run_LargeStdOut_DoesNotDeadlock()
    {
        string file = Path.Combine(TestData.NewTempDir(), "big.txt");
        File.WriteAllText(file, new string('x', 200_000));

        ProcessStartInfo psi =
            BarTenderProcess.BuildPsi(
                Path.Combine(Environment.SystemDirectory, "cmd.exe"));
        psi.Arguments = $"/c type \"{file}\"";

        var result = BarTenderProcess.Run(psi, false, 10_000);

        Assert.True(result.Exited);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(200_000, result.StdOut.Length);
    }
}
