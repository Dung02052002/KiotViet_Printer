using System.Runtime.InteropServices;
using SkiaSharp;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Dữ liệu phục vụ KIỂM TRA CHẤT LƯỢNG mask trong Preview và SỬA MASK bằng cọ —
// KHÔNG phải file xuất thật.
//
// Sinh ra mỗi lần xử lý khi UI yêu cầu (BgRemovalRequest.Inspect = true); chế độ
// CLI batch bỏ qua để không tốn thời gian/bộ nhớ.
//
// Cả hai bitmap đều lấy từ ĐÚNG dữ liệu cuối cùng dùng để composite:
//   - FinalMask  : alpha map cuối (sau refinement + feather) — chính là alpha
//                  truyền cho CompositeOnWhite.
//   - Foreground : màu foreground đã khử nhiễm màu biên, ĐỤC (không kênh alpha).
//
// Cố ý tách rời thành 2 ảnh thay vì một ảnh RGBA "cutout": PNG RGBA khi giải mã
// lại rất dễ bị đưa về premultiplied alpha, làm mất sạch màu ở vùng alpha = 0.
// Mà đó lại đúng là vùng người dùng cần tô LẠI thành foreground khi sửa mask
// (ví dụ AI cắt nhầm mất một mảng vật thể). Giữ foreground đục thì màu luôn còn
// nguyên ở mọi pixel, ghép với mask lúc nào cũng được.
public sealed class BgRemovalInspection : IDisposable
{
    // Gray8: trắng = foreground (a=1), đen = background (a=0), xám = biên mềm / bán trong suốt.
    public required SKBitmap FinalMask { get; init; }

    // Rgba8888 Opaque: màu foreground đã khử nhiễm biên, đầy đủ ở MỌI pixel.
    public required SKBitmap Foreground { get; init; }

    public int Width => FinalMask.Width;
    public int Height => FinalMask.Height;

    // Dựng từ foreground full-res + alpha full-res. Cạnh dài được giới hạn ở
    // `maxSide` để preview không ngốn bộ nhớ với ảnh rất lớn (cả hai layer thu
    // nhỏ theo cùng công thức nên vẫn khớp pixel khi zoom).
    public static BgRemovalInspection Build(RgbImage foreground, AlphaMap alpha, int maxSide = 3000)
    {
        int w = foreground.Width;
        int h = foreground.Height;
        if (alpha.Width != w || alpha.Height != h)
            throw new ArgumentException("Alpha map phải cùng kích thước foreground.");

        SKImageInfo info = new(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        SKBitmap fg = new(info);
        byte[] fgPx = new byte[h * info.RowBytes];
        byte[] rgb = foreground.Rgb;
        int rowBytes = info.RowBytes;

        for (int y = 0; y < h; y++)
        {
            int dstRow = y * rowBytes;
            int srcRow = y * w * 3;
            for (int x = 0; x < w; x++)
            {
                int di = dstRow + (x * 4);
                int si = srcRow + (x * 3);
                fgPx[di] = rgb[si];
                fgPx[di + 1] = rgb[si + 1];
                fgPx[di + 2] = rgb[si + 2];
                fgPx[di + 3] = 255;
            }
        }

        Marshal.Copy(fgPx, 0, fg.GetPixels(), fgPx.Length);

        SKBitmap mask = alpha.ToGray8Bitmap();

        int longest = Math.Max(w, h);
        if (longest > maxSide)
        {
            float s = (float)maxSide / longest;
            int nw = Math.Max(1, (int)Math.Round(w * s));
            int nh = Math.Max(1, (int)Math.Round(h * s));
            SKSamplingOptions sampling = new(SKCubicResampler.Mitchell);

            SKBitmap? fgSmall = fg.Resize(
                new SKImageInfo(nw, nh, SKColorType.Rgba8888, SKAlphaType.Opaque), sampling);
            SKBitmap? maskSmall = mask.Resize(
                new SKImageInfo(nw, nh, SKColorType.Gray8, SKAlphaType.Opaque), sampling);

            fg.Dispose();
            mask.Dispose();

            if (fgSmall == null || maskSmall == null)
            {
                fgSmall?.Dispose();
                maskSmall?.Dispose();
                throw new InvalidOperationException("Thu nhỏ ảnh kiểm tra mask thất bại.");
            }

            fg = fgSmall;
            mask = maskSmall;
        }

        return new BgRemovalInspection { FinalMask = mask, Foreground = fg };
    }

    public void Dispose()
    {
        FinalMask.Dispose();
        Foreground.Dispose();
    }
}
