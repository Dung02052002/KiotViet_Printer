using System.Diagnostics;
using System.Drawing;
using System.Text;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.SS.UserModel;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

internal static class WarehouseRows
{
    public static List<WarehouseProductRow> Make(int count, string? longName = null) =>
        Enumerable.Range(1, count).Select(i => new WarehouseProductRow
        {
            SourceRowNumber = i + 1,
            ProductCode = $"SP{i:000}",
            SourceName = $"Sản phẩm {i}",
            TicketName = longName ?? (i % 2 == 0 ? $"Giày nữ mẫu {i}, mũ giày bằng da tổng hợp" : $"Kính mắt Pucini mẫu {i}"),
            Unit = i % 2 == 0 ? "Đôi" : "Cái",
            RequestedQuantity = i,
            ActualQuantity = i
        }).ToList();

    public static WarehouseExportResult Export(string template, string output, int count,
        WarehousePageSettings? page = null, WarehouseFontSettings? font = null, string? longName = null) =>
        new WarehouseExcelWriter().Export(new WarehouseExportRequest
        {
            TemplatePath = template,
            OutputPath = output,
            TicketCode = "XK23609",
            Rows = Make(count, longName),
            Page = page ?? WarehousePageSettings.KeepTemplate(),
            Font = font ?? WarehouseFontSettings.KeepTemplate()
        });
}

public class WarehousePageSetupTests
{
    private static WarehousePageSetup PageOf(string path)
    {
        using IWorkbook wb = WarehouseExcelReader.OpenWorkbook(path, "test");
        return WarehousePageSetupAccess.Read(wb, 0);
    }

    [Fact]
    public void Template_PageSetup_IsReadFromFile()
    {
        string template = WarehouseTestData.CreateTemplate(TestData.NewTempDir());
        WarehousePageSetup page = new WarehouseExcelReader().ReadTemplateLayout(template).Page;

        Assert.Equal(9, page.PaperSize); // A4
        Assert.False(page.Landscape);
        Assert.Equal(0.25, page.Margins.Left, 3);
        Assert.Equal(0.75, page.Margins.Top, 3);
        Assert.Equal("Sheet1!$A$1:$D$19", page.PrintArea);
        Assert.StartsWith("A4", page.PaperName);
        Assert.Equal("Dọc", page.OrientationName);
    }

