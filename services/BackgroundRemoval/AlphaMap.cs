using System.Runtime.InteropServices;
using SkiaSharp;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Mask alpha mềm, một kênh, giá trị [0..1], row-major. Đơn vị dữ liệu chung của
// toàn bộ pipeline refinement (thay cho việc chuyền SKBitmap Gray8 qua từng bước
// và mất độ chính xám ở mỗi lần convert).
public sealed class AlphaMap
{
    public int Width { get; }
    public int Height { get; }
    public float[] Data { get; }

    public AlphaMap(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new float[width * height];
    }

    public AlphaMap(int width, int height, float[] data)
    {
        if (data.Length != width * height)
            throw new ArgumentException("Kích thước data không khớp width*height.");
        Width = width;
        Height = height;
        Data = data;
    }

    public float this[int x, int y]
    {
        get => Data[(y * Width) + x];
        set => Data[(y * Width) + x] = value;
    }

    public AlphaMap Clone() => new(Width, Height, (float[])Data.Clone());

    public void Clamp01()
    {
        float[] d = Data;
        for (int i = 0; i < d.Length; i++)
        {
            float v = d[i];
            d[i] = v < 0f ? 0f : v > 1f ? 1f : v;
        }
    }

    // Resize bằng SkiaSharp qua bitmap Gray8. Cubic (Mitchell) cho phóng to,
    // linear cho thu nhỏ — hành vi khớp code composite hiện có.
    public AlphaMap Resized(int targetW, int targetH)
    {
        if (targetW == Width && targetH == Height)
            return Clone();

        byte[] srcBytes = new byte[Width * Height];
        for (int i = 0; i < srcBytes.Length; i++)
        {
            float v = Data[i] * 255f;
            srcBytes[i] = (byte)(v < 0f ? 0f : v > 255f ? 255f : v + 0.5f);
        }

        using SKBitmap src = new(new SKImageInfo(Width, Height, SKColorType.Gray8, SKAlphaType.Opaque));
        Marshal.Copy(srcBytes, 0, src.GetPixels(), srcBytes.Length);

        // Cubic (Mitchell) cho cả phóng to lẫn thu nhỏ — khớp bộ lọc dùng cho mask
        // ở code composite gốc, biên mask mượt, không ringing đáng kể.
        SKSamplingOptions sampling = new(SKCubicResampler.Mitchell);

        using SKBitmap dst = src.Resize(
                new SKImageInfo(targetW, targetH, SKColorType.Gray8, SKAlphaType.Opaque), sampling)
            ?? throw new InvalidOperationException("Resize alpha map thất bại.");

        byte[] dstBytes = new byte[targetW * targetH];
        Marshal.Copy(dst.GetPixels(), dstBytes, 0, dstBytes.Length);

        float[] outData = new float[dstBytes.Length];
        for (int i = 0; i < dstBytes.Length; i++)
            outData[i] = dstBytes[i] / 255f;

        return new AlphaMap(targetW, targetH, outData);
    }

    public SKBitmap ToGray8Bitmap()
    {
        SKBitmap bmp = new(new SKImageInfo(Width, Height, SKColorType.Gray8, SKAlphaType.Opaque));
        byte[] bytes = new byte[Width * Height];
        for (int i = 0; i < bytes.Length; i++)
        {
            float v = Data[i] * 255f;
            bytes[i] = (byte)(v < 0f ? 0f : v > 255f ? 255f : v + 0.5f);
        }
        Marshal.Copy(bytes, 0, bmp.GetPixels(), bytes.Length);
        return bmp;
    }
}

// Ảnh RGB full-res đóng gói chặt (3 byte / pixel). Dùng cho composite + khử nhiễm
// màu biên ở đúng resolution ảnh gốc.
public sealed class RgbImage
{
    public int Width { get; }
    public int Height { get; }

    // [ (y*Width + x) * 3 + c ], c: 0=R 1=G 2=B, giá trị 0..255.
    public byte[] Rgb { get; }

    public RgbImage(int width, int height)
    {
        Width = width;
        Height = height;
        Rgb = new byte[width * height * 3];
    }

    public static RgbImage FromBitmap(SKBitmap bitmap)
    {
        // Chỉ dispose bản copy do method này tạo — KHÔNG dispose bitmap của caller.
        bool ownsRgba = bitmap.ColorType != SKColorType.Rgba8888;
        SKBitmap rgba = ownsRgba
            ? bitmap.Copy(SKColorType.Rgba8888)
              ?? throw new InvalidOperationException("Chuyển ảnh sang RGBA thất bại.")
            : bitmap;

        try
        {
            return Extract(rgba);
        }
        finally
        {
            if (ownsRgba)
                rgba.Dispose();
        }
    }

    private static RgbImage Extract(SKBitmap rgba)
    {
        int w = rgba.Width;
        int h = rgba.Height;
        RgbImage img = new(w, h);

        byte[] src = rgba.Bytes;
        int rowBytes = rgba.RowBytes;
        byte[] dst = img.Rgb;

        for (int y = 0; y < h; y++)
        {
            int srcRow = y * rowBytes;
            int dstRow = y * w * 3;
            for (int x = 0; x < w; x++)
            {
                int si = srcRow + (x * 4);
                int di = dstRow + (x * 3);
                dst[di] = src[si];
                dst[di + 1] = src[si + 1];
                dst[di + 2] = src[si + 2];
            }
        }

        return img;
    }

    // Ảnh xám [0..1] cùng kích thước — guide cho edge-aware smoothing / decontam.
    public float[] ToGrayNormalized()
    {
        float[] gray = new float[Width * Height];
        for (int p = 0, i = 0; p < gray.Length; p++, i += 3)
            gray[p] = ((0.299f * Rgb[i]) + (0.587f * Rgb[i + 1]) + (0.114f * Rgb[i + 2])) / 255f;
        return gray;
    }
}
