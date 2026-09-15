using System.Runtime.InteropServices;
using SkiaSharp;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Phiên sửa mask thủ công cho MỘT ảnh đã xử lý xong.
//
// Lý do tồn tại: BiRefNet là model *salient object detection*, nên với ảnh chụp
// lộn xộn nó thỉnh thoảng gộp nhầm cả mảng nền lớn vào vật thể (đo trên ảnh mũ:
// một mảng bàn gỗ ~11.000 px nằm trong output THÔ của model ở alpha 254/255).
// Đó là lỗi ngữ nghĩa — không sửa được bằng cách phóng to ảnh, cắt sát chủ thể
// hay chạy lại nhiều lần (đã đo: lần lượt vô tác dụng / tệ hơn / phá biên). Cách
// chắc chắn duy nhất là cho người dùng quét cọ vài giây lên đúng mask.
//
// Toàn bộ thao tác chạy trên alpha full-res đã cache, KHÔNG chạy lại inference.
//
// Không thread-safe — chỉ dùng từ UI thread.
public sealed class MaskEditSession
{
    private const int MaxUndo = 12;

    // Màu foreground đã khử nhiễm biên, đầy đủ ở mọi pixel (w*h*3).
    private readonly byte[] _rgb;

    // Mask AI ban đầu — giữ để "Đặt lại".
    private readonly byte[] _alphaAi;

    // Mask đang sửa.
    private readonly byte[] _alpha;

    private readonly List<byte[]> _undo = new();

    public int Width { get; }
    public int Height { get; }
    public bool IsDirty { get; private set; }
    public bool CanUndo => _undo.Count > 0;

    private MaskEditSession(int width, int height, byte[] rgb, byte[] alpha)
    {
        Width = width;
        Height = height;
        _rgb = rgb;
        _alphaAi = alpha;
        _alpha = (byte[])alpha.Clone();
    }

    // Nạp từ 2 file cache do BackgroundRemoverView ghi sau khi xử lý:
    //   foregroundPath : PNG ĐỤC, màu foreground đã khử nhiễm.
    //   maskPath       : PNG gray8, alpha cuối cùng.
    public static MaskEditSession Load(string foregroundPath, string maskPath)
    {
        using SKBitmap fg = DecodeOpaqueRgba(foregroundPath);
        using SKBitmap mask = DecodeGray(maskPath);

        if (fg.Width != mask.Width || fg.Height != mask.Height)
            throw new InvalidDataException("Foreground và mask lệch kích thước.");

        int w = fg.Width;
        int h = fg.Height;

        byte[] rgb = new byte[w * h * 3];
        byte[] src = fg.Bytes;
        int rowBytes = fg.RowBytes;
        for (int y = 0; y < h; y++)
        {
            int srcRow = y * rowBytes;
            int dstRow = y * w * 3;
            for (int x = 0; x < w; x++)
            {
                int si = srcRow + (x * 4);
                int di = dstRow + (x * 3);
                rgb[di] = src[si];
                rgb[di + 1] = src[si + 1];
                rgb[di + 2] = src[si + 2];
            }
        }

        byte[] alpha = new byte[w * h];
        byte[] mSrc = mask.Bytes;
        int mRow = mask.RowBytes;
        for (int y = 0; y < h; y++)
        {
            int srcRow = y * mRow;
            int dstRow = y * w;
            for (int x = 0; x < w; x++)
                alpha[dstRow + x] = mSrc[srcRow + x];
        }

        return new MaskEditSession(w, h, rgb, alpha);
    }

    // ---- chỉnh sửa ----

    // Gọi MỘT lần khi bắt đầu một nét cọ (mouse down) — cả nét là một bước hoàn tác.
    public void BeginStroke()
    {
        _undo.Add((byte[])_alpha.Clone());
        if (_undo.Count > MaxUndo)
            _undo.RemoveAt(0);
    }