    [Fact]
    public void Export_KeepTemplate_LeavesPageSetupUntouched_ExceptShiftedPrintArea()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "out.xlsx"), 5);

        WarehousePageSetup before = PageOf(template);
        WarehousePageSetup after = PageOf(result.OutputPath);
        Assert.Equal(before with { PrintArea = null }, after with { PrintArea = null });
        Assert.Equal("Sheet1!$A$1:$D$22", after.PrintArea); // 3 dòng hàng thêm → vùng in dời 3 dòng
        Assert.Contains(result.VerifiedChecks, c => c.Contains("Thiết lập trang in giữ nguyên"));
    }

    public static TheoryData<string> Overrides => new() { "paper", "landscape", "margins", "percent", "fitWidth", "fitOnePage", "all" };

    private static WarehousePageSettings Make(string kind) => kind switch
    {
        "paper" => new() { PaperSize = 1 },
        "landscape" => new() { Landscape = true },
        "margins" => new() { Margins = new WarehouseMargins(0.5, 0.4, 0.6, 0.7, 0, 0) },
        "percent" => new() { Scale = new WarehouseScaleChoice(WarehouseScaleMode.Percent, 85) },
        "fitWidth" => new() { Scale = new WarehouseScaleChoice(WarehouseScaleMode.FitWidth) },
        "fitOnePage" => new() { Scale = new WarehouseScaleChoice(WarehouseScaleMode.FitOnePage) },
        _ => new()
        {
            PaperSize = 11, Landscape = true, Margins = new WarehouseMargins(0.3, 0.3, 0.5, 0.5, 0, 0),
            Scale = new WarehouseScaleChoice(WarehouseScaleMode.Percent, 120)
        }
    };

    [Theory]
    [MemberData(nameof(Overrides))]
    public void Export_ChangesOnlyTheSelectedPageItems(string kind)
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string hash = WarehouseTestData.Sha256(template);
        WarehousePageSettings settings = Make(kind);

        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, $"{kind}.xlsx"), 2, settings);

        WarehousePageSetup expected = settings.ApplyTo(PageOf(template));
        WarehousePageSetup actual = PageOf(result.OutputPath);
        Assert.Equal(expected.PaperSize, actual.PaperSize);
        Assert.Equal(expected.Landscape, actual.Landscape);
        Assert.Equal(expected.FitToPage, actual.FitToPage);
        Assert.Equal(expected.Scale, actual.Scale);
        if (expected.FitToPage)
            Assert.Equal((expected.FitWidth, expected.FitHeight), (actual.FitWidth, actual.FitHeight));
        Assert.Equal(expected.Margins.Left, actual.Margins.Left, 3);
        Assert.Equal(expected.Margins.Bottom, actual.Margins.Bottom, 3);
        // Lề header/footer không bao giờ bị đổi (dù người dùng đổi lề trang).
        Assert.Equal(PageOf(template).Margins.Header, actual.Margins.Header, 3);
        Assert.Equal(expected.PrintArea, actual.PrintArea);
        Assert.Contains(result.VerifiedChecks, c => c.Contains("đã đổi"));
        Assert.Equal(hash, WarehouseTestData.Sha256(template));
    }

    [Fact]
    public void PageSettings_DescribeAndKeepTemplate()
    {
        Assert.True(WarehousePageSettings.KeepTemplate().KeepsTemplate);
        Assert.Equal("giữ nguyên như mẫu", WarehousePageSettings.KeepTemplate().Describe());
        string text = new WarehousePageSettings { PaperSize = 9, Landscape = true }.Describe();
        Assert.Contains("A4", text);
        Assert.Contains("giấy ngang", text);
    }

    [Fact]
    public void ValidatePage_BlocksImpossibleValues_AndWarnsSmallText()
    {
        WarehouseTemplateLayout layout = new WarehouseExcelReader().ReadTemplateLayout(WarehouseTestData.CreateTemplate(TestData.NewTempDir()));

        WarehouseValidationReport negative = new();
        WarehouseDocumentService.ValidatePage(new() { Margins = new WarehouseMargins(-0.1, 0.2, 0.5, 0.5, 0, 0) }, layout, negative);
        Assert.Contains(negative.Errors, e => e.Contains("âm"));

        WarehouseValidationReport tooWide = new();
        WarehouseDocumentService.ValidatePage(new() { Margins = new WarehouseMargins(4, 4, 0.5, 0.5, 0, 0) }, layout, tooWide);
        Assert.Contains(tooWide.Errors, e => e.Contains("quá lớn"));

        WarehouseValidationReport badScale = new();
        WarehouseDocumentService.ValidatePage(new() { Scale = new WarehouseScaleChoice(WarehouseScaleMode.Percent, 5) }, layout, badScale);
        Assert.Contains(badScale.Errors, e => e.Contains("10%"));

        WarehouseValidationReport small = new();
        WarehouseDocumentService.ValidatePage(new() { Scale = new WarehouseScaleChoice(WarehouseScaleMode.Percent, 50) }, layout, small);
        Assert.Empty(small.Errors);
        Assert.Contains(small.Warnings, w => w.Contains("khó đọc"));

        WarehouseValidationReport onePage = new();
        WarehouseDocumentService.ValidatePage(new() { Scale = new WarehouseScaleChoice(WarehouseScaleMode.FitOnePage) }, layout, onePage);
        Assert.Contains(onePage.Warnings, w => w.Contains("Vừa một trang"));

        WarehouseValidationReport keep = new();
        WarehouseDocumentService.ValidatePage(WarehousePageSettings.KeepTemplate(), layout, keep);
        Assert.Empty(keep.Errors);
        Assert.Empty(keep.Warnings);
    }

    [Theory]
    [InlineData("Kho HCM", "Phiếu Xuất Kho.xlsx")]
    [InlineData("Đồng Khởi", "Phiếu Xuất Kho.xlsx")]
    public void RealTemplates_PageSetup_IsLetterPortrait(string folder, string file)
    {
        string template = Path.Combine(WarehouseTestData.RealTemplatesFolder, folder, file);
        if (!File.Exists(template))
            return;

        WarehousePageSetup page = new WarehouseExcelReader().ReadTemplateLayout(template).Page;
        // Đo bằng Excel trên các mẫu thật: PaperSize = 1 (Letter), dọc, lề 0,25" / 0,75", không đặt vùng in.
        Assert.Equal(1, page.PaperSize);
        Assert.False(page.Landscape);
        Assert.Equal(0.25, page.Margins.Left, 2);
        Assert.Equal(0.75, page.Margins.Top, 2);
        Assert.Null(page.PrintArea);
    }
}

