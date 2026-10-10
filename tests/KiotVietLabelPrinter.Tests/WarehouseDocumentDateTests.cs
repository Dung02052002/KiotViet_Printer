using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using KiotVietLabelPrinter.Views;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>Ghép / đọc chữ ô ngày lập phiếu ("Ngày  22  tháng   9     năm 2026").</summary>
public class WarehouseDocumentDateServiceTests
{
    // Chữ thật lấy từ các file mẫu (khoảng trắng khác nhau ở từng cửa hàng).
    [Theory]
    [InlineData("Ngày  22  tháng   9     năm 2026", 2026, 10, 10, "Ngày  10  tháng   10     năm 2026")]
    [InlineData("Ngày   9   tháng   9    năm 2026", 2026, 1, 5, "Ngày   5   tháng   1    năm 2026")]
    [InlineData("Ngày    6   tháng   7   năm 2026", 2025, 12, 31, "Ngày    31   tháng   12   năm 2025")]
    [InlineData("Ngày 09 tháng 09 năm 2026", 2026, 3, 1, "Ngày 01 tháng 03 năm 2026")]
    [InlineData("Ngày ... tháng ... năm ...", 2028, 2, 29, "Ngày 29 tháng 2 năm 2028")]
    [InlineData("Ngày…tháng…năm…", 2026, 10, 10, "Ngày 10 tháng 10 năm 2026")]
    [InlineData("Hà Nội, ngày 1 tháng 1 năm 2026", 2026, 4, 30, "Hà Nội, ngày 30 tháng 4 năm 2026")]
    public void Compose_ReplacesOnlyTheNumbers(string template, int y, int m, int d, string expected)
    {
        string text = WarehouseDocumentDateService.Compose(template, new DateTime(y, m, d, 23, 59, 0));
        Assert.Equal(expected, text);
        Assert.Equal(new DateTime(y, m, d), WarehouseDocumentDateService.TryRead(text));
    }

    [Fact]
    public void Read_DistinguishesDayFromMonth()
    {
        // 03/04 = ngày 3 tháng 4, không phải 4 tháng 3.
        Assert.Equal(new DateTime(2026, 4, 3), WarehouseDocumentDateService.TryRead("Ngày 3 tháng 4 năm 2026"));
        Assert.Null(WarehouseDocumentDateService.TryRead("Ngày 31 tháng 2 năm 2026"));
        Assert.Null(WarehouseDocumentDateService.TryRead("Ngày ... tháng ... năm ..."));
    }

    [Theory]
    [InlineData("Mẫu số : 02-VT\n(Ban hành theo Thông tư số 133/2016/TT-BTC Ngày 26/08/2016 của Bộ tài Chính)")]
    [InlineData("Số : XK023056")]
    [InlineData("Ngày giao hàng: 22/09/2026")]
    [InlineData("Ngày 22 tháng 9 năm 2026 (đã ký)")]
    [InlineData("")]
    public void IsDateText_RejectsOtherCells(string text) => Assert.False(WarehouseDocumentDateService.IsDateText(text));

    [Fact]
    public void Validate_RejectsOutOfRange()
    {
        Assert.Null(WarehouseDocumentDateService.Validate(new DateTime(2026, 10, 10)));
        Assert.NotNull(WarehouseDocumentDateService.Validate(new DateTime(1999, 12, 31)));
        Assert.NotNull(WarehouseDocumentDateService.Validate(new DateTime(2100, 1, 1)));
    }
}

/// <summary>Ngày lập phiếu được ghi vào đúng ô của file kết quả và kiểm tra lại sau khi ghi.</summary>
public class WarehouseDocumentDateExportTests
{
    private static readonly List<WarehouseProductRow> Rows =
    [
        new() { SourceRowNumber = 2, ProductCode = "SP1", TicketName = "Giày nữ", Unit = "Đôi", RequestedQuantity = 4, ActualQuantity = 4 }
    ];

    private static WarehouseExportResult Export(string template, string output, DateTime date) =>
        new WarehouseExcelWriter().Export(new WarehouseExportRequest
        {
            TemplatePath = template,
            OutputPath = output,
            TicketCode = "XK23609",
            DocumentDate = date,
            Rows = Rows
        });

    private static ICell B4(IWorkbook wb) => wb.GetSheetAt(0).GetRow(3).GetCell(1);

