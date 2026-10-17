using System.Runtime.ExceptionServices;
using KiotVietLabelPrinter.Forms.Warehouse;
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

    /// <summary>
    /// Nhiều phiếu: PHIẾU SAU / PHIẾU TRƯỚC thay tài liệu ngay trong cửa sổ (số
    /// trang theo phiếu mới), không cần đóng cửa sổ; IN TẤT CẢ chỉ bật khi CanPrint.
    /// Phiếu dựng lỗi (Load trả null) thì ở lại phiếu đang xem.
    /// </summary>
    [Fact]
    public void Tickets_NextPrevious_SwapDocumentInPlace()
    {
        string dir = TestData.NewTempDir();
        string[] pdfs = [Path.Combine(dir, "a.pdf"), Path.Combine(dir, "b.pdf"), Path.Combine(dir, "c.pdf")];
        File.WriteAllBytes(pdfs[0], PdfPageRendererTests.MinimalPdf((612, 792), (612, 792)));
        File.WriteAllBytes(pdfs[1], PdfPageRendererTests.MinimalPdf((612, 792), (612, 792), (612, 792)));
        File.WriteAllBytes(pdfs[2], PdfPageRendererTests.MinimalPdf((612, 792)));
        int[] pages = [2, 3, 1];
        HashSet<int> seen = [0];
        bool failThird = true;

        Exception? unhandled = null;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) => unhandled = e.Exception;
            try
            {
                WarehousePreviewTickets tickets = new()
                {
                    Names = ["XK023510 · THÙNG 1", "XK023512 · THÙNG 2", "XK023519 · THÙNG 3"],
                    StartIndex = 0,
                    Load = async (_, index) =>
                    {
                        await Task.Delay(20);
                        if (index == 2 && failThird)
                            return null;
                        seen.Add(index);
                        return PreviewOf(pdfs[index], pages[index]);
                    },
                    CanPrint = () => seen.Count == 3,
                    Hint = () => $"Đã xem {seen.Count}/3"
                };
                using FormWarehousePrintPreview form = new(PreviewOf(pdfs[0], 2), _ => Task.FromResult(true), "", tickets)
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(0, 0),
                    ShowInTaskbar = false
                };
                form.Shown += async (_, _) =>
                {
                    try
                    {
                        Button next = Find<Button>(form, "PHIẾU SAU");
                        Button previous = Find<Button>(form, "PHIẾU TRƯỚC");
                        Button printAll = Find<Button>(form, "IN TẤT CẢ");
                        Label ticket = Find<Label>(form, "Phiếu 1/3: XK023510 · THÙNG 1");
                        Label page = Find<Label>(form, "Trang 1 / 2");
                        Assert.False(previous.Enabled);
                        Assert.False(printAll.Enabled);

                        next.PerformClick();
                        await WaitFor(() => ticket.Text.StartsWith("Phiếu 2/3"));
                        Assert.Equal("Trang 1 / 3", page.Text);
                        Assert.Contains("b.pdf", form.Text);

                        // Phiếu 3 dựng lỗi: ở lại phiếu 2.
                        next.PerformClick();
                        await Task.Delay(300);
                        Assert.StartsWith("Phiếu 2/3", ticket.Text);
                        Assert.Equal("Trang 1 / 3", page.Text);

                        failThird = false;
                        next.PerformClick();
                        await WaitFor(() => ticket.Text.StartsWith("Phiếu 3/3"));
                        Assert.Equal("Trang 1 / 1", page.Text);
                        Assert.False(next.Enabled);
                        Assert.True(printAll.Enabled);

                        // Ảnh chụp để xem bằng mắt khi cần (cùng biến môi trường với test bố cục màn hình phiếu).
                        if (Environment.GetEnvironmentVariable("WAREHOUSE_LAYOUT_SHOTS") is { Length: > 0 } shots)
                        {
                            Directory.CreateDirectory(shots);
                            using Bitmap bitmap = new(form.Width, form.Height);
                            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                            bitmap.Save(Path.Combine(shots, "preview-tickets.png"));
                        }

                        previous.PerformClick();
                        await WaitFor(() => ticket.Text.StartsWith("Phiếu 2/3"));
                        Assert.Equal("Trang 1 / 3", page.Text);
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        form.Close();
                    }
                };
                Application.Run(form);
            }
            catch (Exception ex)
            {
                failure ??= ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromMinutes(1)), "Cửa sổ xem trước không đóng được.");

        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.Null(unhandled);
    }

    private static T Find<T>(Control root, string text) where T : Control =>
        All(root).OfType<T>().First(c => c.Text == text);

    private static IEnumerable<Control> All(Control root) =>
        root.Controls.Cast<Control>().SelectMany(c => All(c).Prepend(c));

    private static async Task WaitFor(Func<bool> condition)
    {
        for (int i = 0; i < 100 && !condition(); i++)
            await Task.Delay(50);
        Assert.True(condition(), "Hết thời gian chờ chuyển phiếu.");
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