public class WarehousePreviewNavigatorTests
{
    [Fact]
    public void Navigator_MovesBetweenRealPages_AndClamps()
    {
        WarehousePreviewNavigator nav = new(3);
        Assert.Equal("Trang 1 / 3", nav.Label);
        Assert.False(nav.CanGoBack);
        Assert.False(nav.Previous());

        Assert.True(nav.Next());
        Assert.Equal("Trang 2 / 3", nav.Label);
        Assert.True(nav.Last());
        Assert.Equal(3, nav.Current);
        Assert.False(nav.CanGoForward);
        Assert.False(nav.Next());

        Assert.True(nav.Previous());
        Assert.Equal(2, nav.Current);
        Assert.True(nav.First());
        Assert.Equal(1, nav.Current);
        Assert.True(nav.GoTo(99));
        Assert.Equal(3, nav.Current);
    }

    [Fact]
    public void Navigator_SinglePage_HasNoNavigation()
    {
        WarehousePreviewNavigator nav = new(1);
        Assert.Equal("Trang 1 / 1", nav.Label);
        Assert.False(nav.CanGoBack || nav.CanGoForward);
        Assert.Throws<ArgumentOutOfRangeException>(() => new WarehousePreviewNavigator(0));
    }

    [Fact]
    public void DescribeRows_NamesTheParts_OfThePhieu()
    {
        string dir = TestData.NewTempDir();
        WarehouseExportResult result = WarehouseRows.Export(WarehouseTestData.CreateTemplate(dir), Path.Combine(dir, "o.xlsx"), 10);
        WarehouseTemplateLayout layout = result.Layout; // dòng hàng: Excel 14–23

        Assert.Equal("dòng Excel 1–18: đầu phiếu, tiêu đề bảng, dòng hàng 1–5",
            WarehousePrintPreviewService.DescribeRows(1, 18, layout));
        Assert.Equal("dòng Excel 19–27: dòng hàng 6–10, cuối phiếu (ký tên)",
            WarehousePrintPreviewService.DescribeRows(19, 27, layout));
    }
}

public class WarehouseFileStampTests
{
    [Fact]
    public void ExportResult_CarriesStamp_SheetAndLayout_AndDetectsLaterChanges()
    {
        string dir = TestData.NewTempDir();
        WarehouseExportResult result = WarehouseRows.Export(WarehouseTestData.CreateTemplate(dir), Path.Combine(dir, "o.xlsx"), 4);

        Assert.Equal("Sheet1", result.SheetName);
        Assert.Equal(4, result.Layout.DataRowCount);
        Assert.True(result.Stamp.Matches(result.OutputPath));

        // Sửa file bên ngoài (vd mở bằng Excel rồi lưu) → bản đã xuất không còn được coi là bản mới nhất.
        File.AppendAllText(result.OutputPath, " ");
        Assert.False(result.Stamp.Matches(result.OutputPath));

        File.Delete(result.OutputPath);
        Assert.False(result.Stamp.Matches(result.OutputPath));
        Assert.Null(WarehouseFileStamp.Capture(result.OutputPath));
    }
}