    [Theory]
    [InlineData(2026, 10, 10, ".xlsx")] // khác ngày trong mẫu (22/09/2026)
    [InlineData(2024, 1, 15, ".xlsx")]  // quá khứ
    [InlineData(2027, 6, 1, ".xlsx")]   // tương lai
    [InlineData(2026, 4, 30, ".xlsx")]  // cuối tháng
    [InlineData(2026, 12, 31, ".xlsx")] // cuối năm
    [InlineData(2028, 2, 29, ".xlsx")]  // năm nhuận
    [InlineData(2026, 3, 4, ".xls")]    // ngày ≤ 12: không được đảo ngày / tháng
    public void Export_WritesSelectedDate_KeepsCellFormatting(int y, int m, int d, string extension)
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir, extension: extension);
        string hash = WarehouseTestData.Sha256(template);
        DateTime date = new(y, m, d);

        WarehouseTemplateLayout layout = new WarehouseExcelReader().ReadTemplateLayout(template);
        Assert.Equal("B4", new CellReference(layout.DocumentDateRow, layout.DocumentDateColumn).FormatAsString());

        WarehouseExportResult result = Export(template, Path.Combine(dir, "XK23609" + extension), date);
        Assert.Contains(result.VerifiedChecks, c => c.StartsWith("Ngày lập phiếu") && c.Contains(date.ToString("dd/MM/yyyy")));

        using IWorkbook original = WarehouseExcelReader.OpenWorkbook(template, "test");
        using IWorkbook output = WarehouseExcelReader.OpenWorkbook(result.OutputPath, "test");
        ICell cell = B4(output);
        Assert.Equal(CellType.String, cell.CellType);
        Assert.Equal($"Ngày  {d}  tháng   {m}     năm {y}", cell.StringCellValue);
        Assert.Equal(date, WarehouseDocumentDateService.TryRead(cell.StringCellValue));

        // Chỉ chữ đổi: font, cỡ chữ, căn lề, viền, nền, định dạng số giữ nguyên như ô mẫu.
        Assert.Equal(SnapshotReader.Cell(original, B4(original)).Style, SnapshotReader.Cell(output, cell).Style);
        Assert.Equal(SnapshotReader.Cell(original, B4(original)).Font, SnapshotReader.Cell(output, cell).Font);
        Assert.Equal("Số : XK23609", output.GetSheetAt(0).GetRow(4).GetCell(1).StringCellValue);
        Assert.Equal(hash, WarehouseTestData.Sha256(template)); // file mẫu không bị sửa
    }

    [Fact]
    public void Template_WithoutDateCell_IsReported()
    {
        string dir = TestData.NewTempDir();
        string template = Rewrite(WarehouseTestData.CreateTemplate(dir), wb => B4(wb).SetCellValue("Ngày lập: (để trống)"));

        WarehouseException ex = Assert.Throws<WarehouseException>(() => new WarehouseExcelReader().ReadTemplateLayout(template));
        Assert.Equal(WarehouseErrorKind.TemplateStructure, ex.Kind);
        Assert.Contains("ngày lập phiếu", ex.Message);
        Assert.Throws<WarehouseException>(() => Export(template, Path.Combine(dir, "out.xlsx"), new DateTime(2026, 10, 10)));
        Assert.False(File.Exists(Path.Combine(dir, "out.xlsx")));
    }

    [Fact]
    public void Template_WithTwoDateCells_IsReported()
    {
        string dir = TestData.NewTempDir();
        string template = Rewrite(WarehouseTestData.CreateTemplate(dir),
            wb => wb.GetSheetAt(0).GetRow(4).CreateCell(2).SetCellValue("Ngày 1 tháng 1 năm 2026"));

        WarehouseException ex = Assert.Throws<WarehouseException>(() => new WarehouseExcelReader().ReadTemplateLayout(template));
        Assert.Contains("B4", ex.Message);
        Assert.Contains("C5", ex.Message);
    }

    /// <summary>Các file mẫu THẬT trên máy (nếu có): ô ngày luôn ở B4 và ghi đúng ngày đã chọn.</summary>
    [Theory]
    [InlineData("Kho HCM", "Phiếu Xuất Kho.xlsx")]
    [InlineData("ViVo", "Phiếu Xuất Kho.xlsx")]
    [InlineData("Thảo Điền", "PHIẾU XUẤT KHO.xlsx")]
    public void RealTemplates_DateWrittenInPlace(string folder, string file)
    {
        string template = Path.Combine(WarehouseTestData.RealTemplatesFolder, folder, file);
        if (!File.Exists(template))
            return; // máy không có file mẫu thật

        string dir = TestData.NewTempDir();
        WarehouseTemplateLayout layout = new WarehouseExcelReader().ReadTemplateLayout(template);
        Assert.Equal("B4", new CellReference(layout.DocumentDateRow, layout.DocumentDateColumn).FormatAsString());

        WarehouseExportResult result = Export(template, Path.Combine(dir, "XK23609.xlsx"), new DateTime(2026, 12, 31));
        using IWorkbook output = WarehouseExcelReader.OpenWorkbook(result.OutputPath, "test");
        Assert.Equal(WarehouseDocumentDateService.Compose(layout.DocumentDateText, new DateTime(2026, 12, 31)), B4(output).StringCellValue);
        Assert.Equal(new DateTime(2026, 12, 31), WarehouseDocumentDateService.TryRead(B4(output).StringCellValue));
    }

    private static string Rewrite(string path, Action<IWorkbook> change)
    {
        using IWorkbook wb = WarehouseExcelReader.OpenWorkbook(path, "test");
        change(wb);
        using FileStream fs = File.Create(path);
        wb.Write(fs);
        return path;
    }
}

