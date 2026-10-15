using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class FileRetentionTests
{
    [Fact]
    public void DeleteOlderThan_RemovesOnlyOldMatchingFiles()
    {
        string dir = TestData.NewTempDir();
        string oldXml = Touch(dir, "print_old.xml", TimeSpan.FromDays(10));
        string newXml = Touch(dir, "print_new.xml", TimeSpan.FromHours(1));
        string oldOther = Touch(dir, "last_print_debug.xml", TimeSpan.FromDays(10));

        FileRetention.DeleteOlderThan(dir, "print_*.xml", TimeSpan.FromDays(7));

        Assert.False(File.Exists(oldXml));
        Assert.True(File.Exists(newXml));
        Assert.True(File.Exists(oldOther));
    }

    [Fact]
    public void DeleteOlderThan_MissingFolder_DoesNothing()
    {
        FileRetention.DeleteOlderThan(Path.Combine(TestData.NewTempDir(), "khong-co"), "*", TimeSpan.Zero);
    }

    [Fact]
    public void RotateIfLarger_MovesBigLogToOld_AndKeepsSmallLog()
    {
        string dir = TestData.NewTempDir();
        string big = Path.Combine(dir, "big.log");
        string small = Path.Combine(dir, "small.log");
        File.WriteAllText(big, new string('x', 2000));
        File.WriteAllText(big + ".old", "bản old cũ");
        File.WriteAllText(small, "ok");

        FileRetention.RotateIfLarger(big, 1000);
        FileRetention.RotateIfLarger(small, 1000);

        Assert.False(File.Exists(big));
        Assert.Equal(2000, new FileInfo(big + ".old").Length);
        Assert.Equal("ok", File.ReadAllText(small));
        Assert.False(File.Exists(small + ".old"));
    }

    private static string Touch(string dir, string name, TimeSpan age)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return path;
    }
}
