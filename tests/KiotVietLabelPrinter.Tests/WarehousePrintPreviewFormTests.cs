using System.Runtime.ExceptionServices;
using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>Cửa sổ xem trước bản in: tài liệu rỗng và đóng cửa sổ giữa lúc đang dựng trang.</summary>
[Collection(WarehouseViewTestHost.Collection)]
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

    [Fact]
    public void ClosingWhileAPageIsRendering_RaisesNoError()
    {
        string pdf = Path.Combine(TestData.NewTempDir(), "pages.pdf");
        File.WriteAllBytes(pdf, PdfPageRendererTests.MinimalPdf((612, 792), (612, 792), (612, 792)));

        Exception? unhandled = null;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => unhandled = e.Exception;
            try
            {
                FormWarehousePrintPreview form = new(PreviewOf(pdf, 3), null, "")
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(0, 0),
                    ShowInTaskbar = false
                };
                // Đóng ngay sau khi cửa sổ bắt đầu dựng trang đầu (Shown của chính form chạy trước).
                form.Shown += (_, _) => form.Close();
                Application.Run(form);

                // Cho các tác vụ dựng trang đang dở chạy xong và trả kết quả về luồng giao diện.
                using System.Windows.Forms.Timer wait = new() { Interval = 800 };
                using ApplicationContext context = new();
                wait.Tick += (_, _) => context.ExitThread();
                wait.Start();
                Application.Run(context);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromMinutes(1)), "Cửa sổ xem trước không đóng được.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.Null(unhandled);
    }
}
