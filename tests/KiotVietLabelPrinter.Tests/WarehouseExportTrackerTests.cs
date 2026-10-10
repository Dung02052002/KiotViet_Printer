using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Tình trạng file phiếu đã xuất: chỉ bản xuất từ đúng dữ liệu / thiết lập
/// đang hiển thị, còn nguyên trên đĩa, mới được xem trước và in.
/// </summary>
public class WarehouseExportTrackerTests
{
    private static WarehouseExportResult Export(string dir, string name = "XK1.xlsx") =>
        WarehouseRows.Export(WarehouseTestData.CreateTemplate(dir), Path.Combine(dir, name), 3);

    [Fact]
    public void NothingExported_IsNone()
    {
        WarehouseExportTracker tracker = new();
        Assert.Equal(WarehouseExportState.None, tracker.Check());
        Assert.Contains("XUẤT PHIẾU EXCEL", tracker.Describe(WarehouseExportState.None));

        // Đổi dữ liệu khi chưa xuất lần nào: không có gì để thành "cũ".
        tracker.MarkStale();
        Assert.Equal(WarehouseExportState.None, tracker.Check());
    }

    [Fact]
    public void AnyChangeAfterExport_MakesItStale_UntilExportedAgain()
    {
        string dir = TestData.NewTempDir();
        WarehouseExportTracker tracker = new();
        tracker.Record(Export(dir));
        Assert.Equal(WarehouseExportState.Current, tracker.Check());
        Assert.Equal("", tracker.Describe(WarehouseExportState.Current));

        // File vẫn còn nguyên trên đĩa nhưng dữ liệu đã đổi → KHÔNG được coi là hợp lệ.
        tracker.MarkStale();
        Assert.True(File.Exists(tracker.Last!.OutputPath));
        Assert.Equal(WarehouseExportState.Stale, tracker.Check());

        // (Xuất lại cùng tên cần người dùng xác nhận ghi đè — writer từ chối nếu không; ở đây xuất ra tên khác.)
        tracker.Record(Export(dir, "XK1-lan2.xlsx"));
        Assert.Equal(WarehouseExportState.Current, tracker.Check());
    }

    [Fact]
    public void FileDeletedOrEditedOutside_IsNotCurrent()
    {
        string dir = TestData.NewTempDir();
        WarehouseExportTracker tracker = new();

        tracker.Record(Export(dir, "a.xlsx"));
        File.AppendAllText(tracker.Last!.OutputPath, " ");
        Assert.Equal(WarehouseExportState.ModifiedExternally, tracker.Check());

        tracker.Record(Export(dir, "b.xlsx"));
        File.Delete(tracker.Last!.OutputPath);
        Assert.Equal(WarehouseExportState.Missing, tracker.Check());
        Assert.Contains(tracker.Last.OutputPath, tracker.Describe(WarehouseExportState.Missing));
    }

    [Fact]
    public void Preview_BelongsToTheExactExport_AndIsForgottenOnPrinterChangeOrReexport()
    {
        string dir = TestData.NewTempDir();
        WarehouseExportTracker tracker = new();
        tracker.Record(Export(dir));
        Assert.False(tracker.WasPreviewed);

        tracker.MarkPreviewed(tracker.Last!.Stamp);
        Assert.True(tracker.WasPreviewed);

        // Đổi máy in: bản xem trước phân trang theo máy cũ.
        tracker.ForgetPreview();
        Assert.False(tracker.WasPreviewed);

        // Bản xem trước của lần xuất trước không tính cho lần xuất mới.
        tracker.MarkPreviewed(tracker.Last.Stamp);
        tracker.Record(Export(dir, "XK2.xlsx"));
        Assert.False(tracker.WasPreviewed);
    }

    [Fact]
    public void Reset_ForgetsTheExport()
    {
        string dir = TestData.NewTempDir();
        WarehouseExportTracker tracker = new();
        tracker.Record(Export(dir));
        tracker.MarkStale();

        tracker.Reset();
        Assert.Null(tracker.Last);
        Assert.Equal(WarehouseExportState.None, tracker.Check());
    }
}
