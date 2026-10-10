using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>Cửa sổ xem trước bản in: tài liệu rỗng.</summary>
public class WarehousePrintPreviewFormTests
{
    private static WarehousePrintPreview PreviewOf(string pdfPath, int pages) => new()
    {
        WorkbookPath = pdfPath,
        SourceStamp = WarehouseFileStamp.Capture(pdfPath)!,
        SheetName = "Sheet1",
        PdfPath = pdfPath,
        PageCount = pages,
        RenderedPageCount = pages,
        PaginationPrinter = "test",
        Page = new WarehousePageSetup
        {
            PaperSize = 9,
            Landscape = false,
            Margins = new WarehouseMargins(0.7, 0.7, 0.75, 0.75, 0.3, 0.3),
            FitToPage = false,
            Scale = 100,
            FitWidth = 1,
            FitHeight = 1,
            ManualRowBreakCount = 0,
            HasHeaderFooter = false,
            CenterHorizontally = false,
            CenterVertically = false
        },
        PrintedPaper = "A4",
        FontDescription = "test",
        Pages = new List<WarehousePreviewPage>(),
        Issues = new List<WarehousePreviewIssue>()
    };

    [Fact]
    public void EmptyPdf_IsRejected_InsteadOfShowingAFakePage()
    {
        string pdf = Path.Combine(TestData.NewTempDir(), "empty.pdf");
        File.WriteAllBytes(pdf, PdfPageRendererTests.MinimalPdf());

        WarehouseException ex = Assert.Throws<WarehouseException>(() => new FormWarehousePrintPreview(PreviewOf(pdf, 0), null, ""));
        Assert.Contains("không có trang nào", ex.Message);
    }
}
