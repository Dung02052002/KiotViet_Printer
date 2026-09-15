using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Models;
using SkiaSharp;

namespace KiotVietLabelPrinter.Services;

// Service cho tính năng "Giảm dung lượng ảnh" — chạy 100% local bằng SkiaSharp
// (đã có sẵn trong project cho Xóa nền ảnh), không API/cloud/upload.
//
// Xử lý TỪNG ảnh một cách độc lập, không giữ ảnh full-res nào ngoài scope của
// Compress(): decode -> resize -> encode -> ghi đĩa -> dispose, rồi mới sang ảnh
// kế tiếp — batch hàng trăm/nghìn ảnh không tích RAM (xem BUỘC #24 trong yêu cầu).
public sealed class ImageCompressionService
{
    // Bảo vệ việc cấp tên file output khi nhiều worker chạy song song cùng lúc
    // (SemaphoreSlim ở View) — không dùng chỉ File.Exists vì có thể 2 luồng
    // cùng thấy tên trống rồi cùng ghi đè nhau.
    private readonly object _pathLock = new();
    private readonly HashSet<string> _reservedPaths = new(StringComparer.OrdinalIgnoreCase);

    public CompressionResult Compress(string inputPath, CompressionOptions options, CancellationToken token)
    {
        Stopwatch sw = Stopwatch.StartNew();
        CompressionResult result = new() { InputPath = inputPath };

        SKBitmap? upright = null;

        try
        {
            long originalBytes = new FileInfo(inputPath).Length;
            result.OriginalBytes = originalBytes;

            token.ThrowIfCancellationRequested();

            upright = DecodeUprightOwned(inputPath);

            result.OriginalWidth = upright.Width;
            result.OriginalHeight = upright.Height;

            token.ThrowIfCancellationRequested();

            (int targetW, int targetH) = ComputeTargetSize(upright.Width, upright.Height, options);

            CompressionPreset preset = CompressionPreset.Get(options.Quality);
            byte[] encoded = EncodeImage(upright, targetW, targetH, options.Format, preset.JpegQuality);

            token.ThrowIfCancellationRequested();

            OutputImageFormat? inputFormat = DetectFormat(inputPath);
            bool sameFormat = inputFormat == options.Format;

            // Cùng format: chỉ dùng bản nén nếu THỰC SỰ nhỏ hơn bản gốc — tránh
            // trả về ảnh "nén" mà lại to hơn (BUỘC #20). Khác format: luôn dùng
            // bản đã convert vì đây là yêu cầu đổi định dạng, không phải nén
            // (BUỘC #21) — không được âm thầm trả về định dạng khác cái người
            // dùng chọn.
            bool useCompressed = !sameFormat || encoded.LongLength < originalBytes;

            Directory.CreateDirectory(options.OutputFolder);

            if (useCompressed)
            {
                string outputPath = ReserveOutputPath(inputPath, options.OutputFolder, options.Format);
                File.WriteAllBytes(outputPath, encoded);

                result.OutputPath = outputPath;
                result.OutputBytes = encoded.LongLength;
                result.OutputWidth = targetW;
                result.OutputHeight = targetH;
                result.OriginalKept = false;
            }
            else
            {
                string outputPath = ReserveOutputPath(inputPath, options.OutputFolder, null);
                File.Copy(inputPath, outputPath, overwrite: false);

                result.OutputPath = outputPath;
                result.OutputBytes = originalBytes;
                result.OutputWidth = upright.Width;
                result.OutputHeight = upright.Height;
                result.OriginalKept = true;
            }

            result.SavedBytes = Math.Max(0, originalBytes - result.OutputBytes);
            result.SavedPercent = originalBytes > 0 ? result.SavedBytes * 100.0 / originalBytes : 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        finally
        {
            upright?.Dispose();
            result.ProcessingTime = sw.Elapsed;
        }

        return result;
    }

    // Đọc ảnh gốc cho hiển thị danh sách (thumbnail + resolution thật) — dùng
    // trong lúc thêm ảnh, TRƯỚC khi bấm "GIẢM DUNG LƯỢNG" (BUỘC #5). Trả về
    // null nếu file lỗi/không đọc được, không throw để không làm hỏng cả batch
    // add vì một file lỗi.
    public static Bitmap? TryDecodeThumbnail(string path, int boxSize, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            using SKBitmap upright = DecodeUprightOwned(path);
            width = upright.Width;
            height = upright.Height;

            float scale = Math.Min((float)boxSize / upright.Width, (float)boxSize / upright.Height);
            int w = Math.Max(1, (int)Math.Round(upright.Width * scale));
            int h = Math.Max(1, (int)Math.Round(upright.Height * scale));

            SKImageInfo canvasInfo = new(boxSize, boxSize, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using SKBitmap canvas = new(canvasInfo);

            using (SKBitmap resized = upright.Resize(
                       new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                       new SKSamplingOptions(SKCubicResampler.Mitchell))
                   ?? throw new InvalidOperationException("Resize thumbnail thất bại."))
            using (SKCanvas c = new(canvas))
            {
                c.Clear(SKColors.White);
                c.DrawBitmap(resized, (boxSize - w) / 2f, (boxSize - h) / 2f);
            }

            return SkiaToBitmap(canvas);
        }
        catch
        {
            return null;
        }
    }

    // Bridge SKBitmap -> System.Drawing.Bitmap để control WinForms
    // (DataGridViewImageColumn, PictureBox) hiển thị được — chép thẳng pixel,
    // không đi vòng qua PNG encode/decode.
    private static Bitmap SkiaToBitmap(SKBitmap source)
    {
        int w = source.Width;
        int h = source.Height;

        Bitmap bmp = new(w, h, PixelFormat.Format32bppArgb);
        BitmapData dst = bmp.LockBits(
            new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            byte[] src = source.Bytes;
            int srcStride = source.RowBytes;
            byte[] row = new byte[dst.Stride];

            for (int y = 0; y < h; y++)
            {
                int srcRow = y * srcStride;
                for (int x = 0; x < w; x++)
                {
                    int di = x * 4;
                    int si = srcRow + (x * 4);
                    row[di] = src[si + 2];
                    row[di + 1] = src[si + 1];
                    row[di + 2] = src[si];
                    row[di + 3] = src[si + 3];
                }

                Marshal.Copy(row, 0, IntPtr.Add(dst.Scan0, y * dst.Stride), dst.Stride);
            }
        }
        finally
        {
            bmp.UnlockBits(dst);
        }

        return bmp;
    }

    // Decode + chỉnh EXIF orientation, gộp lại đúng MỘT bitmap caller sở hữu và
    // phải dispose — che giấu việc ApplyOrientation có thể trả về chính bitmap
    // đầu vào (case TopLeft) hoặc một bitmap mới (case có xoay).
    private static SKBitmap DecodeUprightOwned(string path)
    {
        using FileStream fs = File.OpenRead(path);
        SKBitmap raw = DecodeRaw(fs, out SKEncodedOrigin origin);
        SKBitmap upright = ApplyOrientation(raw, origin);

        if (!ReferenceEquals(upright, raw))
            raw.Dispose();

        return upright;
    }

    private static SKBitmap DecodeRaw(Stream stream, out SKEncodedOrigin origin)
    {
        using SKCodec codec = SKCodec.Create(stream)
            ?? throw new InvalidDataException("Không đọc được ảnh (định dạng không hỗ trợ hoặc file lỗi).");

        origin = codec.EncodedOrigin;

        SKImageInfo info = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap bitmap = new(info);

        SKCodecResult decodeStatus = codec.GetPixels(info, bitmap.GetPixels());
        if (decodeStatus != SKCodecResult.Success && decodeStatus != SKCodecResult.IncompleteInput)
        {
            bitmap.Dispose();
            throw new InvalidDataException($"Không giải mã được ảnh ({decodeStatus}).");
        }

        return bitmap;
    }

    // Chỉnh xoay theo EXIF Orientation (BUỘC #30) — chỉ xử lý 3 trường hợp thực
    // tế xảy ra từ camera điện thoại/máy ảnh (xoay 90/180/270), bỏ qua 4 trường
    // hợp lật gương hiếm gặp (chỉ do một số phần mềm scan tạo ra, không phải
    // camera thật) để tránh dựng sai ma trận biến đổi phức tạp không kiểm chứng
    // được. Trả về CHÍNH bitmap đầu vào (không copy) khi không cần xoay.
    private static SKBitmap ApplyOrientation(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        switch (origin)
        {
            case SKEncodedOrigin.BottomRight: // ảnh chụp ngược 180°
            {
                SKBitmap rotated = new(bitmap.Width, bitmap.Height);
                using SKCanvas canvas = new(rotated);
                canvas.Clear(SKColors.Transparent);
                canvas.RotateDegrees(180, bitmap.Width / 2f, bitmap.Height / 2f);
                canvas.DrawBitmap(bitmap, 0, 0);
                return rotated;
            }
            case SKEncodedOrigin.RightTop: // cầm ngang, xoay 90° CW để dựng thẳng
            {
                SKBitmap rotated = new(bitmap.Height, bitmap.Width);
                using SKCanvas canvas = new(rotated);
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(rotated.Width, 0);
                canvas.RotateDegrees(90);
                canvas.DrawBitmap(bitmap, 0, 0);
                return rotated;
            }
            case SKEncodedOrigin.LeftBottom: // cầm ngang chiều ngược lại, xoay 270° CW
            {
                SKBitmap rotated = new(bitmap.Height, bitmap.Width);
                using SKCanvas canvas = new(rotated);
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(0, rotated.Height);
                canvas.RotateDegrees(270);
                canvas.DrawBitmap(bitmap, 0, 0);
                return rotated;
            }
            default: // TopLeft (bình thường) hoặc lật gương hiếm gặp
                return bitmap;
        }
    }

    // Không bao giờ upscale (BUỘC #10): chỉ co lại khi cạnh dài nhất vượt mốc
    // preset, và bỏ qua hoàn toàn khi "Giữ nguyên kích thước" được bật.
    private static (int Width, int Height) ComputeTargetSize(int width, int height, CompressionOptions options)
    {
        if (options.KeepOriginalDimensions)
            return (width, height);

        int maxEdge = CompressionPreset.Get(options.Quality).MaxEdge;
        int longest = Math.Max(width, height);

        if (longest <= maxEdge)
            return (width, height);

        double scale = (double)maxEdge / longest;
        int w = Math.Max(1, (int)Math.Round(width * scale));
        int h = Math.Max(1, (int)Math.Round(height * scale));
        return (w, h);
    }

    // Resize (nếu cần) bằng bộ lọc cubic Mitchell — cùng thuật toán resize chất
    // lượng cao đã dùng cho thumbnail/preview ở BackgroundRemoverView, tương
    // đương high-quality bicubic (BUỘC #23). JPEG luôn flatten alpha lên nền
    // trắng (#11); WebP giữ nguyên alpha nếu có.
    private static byte[] EncodeImage(SKBitmap upright, int targetW, int targetH, OutputImageFormat format, int quality)
    {
        bool needsResize = targetW != upright.Width || targetH != upright.Height;

        SKBitmap? resized = null;
        try
        {
            if (needsResize)
            {
                SKImageInfo info = new(targetW, targetH, upright.ColorType, upright.AlphaType);
                resized = upright.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
                    ?? throw new InvalidOperationException("Resize ảnh thất bại.");
            }

            SKBitmap source = resized ?? upright;

            if (format == OutputImageFormat.Jpeg)
            {
                using SKBitmap flattened = new(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
                using (SKCanvas canvas = new(flattened))
                {
                    canvas.Clear(SKColors.White);
                    canvas.DrawBitmap(source, 0, 0);
                }

                using SKImage image = SKImage.FromBitmap(flattened);
                using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                return data.ToArray();
            }
            else
            {
                using SKImage image = SKImage.FromBitmap(source);
                using SKData data = image.Encode(SKEncodedImageFormat.Webp, quality);
                return data.ToArray();
            }
        }
        finally
        {
            resized?.Dispose();
        }
    }

    private static OutputImageFormat? DetectFormat(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => OutputImageFormat.Jpeg,
        ".webp" => OutputImageFormat.WebP,
        _ => null
    };

    // Sinh tên file KHÔNG đè lên nhau (#28) và không đụng file gốc (#29):
    // IMG001.jpg -> IMG001_compressed.jpg -> IMG001_compressed_2.jpg -> ...
    // format = null nghĩa là giữ nguyên gốc (trường hợp Original kept).
    private string ReserveOutputPath(string inputPath, string outputFolder, OutputImageFormat? format)
    {
        string baseName = Path.GetFileNameWithoutExtension(inputPath);
        string ext = format switch
        {
            OutputImageFormat.Jpeg => ".jpg",
            OutputImageFormat.WebP => ".webp",
            _ => Path.GetExtension(inputPath)
        };

        lock (_pathLock)
        {
            string candidate = Path.Combine(outputFolder, $"{baseName}_compressed{ext}");
            int n = 2;
            while (File.Exists(candidate) || _reservedPaths.Contains(candidate))
            {
                candidate = Path.Combine(outputFolder, $"{baseName}_compressed_{n}{ext}");
                n++;
            }

            _reservedPaths.Add(candidate);
            return candidate;
        }
    }
}