    // Tô một chấm cọ tại (cx, cy) theo TỌA ĐỘ ẢNH. foreground = true để giữ lại
    // (alpha -> 255), false để xóa về nền (alpha -> 0).
    //
    // Lõi cọ đặc, chỉ mềm ở 15% ngoài cùng: sửa mask cần dứt khoát (xóa hẳn mảng
    // nền sót), nhưng vẫn cần rìa mềm để chỗ vá không lộ răng cưa khi composite.
    public bool Paint(float cx, float cy, float radius, bool foreground)
    {
        if (radius < 0.5f)
            return false;

        int x0 = Math.Max(0, (int)Math.Floor(cx - radius));
        int x1 = Math.Min(Width - 1, (int)Math.Ceiling(cx + radius));
        int y0 = Math.Max(0, (int)Math.Floor(cy - radius));
        int y1 = Math.Min(Height - 1, (int)Math.Ceiling(cy + radius));
        if (x0 > x1 || y0 > y1)
            return false;

        byte target = foreground ? (byte)255 : (byte)0;
        float rSq = radius * radius;
        float solid = radius * 0.85f;
        float solidSq = solid * solid;
        bool changed = false;

        for (int y = y0; y <= y1; y++)
        {
            float dy = y - cy;
            int row = y * Width;
            for (int x = x0; x <= x1; x++)
            {
                float dx = x - cx;
                float dSq = (dx * dx) + (dy * dy);
                if (dSq > rSq)
                    continue;

                float weight;
                if (dSq <= solidSq)
                {
                    weight = 1f;
                }
                else
                {
                    float d = (float)Math.Sqrt(dSq);
                    weight = 1f - ((d - solid) / Math.Max(0.001f, radius - solid));
                }

                int i = row + x;
                byte cur = _alpha[i];
                byte next = (byte)(cur + ((target - cur) * weight) + 0.5f);
                if (next != cur)
                {
                    _alpha[i] = next;
                    changed = true;
                }
            }
        }

        if (changed)
            IsDirty = true;

        return changed;
    }

    public bool Undo()
    {
        if (_undo.Count == 0)
            return false;

        byte[] prev = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Array.Copy(prev, _alpha, _alpha.Length);
        IsDirty = !AlphaMatchesAi();
        return true;
    }

    // Bỏ mọi chỉnh sửa, quay về đúng mask AI.
    public void Reset()
    {
        if (AlphaMatchesAi())
            return;

        _undo.Add((byte[])_alpha.Clone());
        if (_undo.Count > MaxUndo)
            _undo.RemoveAt(0);

        Array.Copy(_alphaAi, _alpha, _alpha.Length);
        IsDirty = false;
    }

    private bool AlphaMatchesAi()
    {
        for (int i = 0; i < _alpha.Length; i++)
        {
            if (_alpha[i] != _alphaAi[i])
                return false;
        }
        return true;
    }

    // ---- dựng layer hiển thị ----

    public SKBitmap RenderMask()
    {
        SKBitmap bmp = new(new SKImageInfo(Width, Height, SKColorType.Gray8, SKAlphaType.Opaque));
        CopyRows(_alpha, bmp, 1);
        return bmp;
    }

