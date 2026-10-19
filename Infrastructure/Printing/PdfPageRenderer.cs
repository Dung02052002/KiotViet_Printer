using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Services.Warehouse;

namespace KiotVietLabelPrinter.Infrastructure.Printing;

/// <summary>
/// Dựng ảnh từng trang của một file PDF bằng PDFium (pdfium.dll từ gói
/// bblanchon.PDFium.Win32, giấy phép Apache-2.0). Dùng cho Xem trước bản in:
/// PDF do chính Excel xuất nên mỗi trang ảnh là đúng một trang Excel sẽ in.
/// PDFium không an toàn đa luồng — mọi lời gọi đi qua một khoá chung.
/// </summary>
public sealed class PdfPageRenderer : IDisposable
{
    private static readonly object PdfiumLock = new();
    private static bool _libraryInitialized;

    private readonly byte[] _content;
    private GCHandle _pin;
    private IntPtr _document;

    public int PageCount { get; }

    private PdfPageRenderer(byte[] content, GCHandle pin, IntPtr document, int pageCount)
    {
        _content = content;
        _pin = pin;
        _document = document;
        PageCount = pageCount;
    }

    public static PdfPageRenderer Open(string path)
    {
        byte[] content;
        try
        {
            content = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WarehouseException(WarehouseErrorKind.PreviewFailed, $"Không đọc được file xem trước:\n{path}", ex);
        }

        lock (PdfiumLock)
        {
            EnsureLibrary();

            // PDFium đọc thẳng từ vùng nhớ này suốt thời gian tài liệu còn mở → ghim lại.
            GCHandle pin = GCHandle.Alloc(content, GCHandleType.Pinned);
            IntPtr document = Native.FPDF_LoadMemDocument(pin.AddrOfPinnedObject(), content.Length, null);
            if (document == IntPtr.Zero)
            {
                uint error = Native.FPDF_GetLastError();
                pin.Free();
                throw new WarehouseException(WarehouseErrorKind.PreviewFailed,
                    $"File xem trước không phải PDF hợp lệ (mã lỗi PDFium {error}).");
            }

            return new PdfPageRenderer(content, pin, document, Native.FPDF_GetPageCount(document));
        }
    }

    /// <summary>Kích thước trang theo point (1/72 inch) — đúng khổ giấy Excel đã dùng.</summary>
    public SizeF GetPageSizePoints(int pageIndex)
    {
        lock (PdfiumLock)
        {
            ThrowIfDisposed();
            CheckIndex(pageIndex);
            if (Native.FPDF_GetPageSizeByIndex(_document, pageIndex, out double width, out double height) == 0)
                throw new WarehouseException(WarehouseErrorKind.PreviewFailed, $"Không đọc được kích thước trang {pageIndex + 1}.");
            return new SizeF((float)width, (float)height);
        }
    }

    /// <summary>Dựng ảnh một trang (nền trắng) ở độ phân giải <paramref name="dpi"/>. Người gọi giữ và Dispose ảnh.</summary>
    public Bitmap Render(int pageIndex, float dpi)
    {
        SizeF size = GetPageSizePoints(pageIndex);
        int width = Math.Max(1, (int)Math.Round(size.Width / 72f * dpi));
        int height = Math.Max(1, (int)Math.Round(size.Height / 72f * dpi));

        // PArgb (alpha nhân sẵn) là định dạng GDI+ vẽ nhanh nhất — nhanh hơn Argb nhiều lần
        // khi phóng / thu ảnh trang lớn trên màn hình. Trang được tô nền trắng đục (alpha 255)
        // trước khi dựng nên dữ liệu BGRA của PDFium giống hệt ở hai định dạng.
        Bitmap bitmap = new(width, height, PixelFormat.Format32bppPArgb);
        bitmap.SetResolution(dpi, dpi);
        BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            lock (PdfiumLock)
            {
                ThrowIfDisposed();
                IntPtr page = Native.FPDF_LoadPage(_document, pageIndex);
                if (page == IntPtr.Zero)
                    throw new WarehouseException(WarehouseErrorKind.PreviewFailed, $"Không mở được trang {pageIndex + 1} của bản xem trước.");

                // Bitmap PDFium dùng chung bộ nhớ với ảnh GDI+ (BGRA = Format32bppArgb).
                IntPtr pdfBitmap = Native.FPDFBitmap_CreateEx(width, height, Native.FPDFBitmap_BGRA, data.Scan0, data.Stride);
                try
                {
                    Native.FPDFBitmap_FillRect(pdfBitmap, 0, 0, width, height, 0xFFFFFFFF);
                    Native.FPDF_RenderPageBitmap(pdfBitmap, page, 0, 0, width, height, 0, Native.FPDF_ANNOT | Native.FPDF_PRINTING);
                }
                finally
                {
                    Native.FPDFBitmap_Destroy(pdfBitmap);
                    Native.FPDF_ClosePage(page);
                }
            }
        }
        catch
        {
            bitmap.UnlockBits(data);
            bitmap.Dispose();
            throw;
        }

        bitmap.UnlockBits(data);
        return bitmap;
    }

    public void Dispose()
    {
        lock (PdfiumLock)
        {
            if (_document != IntPtr.Zero)
            {
                Native.FPDF_CloseDocument(_document);
                _document = IntPtr.Zero;
            }

            if (_pin.IsAllocated)
                _pin.Free();
        }

        GC.KeepAlive(_content);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_document == IntPtr.Zero, this);

    private void CheckIndex(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex), pageIndex, $"Tài liệu có {PageCount} trang.");
    }

    private static void EnsureLibrary()
    {
        if (_libraryInitialized)
            return;

        try
        {
            Native.FPDF_InitLibrary();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new WarehouseException(WarehouseErrorKind.PreviewFailed,
                "Thiếu thư viện dựng ảnh PDF (pdfium.dll) trong thư mục cài đặt — hãy cài lại phần mềm.", ex);
        }

        _libraryInitialized = true;
    }

    private static class Native
    {
        private const string Library = "pdfium";

        public const int FPDFBitmap_BGRA = 4;
        public const int FPDF_ANNOT = 0x01;
        public const int FPDF_PRINTING = 0x800;

        [DllImport(Library)]
        public static extern void FPDF_InitLibrary();

        [DllImport(Library)]
        public static extern uint FPDF_GetLastError();

        [DllImport(Library, CharSet = CharSet.Ansi)]
        public static extern IntPtr FPDF_LoadMemDocument(IntPtr dataBuffer, int size, string? password);

        [DllImport(Library)]
        public static extern void FPDF_CloseDocument(IntPtr document);

        [DllImport(Library)]
        public static extern int FPDF_GetPageCount(IntPtr document);

        [DllImport(Library)]
        public static extern int FPDF_GetPageSizeByIndex(IntPtr document, int pageIndex, out double width, out double height);

        [DllImport(Library)]
        public static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

        [DllImport(Library)]
        public static extern void FPDF_ClosePage(IntPtr page);

        [DllImport(Library)]
        public static extern IntPtr FPDFBitmap_CreateEx(int width, int height, int format, IntPtr firstScan, int stride);

        [DllImport(Library)]
        public static extern int FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

        [DllImport(Library)]
        public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX,
            int sizeY, int rotate, int flags);

        [DllImport(Library)]
        public static extern void FPDFBitmap_Destroy(IntPtr bitmap);
    }
}