public class PdfPageRendererTests
{
    /// <summary>PDF tối giản viết tay: mỗi trang có một ô vuông đen 100×100pt ở góc dưới-trái (cách mép 72pt).</summary>
    internal static byte[] MinimalPdf(params (int Width, int Height)[] pages)
    {
        List<string> objects = new()
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Kids [{string.Join(" ", pages.Select((_, i) => $"{3 + i * 2} 0 R"))}] /Count {pages.Length} >>"
        };
        foreach ((int w, int h) in pages)
        {
            int contentId = objects.Count + 2;
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {w} {h}] /Contents {contentId} 0 R >>");
            const string content = "0 0 0 rg 72 72 100 100 re f";
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}\nendstream");
        }

        StringBuilder pdf = new("%PDF-1.4\n");
        List<int> offsets = new();
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        int xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (int offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    [Fact]
    public void Renderer_ReadsPageCountAndSize_AndDrawsContent()
    {
        string path = Path.Combine(TestData.NewTempDir(), "two.pdf");
        File.WriteAllBytes(path, MinimalPdf((612, 792), (842, 595)));

        using PdfPageRenderer pdf = PdfPageRenderer.Open(path);
        Assert.Equal(2, pdf.PageCount);
        Assert.Equal(new SizeF(612, 792), pdf.GetPageSizePoints(0));
        Assert.Equal(new SizeF(842, 595), pdf.GetPageSizePoints(1));

        using Bitmap page = pdf.Render(0, 72);
        Assert.Equal((612, 792), (page.Width, page.Height));
        // Ô vuông đen: x 72–172, y (từ trên) 792-172 = 620 … 720.
        Assert.Equal(Color.Black.ToArgb(), page.GetPixel(120, 670).ToArgb());
        Assert.Equal(Color.White.ToArgb(), page.GetPixel(10, 10).ToArgb());

        using Bitmap hiRes = pdf.Render(1, 144);
        Assert.Equal((1684, 1190), (hiRes.Width, hiRes.Height));
        Assert.Throws<ArgumentOutOfRangeException>(() => pdf.GetPageSizePoints(2));
    }

    [Fact]
    public void Renderer_RejectsNonPdf()
    {
        string path = Path.Combine(TestData.NewTempDir(), "bad.pdf");
        File.WriteAllText(path, "không phải pdf");
        WarehouseException ex = Assert.Throws<WarehouseException>(() => PdfPageRenderer.Open(path));
        Assert.Equal(WarehouseErrorKind.PreviewFailed, ex.Kind);
    }
}

/// <summary>
/// Xem trước / in THẬT bằng Microsoft Excel. Mở Excel nên chỉ chạy khi đặt
/// KV_WAREHOUSE_PRINT_TEST=1 (và máy có Excel). Cùng collection với test in
/// cũ để không chạy song song (kiểm tra tiến trình Excel tồn đọng).
/// </summary>
[Collection("Excel")]
public class WarehousePrintPreviewExcelTests
{
    private const string PdfPrinter = "Microsoft Print to PDF";

    private static bool Enabled =>
        Environment.GetEnvironmentVariable("KV_WAREHOUSE_PRINT_TEST") == "1" && WarehousePrinterService.IsExcelAvailable();

    private static WarehousePreviewRequest Request(WarehouseExportResult result, string template, int templateRows,
        string? printer = null) => new()
    {
        WorkbookPath = result.OutputPath,
        Layout = result.Layout,
        TemplatePath = template,
        TemplateRowCount = templateRows,
        PrinterName = printer,
        PdfFolder = Path.Combine(TestData.NewTempDir(), "preview"),
        FontDescription = "test"
    };

    private static HashSet<int> ExcelPids() =>
        Process.GetProcessesByName("EXCEL").Select(p => p.Id).ToHashSet();

    /// <summary>
    /// Không còn tiến trình Excel mới nào. Cho vài giây: khi máy bận (cả bộ
    /// test chạy song song) tiến trình vừa thoát vẫn có thể còn trong danh
    /// sách một lúc.
    /// </summary>
    private static void AssertNoExcelLeftBehind(HashSet<int> before)
    {
        HashSet<int> extra = new();
        for (int i = 0; i < 20; i++)
        {
            extra = ExcelPids();
            extra.ExceptWith(before);
            if (extra.Count == 0)
                return;
            Thread.Sleep(250);
        }

        Assert.Fail("Tiến trình Excel còn lại sau 5 giây: " + string.Join("; ", extra.Select(Describe)));
    }