    // Foreground + alpha hiện tại, straight alpha — để vẽ trên nền caro.
    public SKBitmap RenderCutout()
    {
        SKImageInfo info = new(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap bmp = new(info);
        byte[] px = new byte[Height * info.RowBytes];

        for (int y = 0; y < Height; y++)
        {
            int dstRow = y * info.RowBytes;
            int aRow = y * Width;
            int fgRow = y * Width * 3;
            for (int x = 0; x < Width; x++)
            {
                int di = dstRow + (x * 4);
                int fi = fgRow + (x * 3);
                px[di] = _rgb[fi];
                px[di + 1] = _rgb[fi + 1];
                px[di + 2] = _rgb[fi + 2];
                px[di + 3] = _alpha[aRow + x];
            }
        }

        Marshal.Copy(px, 0, bmp.GetPixels(), px.Length);
        return bmp;
    }

    // out = F·a + 255·(1−a) — giống hệt BgRemovalPipeline.CompositeOnWhite.
    public SKBitmap RenderOnWhite()
    {
        SKImageInfo info = new(Width, Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        SKBitmap bmp = new(info);
        byte[] px = new byte[Height * info.RowBytes];

        for (int y = 0; y < Height; y++)
        {
            int dstRow = y * info.RowBytes;
            int aRow = y * Width;
            int fgRow = y * Width * 3;
            for (int x = 0; x < Width; x++)
            {
                float av = _alpha[aRow + x] / 255f;
                float inv = 1f - av;
                int di = dstRow + (x * 4);
                int fi = fgRow + (x * 3);

                px[di] = Clamp((_rgb[fi] * av) + (255f * inv));
                px[di + 1] = Clamp((_rgb[fi + 1] * av) + (255f * inv));
                px[di + 2] = Clamp((_rgb[fi + 2] * av) + (255f * inv));
                px[di + 3] = 255;
            }
        }

        Marshal.Copy(px, 0, bmp.GetPixels(), px.Length);
        return bmp;
    }

    // ---- ghi ra đĩa ----

    // Ghi đè ảnh kết quả bằng mask đã sửa. Ghi ra file tạm rồi File.Move để không
    // để lại file hỏng nếu tiến trình chết giữa chừng.
    public void SaveResult(string outputPath)
    {
        using SKBitmap composited = RenderOnWhite();
        WritePngAtomic(composited, outputPath);
    }

    // Cập nhật lại mask cache để lần chọn ảnh sau Preview hiện đúng mask đã sửa.
    public void SaveMask(string maskPath)
    {
        using SKBitmap mask = RenderMask();
        WritePngAtomic(mask, maskPath);
    }

    private static void WritePngAtomic(SKBitmap bitmap, string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        string temp = path + ".tmp";
        using (SKImage image = SKImage.FromBitmap(bitmap))
        using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
        using (FileStream fs = File.Create(temp))
            data.SaveTo(fs);

        File.Move(temp, path, overwrite: true);
    }

    // ---- tiện ích ----

    private static byte Clamp(float v) => (byte)(v < 0f ? 0f : v > 255f ? 255f : v + 0.5f);

    private void CopyRows(byte[] src, SKBitmap dst, int bytesPerPixel)
    {
        int rowBytes = dst.RowBytes;
        int lineLength = Width * bytesPerPixel;
        byte[] buffer = new byte[Height * rowBytes];
        for (int y = 0; y < Height; y++)
            Array.Copy(src, y * lineLength, buffer, y * rowBytes, lineLength);
        Marshal.Copy(buffer, 0, dst.GetPixels(), buffer.Length);
    }

    // Giải mã ép ĐỤC — foreground cache không có alpha, đọc kiểu gì cũng giữ
    // nguyên màu.
    private static SKBitmap DecodeOpaqueRgba(string path)
    {
        using FileStream fs = File.OpenRead(path);
        using SKBitmap decoded = SKBitmap.Decode(fs)
            ?? throw new InvalidDataException($"Không đọc được ảnh foreground: {path}");

        SKImageInfo info = new(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        SKBitmap result = new(info);
        if (!decoded.ScalePixels(result, new SKSamplingOptions(SKFilterMode.Nearest)))
        {
            result.Dispose();
            throw new InvalidOperationException("Chuyển foreground sang RGBA thất bại.");
        }

        return result;
    }

    private static SKBitmap DecodeGray(string path)
    {
        using FileStream fs = File.OpenRead(path);
        using SKBitmap decoded = SKBitmap.Decode(fs)
            ?? throw new InvalidDataException($"Không đọc được mask: {path}");

        SKImageInfo info = new(decoded.Width, decoded.Height, SKColorType.Gray8, SKAlphaType.Opaque);
        SKBitmap result = new(info);
        if (!decoded.ScalePixels(result, new SKSamplingOptions(SKFilterMode.Nearest)))
        {
            result.Dispose();
            throw new InvalidOperationException("Chuyển mask sang Gray8 thất bại.");
        }

        return result;
    }
}
