using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Barcodes;

namespace KiotVietLabelPrinter.Infrastructure.Printing;

/// <summary>
/// Kết quả so kích thước tem lớn nhất với vùng in được của khổ giấy đang chọn.
/// Không đọc được vùng in (máy in không báo) thì coi như vừa.
/// </summary>
public sealed record PageFitCheck(
    bool Fits,
    double LabelWidthMm,
    double LabelHeightMm,
    double PrintableWidthMm,
    double PrintableHeightMm);

// In tem của "Tạo mã vạch" trực tiếp ra máy in Windows (không qua
// BarTender). Mỗi dòng hàng hoá (Tên hàng + Mã hàng) đúng một tem / một trang.
// Ảnh được vẽ LẠI theo đúng độ phân giải của máy in và đặt 1 pixel ảnh = 1
// điểm in, nên vạch không bị nội suy/kéo giãn — kích thước thật đúng như
// thiết lập (mm).
// Hộp thoại chọn máy in và hỏi xác nhận thuộc về màn hình (xem
// BarcodeGeneratorView.PrintLabels) — service này không hiện UI.
public class BarcodePrintService
{
    private readonly Code128BarcodeService _barcodeService = new();

    public PrintDocument CreateDocument(IReadOnlyList<BarcodeLabelItem> items) => new()
    {
        DocumentName = items.Count == 1 ? $"Mã vạch {items[0].Code}" : $"Mã vạch ({items.Count} tem)",
        OriginAtMargins = false
    };

    // Vẽ thử TẤT CẢ tem trước khi gửi lệnh in (500 tem chỉ mất cỡ 0,2 giây):
    // tem nào lỗi bố cục thì báo ngay, không để lệnh in dừng giữa chừng làm
    // thiếu tem. Đồng thời lấy kích thước tem lớn nhất để so với khổ giấy.
    // Không tự thu nhỏ tem cho vừa giấy (sẽ làm sai tỷ lệ vạch) — chỉ báo lại.
    public PageFitCheck CheckFitsPage(PrintDocument document, IReadOnlyList<BarcodeLabelItem> items, BarcodeRenderOptions options)
    {
        double maxWidthMm = 0;
        double maxHeightMm = 0;
        for (int i = 0; i < items.Count; i++)
        {
            try
            {
                using BarcodeImage probe = _barcodeService.Render(items[i].Code, items[i].Name, options);
                maxWidthMm = Math.Max(maxWidthMm, probe.WidthMm);
                maxHeightMm = Math.Max(maxHeightMm, probe.HeightMm);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Tem thứ {i + 1} (mã \"{items[i].Code}\") không tạo được, chưa gửi lệnh in tem nào.\n{ex.Message}", ex);
            }
        }

        RectangleF printable = document.DefaultPageSettings.PrintableArea; // 1/100 inch
        if (printable.Width <= 0 || printable.Height <= 0)
            return new PageFitCheck(true, maxWidthMm, maxHeightMm, 0, 0);

        double printableWmm = printable.Width * 0.254;
        double printableHmm = printable.Height * 0.254;
        bool fits = maxWidthMm <= printableWmm + 0.5 && maxHeightMm <= printableHmm + 0.5;
        return new PageFitCheck(fits, maxWidthMm, maxHeightMm, printableWmm, printableHmm);
    }

    /// <summary>Gửi lệnh in: mỗi tem một trang, theo máy in đã chọn trên document.</summary>
    public void Print(PrintDocument document, IReadOnlyList<BarcodeLabelItem> items, BarcodeRenderOptions options)
    {
        int index = 0;
        document.PrintPage += (_, e) =>
        {
            Graphics g = e.Graphics!;
            int dpi = (int)Math.Round(g.DpiX);

            BarcodeRenderOptions printOptions = options.Clone();
            printOptions.Dpi = dpi;
            using BarcodeImage image = _barcodeService.Render(items[index].Code, items[index].Name, printOptions);

            g.PageUnit = GraphicsUnit.Pixel;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;

            // Gốc toạ độ (OriginAtMargins = false) là góc vùng in được của máy
            // in; căn tem vào giữa tờ/tem.
            float hardX = e.PageSettings.HardMarginX * dpi / 100f;
            float hardY = e.PageSettings.HardMarginY * dpi / 100f;
            float pageW = e.PageBounds.Width * dpi / 100f;
            float pageH = e.PageBounds.Height * dpi / 100f;

            int x = (int)Math.Max(0, Math.Round(((pageW - image.Bitmap.Width) / 2f) - hardX));
            int y = (int)Math.Max(0, Math.Round(((pageH - image.Bitmap.Height) / 2f) - hardY));

            g.DrawImage(image.Bitmap, new Rectangle(x, y, image.Bitmap.Width, image.Bitmap.Height),
                0, 0, image.Bitmap.Width, image.Bitmap.Height, GraphicsUnit.Pixel);

            index++;
            e.HasMorePages = index < items.Count;
        };

        document.Print();
    }
}