    [Fact]
    public async Task ExcelSession_TracksItsOwnProcess_AndItIsGoneWhenDone()
    {
        if (!Enabled)
            return;

        int pid = await ExcelAutomation.RunAsync(session => session.ProcessId, TimeSpan.FromMinutes(1), "test",
            WarehouseErrorKind.PreviewFailed);

        Assert.True(pid > 0, "Không xác định được tiến trình Excel của phiên");
        // RunAsync chỉ trả kết quả khi phiên đã đóng: tiến trình phải đã thoát (hoặc đã bị dừng).
        Assert.Throws<ArgumentException>(() =>
        {
            using Process p = Process.GetProcessById(pid);
            if (p.HasExited)
                throw new ArgumentException("đã thoát");
        });
    }

    private static string Describe(int pid)
    {
        try
        {
            using Process p = Process.GetProcessById(pid);
            return $"{pid} bắt đầu {p.StartTime:HH:mm:ss.fff} (bây giờ {DateTime.Now:HH:mm:ss.fff}) cửa sổ \"{p.MainWindowTitle}\"";
        }
        catch (ArgumentException)
        {
            return $"{pid} (đã thoát)";
        }
    }

    [Fact]
    public async Task Preview_SmallPhieu_IsOnePage_DoesNotModifyFiles_NorLeaveExcel()
    {
        if (!Enabled)
            return;

        HashSet<int> excelBefore = ExcelPids();
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "XK1.xlsx"), 3);
        string templateHash = WarehouseTestData.Sha256(template);
        string outputHash = WarehouseTestData.Sha256(result.OutputPath);
        string defaultPrinter = new WarehousePrinterService().GetWindowsDefaultPrinter();

        WarehousePrintPreview preview = await new WarehousePrintPreviewService().BuildAsync(Request(result, template, 2));

        Assert.Equal(1, preview.PageCount);
        Assert.Equal(1, preview.TemplatePageCount);
        using (PdfPageRenderer pdf = PdfPageRenderer.Open(preview.PdfPath))
        {
            Assert.Equal(1, pdf.PageCount);
            SizeF size = pdf.GetPageSizePoints(0);
            Assert.Equal(595, size.Width, 0); // A4 dọc như mẫu
            Assert.Equal(842, size.Height, 0);
        }

        WarehousePreviewPage page = Assert.Single(preview.Pages);
        Assert.Contains("dòng hàng 1–3", page.Content);
        Assert.Contains("cuối phiếu", page.Content);
        Assert.DoesNotContain(preview.Issues, i => i.Message.Contains("bị cắt"));
        Assert.True(preview.IsStillCurrent());
        Assert.Equal(templateHash, WarehouseTestData.Sha256(template));
        Assert.Equal(outputHash, WarehouseTestData.Sha256(result.OutputPath));
        Assert.Equal(defaultPrinter, new WarehousePrinterService().GetWindowsDefaultPrinter());
        AssertNoExcelLeftBehind(excelBefore);
    }

    [Fact]
    public async Task Preview_ManyRows_UsesExcelPagination_AndMapsEveryRowToAPage()
    {
        if (!Enabled)
            return;

        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        const int rows = 90;
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "XK90.xlsx"), rows);

        WarehousePrintPreview preview = await new WarehousePrintPreviewService().BuildAsync(Request(result, template, 2));

        Assert.True(preview.PageCount >= 3, $"90 dòng hàng (mỗi dòng ~47pt) phải nhiều trang, Excel báo {preview.PageCount}");
        using (PdfPageRenderer pdf = PdfPageRenderer.Open(preview.PdfPath))
            Assert.Equal(preview.PageCount, pdf.PageCount);

        Assert.Equal(preview.PageCount, preview.Pages.Count);
        Assert.Contains("đầu phiếu", preview.Pages[0].Content);
        Assert.Contains("cuối phiếu", preview.Pages[^1].Content);
        for (int i = 1; i < preview.Pages.Count; i++)
            Assert.Equal(preview.Pages[i - 1].LastRow + 1, preview.Pages[i].FirstRow);

        // Bảng kéo sang trang sau → có thông tin (không phải lỗi) cho từng chỗ ngắt.
        Assert.Contains(preview.Issues, i => i.Level == WarehousePreviewIssueLevel.Info && i.Message.Contains("tiếp tục sang trang 2"));

        WarehousePreviewNavigator nav = new(preview.PageCount);
        nav.Last();
        Assert.Equal($"Trang {preview.PageCount} / {preview.PageCount}", nav.Label);
    }

    [Fact]
    public async Task Preview_FixedHeightRows_WarnsThatLongNamesAreClipped()
    {
        if (!Enabled)
            return;

        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir, fixedRowHeight: 16);
        const string longName = "Giày nữ, mũ giày bằng da tổng hợp, đế bằng cao su, mã PU-2024-ABC, size 36-39, màu đen phối trắng";
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "XKcut.xlsx"), 3, longName: longName);

        WarehousePrintPreview preview = await new WarehousePrintPreviewService().BuildAsync(Request(result, template, 2));

        Assert.Equal(3, preview.Issues.Count(i => i.Level == WarehousePreviewIssueLevel.Warning && i.Message.Contains("có thể bị cắt")));
        Assert.All(preview.Issues.Where(i => i.Message.Contains("bị cắt")), i => Assert.Equal(1, i.Page));
    }

    [Fact]
    public async Task Preview_ReflectsFontChange_MorePagesWithBiggerText()
    {
        if (!Enabled)
            return;

        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        WarehousePrintPreviewService service = new();

        WarehouseExportResult normal = WarehouseRows.Export(template, Path.Combine(dir, "a.xlsx"), 30);
        WarehouseExportResult big = WarehouseRows.Export(template, Path.Combine(dir, "b.xlsx"), 30, font: new WarehouseFontSettings
        {
            Scope = WarehouseFontApplyScope.WholeDocument, FontFamily = "Times New Roman", FontSize = 24
        });

        WarehousePrintPreview a = await service.BuildAsync(Request(normal, template, 2));
        WarehousePrintPreview b = await service.BuildAsync(Request(big, template, 2));
        Assert.True(b.PageCount > a.PageCount, $"cỡ 24pt: {b.PageCount} trang, giữ mẫu: {a.PageCount} trang");
    }

    [Fact]
    public async Task Preview_PaginatesWithSelectedPrinter_AndPrintOutputsOnlyThePhieuSheet()
    {
        WarehousePrinterService printers = new();
        if (!Enabled || !printers.IsPrinterAvailable(PdfPrinter))
            return;

        HashSet<int> excelBefore = ExcelPids();
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "XK-print.xlsx"), 40);
        string defaultPrinter = printers.GetWindowsDefaultPrinter();

        WarehousePrintPreview preview = await new WarehousePrintPreviewService().BuildAsync(Request(result, template, 2, PdfPrinter));
        Assert.StartsWith(PdfPrinter, preview.PaginationPrinter);
        Assert.Equal(defaultPrinter, printers.GetWindowsDefaultPrinter());

        // In ra máy in PDF: chỉ sheet phiếu (file mẫu còn sheet "Ghi chú" — không được in kèm).
        string printed = Path.Combine(dir, "printed.pdf");
        await printers.PrintWorkbookAsync(result.OutputPath, PdfPrinter, printToFile: printed, sheetName: result.SheetName);
        int printedPages = await WaitForPdfPagesAsync(printed);
        Assert.Equal(preview.PageCount, printedPages);
        Assert.Equal(defaultPrinter, printers.GetWindowsDefaultPrinter());
        AssertNoExcelLeftBehind(excelBefore);
    }

    /// <summary>
    /// Mẫu thật đặt khổ Letter. Đo trên máy phát triển: Excel phân trang theo
    /// MÁY IN — với "Microsoft Print to PDF" trang in là Letter; với máy in chỉ
    /// cài khổ A4 thì Excel in trên A4. Bản xem trước phải cho thấy đúng khổ
    /// thực tế và cảnh báo khi khác khổ đặt trong file.
    /// </summary>
    [Theory]
    [InlineData("Kho HCM", "Phiếu Xuất Kho.xlsx")]
    [InlineData("Đồng Khởi", "Phiếu Xuất Kho.xlsx")]
    public async Task Preview_RealTemplate_ShowsActualPrintedPaper(string folder, string file)
    {
        string template = Path.Combine(WarehouseTestData.RealTemplatesFolder, folder, file);
        if (!Enabled || !File.Exists(template))
            return;

        string dir = TestData.NewTempDir();
        string hash = WarehouseTestData.Sha256(template);
        WarehouseTemplateLayout layout = new WarehouseExcelReader().ReadTemplateLayout(template);
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "XKreal.xlsx"), layout.DataRowCount + 15);
        WarehousePrintPreviewService service = new();

        foreach (string? printer in new[] { null, new WarehousePrinterService().IsPrinterAvailable(PdfPrinter) ? PdfPrinter : null }.Distinct())
        {
            WarehousePrintPreview preview = await service.BuildAsync(Request(result, template, layout.DataRowCount, printer));
            Assert.NotNull(preview.TemplatePageCount);
            using PdfPageRenderer pdf = PdfPageRenderer.Open(preview.PdfPath);
            Assert.Equal(preview.PageCount, pdf.PageCount);
            Assert.Equal(preview.PageCount, preview.Pages.Count);

            SizeF first = pdf.GetPageSizePoints(0);
            for (int i = 1; i < pdf.PageCount; i++)
                Assert.Equal(first, pdf.GetPageSizePoints(i));

            bool isLetter = Math.Abs(first.Width - 612) < 4 && Math.Abs(first.Height - 792) < 4;
            Assert.Equal(!isLetter, preview.Issues.Any(i => i.Message.Contains("File đặt khổ Letter")));
            if (printer == PdfPrinter)
                Assert.True(isLetter, $"Máy in PDF có khổ Letter, trang in là {first}");
        }

        Assert.Equal(hash, WarehouseTestData.Sha256(template));
    }

    [Fact]
    public async Task Preview_PhieuWiderThanPaper_WarnsAboutColumnsOnSeparatePage()
    {
        if (!Enabled)
            return;

        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir, nameColumnChars: 70);
        WarehouseExportResult result = WarehouseRows.Export(template, Path.Combine(dir, "XKwide.xlsx"), 3);

        WarehousePrintPreview preview = await new WarehousePrintPreviewService().BuildAsync(Request(result, template, 2));

        Assert.True(preview.PageCount >= 2);
        Assert.Contains(preview.Issues, i => i.Level == WarehousePreviewIssueLevel.Warning && i.Message.Contains("rộng hơn trang giấy"));

        // "Vừa chiều rộng trang" sửa được đúng lỗi này (người dùng chủ động chọn, không tự áp dụng).
        WarehouseExportResult fitted = WarehouseRows.Export(template, Path.Combine(dir, "XKfit.xlsx"), 3,
            new WarehousePageSettings { Scale = new WarehouseScaleChoice(WarehouseScaleMode.FitWidth) });
        WarehousePrintPreview fittedPreview = await new WarehousePrintPreviewService().BuildAsync(Request(fitted, template, 2));
        Assert.Equal(1, fittedPreview.PageCount);
        Assert.DoesNotContain(fittedPreview.Issues, i => i.Message.Contains("rộng hơn trang giấy"));
    }

    private static async Task<int> WaitForPdfPagesAsync(string path)
    {
        // Excel trả về khi đã giao lệnh cho hàng đợi in; spooler còn đang ghi file PDF.
        for (int i = 0; i < 120; i++)
        {
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length > 0)
                {
                    using PdfPageRenderer pdf = PdfPageRenderer.Open(path);
                    return pdf.PageCount;
                }
            }
            catch (WarehouseException)
            {
                // File chưa ghi xong.
            }

            await Task.Delay(500);
        }

        throw new TimeoutException("Máy in PDF không tạo file sau 60 giây.");
    }
}