/// <summary>
/// Ô chọn ngày trên màn hình Phiếu xuất kho: mặc định theo ngày của máy, ngày
/// người dùng chọn được giữ suốt phiên, đổi ngày làm bản đã xuất thành bản cũ.
/// </summary>
[Collection(WarehouseViewTestHost.Collection)]
public class WarehouseExportViewDateTests : IDisposable
{
    private readonly string _dir = TestData.NewTempDir();
    private DateTime _today = new(2026, 10, 10);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // Thư mục tạm của test — để hệ thống tự dọn nếu còn file bị khoá.
        }
    }

    private void Run(Func<WarehouseExportView, DateTimePicker, Task> scenario) =>
        WarehouseViewTestHost.Run(1300, 640,
            () => new WarehouseExportView(new WarehouseSettingsStore(Path.Combine(_dir, $"settings-{Guid.NewGuid():N}.json")), () => _today),
            (_, view) => scenario(view, Picker(view)));

    private static DateTimePicker Picker(Control root) =>
        Descendants(root).OfType<DateTimePicker>().Single();

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child))
                yield return nested;
        }
    }

    [Fact]
    public void Default_IsToday_FollowsNewDay_OnReopen()
    {
        Run(async (view, picker) =>
        {
            Assert.Equal(new DateTime(2026, 10, 10), view.SelectedDocumentDate);
            Assert.Equal("dd/MM/yyyy", picker.CustomFormat);
            Assert.Equal(DateTimePickerFormat.Custom, picker.Format);

            _today = new DateTime(2026, 10, 11);
            view.OnShown();
            Assert.Equal(new DateTime(2026, 10, 11), view.SelectedDocumentDate);

            _today = new DateTime(2026, 10, 12);
            view.OnShown();
            Assert.Equal(new DateTime(2026, 10, 12), view.SelectedDocumentDate);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public void ChosenDate_IsKept_ThroughReloadEditsAndNewDay()
    {
        Run(async (view, picker) =>
        {
            string template = WarehouseTestData.CreateTemplate(_dir);
            string goods = WarehouseTestData.CreateGoods(_dir,
                WarehouseTestData.Line("SP1", "Giày nữ Pucini", 4d),
                WarehouseTestData.Line("SP2", "Kính mắt Pucini", 2d));

            picker.Value = new DateTime(2026, 9, 30);

            await view.LoadFilesAsync(template, goods); // nạp / làm mới dữ liệu
            await WarehouseViewTestHost.Settle();
            Assert.Equal(new DateTime(2026, 9, 30), view.SelectedDocumentDate);

            DataGridView grid = Descendants(view).OfType<DataGridView>().Single();
            grid.CurrentCell = grid.Rows[0].Cells["colRequested"];
            grid.BeginEdit(selectAll: true);
            grid.EditingControl!.Text = "7";
            grid.EndEdit(); // đổi số lượng
            await WarehouseViewTestHost.Settle();
            Assert.Equal(7, ((WarehouseProductRow)grid.Rows[0].Tag!).RequestedQuantity);
            Assert.Equal(new DateTime(2026, 9, 30), view.SelectedDocumentDate);

            _today = new DateTime(2026, 10, 11);
            view.OnShown(); // mở lại màn hình sang ngày mới
            Assert.Equal(new DateTime(2026, 9, 30), view.SelectedDocumentDate);
        });
    }

    [Fact]
    public void ChoosingTodayAgain_ReturnsToDefault()
    {
        Run(async (view, picker) =>
        {
            picker.Value = new DateTime(2026, 12, 31);
            picker.Value = new DateTime(2026, 10, 10);

            _today = new DateTime(2026, 10, 11);
            view.OnShown();
            Assert.Equal(new DateTime(2026, 10, 11), view.SelectedDocumentDate);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public void ChangingDate_AfterExport_MakesExportStale()
    {
        Run(async (view, picker) =>
        {
            string template = WarehouseTestData.CreateTemplate(_dir);
            WarehouseExportResult result = new WarehouseExcelWriter().Export(new WarehouseExportRequest
            {
                TemplatePath = template,
                OutputPath = Path.Combine(_dir, "XK1.xlsx"),
                TicketCode = "XK1",
                DocumentDate = view.SelectedDocumentDate,
                Rows = [new WarehouseProductRow { SourceRowNumber = 2, TicketName = "Giày nữ", Unit = "Đôi", RequestedQuantity = 1, ActualQuantity = 1 }]
            });
            view.ExportTracker.Record(result);
            Assert.Equal(WarehouseExportState.Current, view.ExportTracker.Check());

            picker.Value = new DateTime(2026, 10, 9);
            Assert.Equal(WarehouseExportState.Stale, view.ExportTracker.Check());
            Assert.False(PrintButton(view).Enabled);
            Assert.False(PreviewButton(view).Enabled);
            await Task.CompletedTask;
        });
    }

    [Fact]
    public void NewDay_WithDefaultDate_MakesYesterdaysExportStale()
    {
        Run(async (view, _) =>
        {
            string template = WarehouseTestData.CreateTemplate(_dir);
            view.ExportTracker.Record(new WarehouseExcelWriter().Export(new WarehouseExportRequest
            {
                TemplatePath = template,
                OutputPath = Path.Combine(_dir, "XK2.xlsx"),
                TicketCode = "XK2",
                DocumentDate = view.SelectedDocumentDate,
                Rows = [new WarehouseProductRow { SourceRowNumber = 2, TicketName = "Giày nữ", Unit = "Đôi", RequestedQuantity = 1, ActualQuantity = 1 }]
            }));

            _today = new DateTime(2026, 10, 11);
            view.OnShown();
            Assert.Equal(WarehouseExportState.Stale, view.ExportTracker.Check());
            await Task.CompletedTask;
        });
    }

    /// <summary>Lăn chuột cuộn trang lướt qua ô ngày (chưa bấm vào): trang cuộn, ngày KHÔNG đổi.</summary>
    [Fact]
    public void MouseWheelOverUnfocusedPicker_ScrollsPage_KeepsDate()
    {
        Run(async (view, picker) =>
        {
            ScrollableControl scroll = (ScrollableControl)Descendants(view).OfType<DataGridView>().Single().Parent!.Parent!.Parent!;
            scroll.AutoScrollPosition = new Point(0, 0);
            Descendants(view).OfType<DataGridView>().Single().Focus();
            await WarehouseViewTestHost.Settle();
            int scrolledBefore = scroll.AutoScrollPosition.Y;

            Point center = picker.PointToScreen(new Point(picker.Width / 2, picker.Height / 2));
            const int wmMouseWheel = 0x020A;
            nint wParam = unchecked((nint)(uint)((-120 & 0xFFFF) << 16));
            nint lParam = (center.Y << 16) | (center.X & 0xFFFF);
            SendMessage(picker.Handle, wmMouseWheel, wParam, lParam);
            await WarehouseViewTestHost.Settle();

            Assert.Equal(new DateTime(2026, 10, 10), view.SelectedDocumentDate);
            Assert.True(scroll.AutoScrollPosition.Y < scrolledBefore, "Lăn chuột trên ô ngày phải cuộn trang");
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private static Button PrintButton(Control view) => Button(view, "IN PHIẾU");

    private static Button PreviewButton(Control view) => Button(view, "XEM TRƯỚC BẢN IN");

    private static Button Button(Control view, string text) =>
        Descendants(view).OfType<Button>().Single(b => b.Text == text);
}
