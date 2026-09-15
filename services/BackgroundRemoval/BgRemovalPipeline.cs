using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

public sealed class BgRemovalRequest
{
    public required string SourcePath { get; init; }
    public required string OutputFolder { get; init; }
    public required QualityMode Mode { get; init; }
    public bool Debug { get; init; }

    // Sinh ảnh kiểm tra chất lượng mask (mask cuối + cutout trên nền caro) cho
    // Preview. Bật từ UI, tắt ở chế độ CLI batch.
    public bool Inspect { get; init; }
}

public sealed class BgRemovalOutcome
{
    public required string OutputPath { get; init; }
    public required BgRemovalTimings Timings { get; init; }

    // Chỉ khác null khi req.Inspect = true. Người gọi chịu trách nhiệm Dispose.
    public BgRemovalInspection? Inspection { get; init; }
}

// Điều phối toàn bộ pipeline xóa nền cho một ảnh, theo QualityMode.
// Không sở hữu InferenceSession — nhận sẵn từ BackgroundRemovalService (đảm bảo
// model chỉ nạp một lần, tái dùng cho mọi ảnh).
internal sealed class BgRemovalPipeline
{
    private readonly OnnxModelSession _segmenter;
    private readonly OnnxModelSession? _matting;
    private readonly string _providerLabel;

    public BgRemovalPipeline(OnnxModelSession segmenter, OnnxModelSession? matting)
    {
        _segmenter = segmenter;
        _matting = matting;
        _providerLabel = matting != null
            ? $"{segmenter.ProviderDescription}+{matting.ProviderDescription}"
            : segmenter.ProviderDescription;
    }

    public BgRemovalOutcome Run(BgRemovalRequest req, CancellationToken token)
    {
        BgRemovalTimings t = new();
        Stopwatch total = Stopwatch.StartNew();
        Stopwatch stage = new();

        // ---- Decode ----
        stage.Restart();
        using SKBitmap original = DecodeRgba(req.SourcePath);
        int w = original.Width;
        int h = original.Height;
        RgbImage srcRgb = RgbImage.FromBitmap(original);
        t.DecodeMs = stage.ElapsedMilliseconds;
        token.ThrowIfCancellationRequested();

        // ---- Segmentation (preprocess + inference) ----
        InferResult seg = _segmenter.Infer(original, token);
        t.PreprocessMs = seg.PreprocessMs;
        t.InferenceMs = seg.InferenceMs;
        AlphaMap segAlpha = seg.Alpha;
        segAlpha.Clamp01();
        token.ThrowIfCancellationRequested();

        int wr = segAlpha.Width;
        int hr = segAlpha.Height;

        AlphaMap rawForDebug = req.Debug ? segAlpha.Clone() : segAlpha;

        // Guide xám ở working-resolution (dùng cho edge-aware smoothing).
        float[] workGuide = Array.Empty<float>();
        if (req.Mode != QualityMode.Fast)
        {
            using SKBitmap workBmp = ResizeRgba(original, wr, hr);
            workGuide = RgbImage.FromBitmap(workBmp).ToGrayNormalized();
        }

        AlphaMap workAlpha;
        TrimapLabel[]? trimap = null;
        AlphaMap? matteForDebug = null;

        switch (req.Mode)
        {
            case QualityMode.Fast:
                workAlpha = segAlpha;
                break;

            case QualityMode.HighQuality:
            {
                stage.Restart();
                workAlpha = new MaskRefiner().Refine(segAlpha, workGuide);
                t.RefineMs = stage.ElapsedMilliseconds;
                break;
            }

            case QualityMode.Ultra:
            {
                stage.Restart();
                // Chỉ khử đốm trước khi dựng trimap — chưa làm mượt biên.
                MaskRefiner blobOnly = new(new MaskRefineOptions
                {
                    RemoveSmallBlobs = true,
                    FillSmallHoles = false,
                    EdgeAwareSmoothing = false
                });
                blobOnly.Refine(segAlpha, Array.Empty<float>());

                trimap = new TrimapBuilder().Build(segAlpha);
                t.RefineMs += stage.ElapsedMilliseconds;

                token.ThrowIfCancellationRequested();

                // Pass matting thứ hai (toàn ảnh, resolution model) — alpha liên tục.
                if (_matting == null)
                    throw new InvalidOperationException("Ultra cần model matting nhưng chưa nạp.");

                InferResult matteRes = _matting.Infer(original, token);
                t.PreprocessMs += matteRes.PreprocessMs;
                t.MattingMs = matteRes.InferenceMs;

                AlphaMap matte = matteRes.Alpha.Width == wr && matteRes.Alpha.Height == hr
                    ? matteRes.Alpha
                    : matteRes.Alpha.Resized(wr, hr);
                matte.Clamp01();
                if (req.Debug)
                    matteForDebug = matte.Clone();

                token.ThrowIfCancellationRequested();

                // Hợp nhất: FG→1, BG→0, unknown→matte (chỉ refine vùng biên).
                stage.Restart();
                AlphaMap merged = new(wr, hr);
                for (int i = 0; i < merged.Data.Length; i++)
                {
                    merged.Data[i] = trimap[i] switch
                    {
                        TrimapLabel.Foreground => 1f,
                        TrimapLabel.Background => 0f,
                        _ => matte.Data[i]
                    };
                }

                // Làm mượt biên + lấp lỗ nhỏ trên kết quả đã hợp nhất.
                workAlpha = new MaskRefiner(new MaskRefineOptions
                {
                    RemoveSmallBlobs = false,
                    FillSmallHoles = true,
                    EdgeAwareSmoothing = true
                }).Refine(merged, workGuide);
                t.RefineMs += stage.ElapsedMilliseconds;
                break;
            }

            default:
                workAlpha = segAlpha;
                break;
        }

        token.ThrowIfCancellationRequested();

        // ---- Resize alpha về full-res + feather ----
        stage.Restart();
        AlphaMap fullAlpha = workAlpha.Resized(w, h);
        fullAlpha.Clamp01();

        // Mask sau refinement nhưng TRƯỚC feather cuối — để debug tách bạch tác
        // động của riêng bước feather.
        AlphaMap? refinedBeforeFeather = req.Debug ? fullAlpha.Clone() : null;

        if (req.Mode != QualityMode.Fast)
            Feather(fullAlpha, req.Mode == QualityMode.Ultra ? 1 : FeatherRadius(w, h));
        t.RefineMs += stage.ElapsedMilliseconds;

        // Từ đây fullAlpha là alpha CUỐI CÙNG dùng để composite — bước decontam
        // bên dưới chỉ đổi màu RGB foreground, không đụng tới alpha.

        // ---- Edge color decontamination ----
        RgbImage foreground = srcRgb;
        if (req.Mode != QualityMode.Fast)
        {
            stage.Restart();
            foreground = new EdgeDecontaminator().Decontaminate(srcRgb, fullAlpha);
            t.DecontamMs = stage.ElapsedMilliseconds;
            token.ThrowIfCancellationRequested();
        }

        // ---- Composite trên nền trắng RGB(255,255,255) ----
        stage.Restart();
        using SKBitmap composited = CompositeOnWhite(foreground, fullAlpha);
        t.CompositeMs = stage.ElapsedMilliseconds;

        // ---- Save ----
        stage.Restart();
        string outputPath = BuildOutputPath(req.OutputFolder, req.SourcePath);
        SavePng(composited, outputPath);

        if (req.Debug)
            SaveDebug(req, rawForDebug.Resized(w, h), refinedBeforeFeather!, fullAlpha, composited,
                foreground, trimap, matteForDebug, wr, hr, w, h);
        t.SaveMs = stage.ElapsedMilliseconds;

        t.TotalMs = total.ElapsedMilliseconds;
        BackgroundRemovalDiagnosticsLog.Write(
            t.ToLogLine(Path.GetFileName(req.SourcePath), req.Mode, _providerLabel));

        // Ảnh kiểm tra mask dựng cuối cùng (sau khi mọi thứ đã lưu an toàn) từ
        // đúng foreground + alpha đã composite.
        return new BgRemovalOutcome
        {
            OutputPath = outputPath,
            Timings = t,
            Inspection = req.Inspect ? BgRemovalInspection.Build(foreground, fullAlpha) : null
        };
    }

    private static int FeatherRadius(int w, int h)
        => Math.Clamp((int)Math.Round(Math.Max(w, h) / 1400.0), 1, 2);

    // Feather 1–2 px CHỈ ở dải biên — không blur toàn mask. Trọng số theo mức
    // "bán trong suốt" (0 ở a=0/1, cực đại quanh a=0.5).
    private static void Feather(AlphaMap alpha, int radius)
    {
        if (radius < 1)
            return;

        float[] a = alpha.Data;
        float[] blurred = MaskMath.BoxBlur(a, alpha.Width, alpha.Height, radius, 2);

        for (int i = 0; i < a.Length; i++)
        {
            float edge = Math.Min(a[i], 1f - a[i]);
            float wgt = MaskMath.SmoothStep(0f, 0.15f, edge);
            if (wgt <= 0f)
                continue;
            a[i] += (blurred[i] - a[i]) * wgt;
        }
    }

    // out = F·a + 255·(1−a). F đã khử nhiễm; nền chính xác RGB(255,255,255).
    private static SKBitmap CompositeOnWhite(RgbImage foreground, AlphaMap alpha)
    {
        int w = foreground.Width;
        int h = foreground.Height;

        SKImageInfo info = new(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        SKBitmap output = new(info);
        byte[] outPx = new byte[h * info.RowBytes];
        int rowBytes = info.RowBytes;

        byte[] fg = foreground.Rgb;
        float[] a = alpha.Data;

        for (int y = 0; y < h; y++)
        {
            int outRow = y * rowBytes;
            int aRow = y * w;
            int fgRow = y * w * 3;
            for (int x = 0; x < w; x++)
            {
                float av = a[aRow + x];
                av = av < 0f ? 0f : av > 1f ? 1f : av;
                float inv = 1f - av;
                int oi = outRow + (x * 4);
                int fi = fgRow + (x * 3);

                outPx[oi] = ClampByte((fg[fi] * av) + (255f * inv));
                outPx[oi + 1] = ClampByte((fg[fi + 1] * av) + (255f * inv));
                outPx[oi + 2] = ClampByte((fg[fi + 2] * av) + (255f * inv));
                outPx[oi + 3] = 255;
            }
        }

        Marshal.Copy(outPx, 0, output.GetPixels(), outPx.Length);
        return output;
    }

    private static byte ClampByte(float v) => (byte)(v < 0f ? 0f : v > 255f ? 255f : v + 0.5f);

    private static SKBitmap DecodeRgba(string path)
    {
        using FileStream fs = File.OpenRead(path);
        using SKBitmap decoded = SKBitmap.Decode(fs)
            ?? throw new InvalidDataException("Không đọc được ảnh (định dạng không hỗ trợ hoặc file lỗi).");

        return decoded.ColorType == SKColorType.Rgba8888
            ? decoded.Copy()
            : decoded.Copy(SKColorType.Rgba8888)
              ?? throw new InvalidOperationException("Chuyển ảnh sang RGBA thất bại.");
    }

    private static SKBitmap ResizeRgba(SKBitmap source, int width, int height)
    {
        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        return source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("Resize ảnh thất bại.");
    }

    private static void SavePng(SKBitmap bitmap, string outputPath)
    {
        string? folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream fs = File.Create(outputPath);
        data.SaveTo(fs);
    }

    private static void SaveDebug(
        BgRemovalRequest req,
        AlphaMap rawMaskFull,
        AlphaMap refinedMaskFull,
        AlphaMap finalMaskFull,
        SKBitmap final,
        RgbImage foreground,
        TrimapLabel[]? trimap,
        AlphaMap? matte,
        int wr, int hr, int w, int h)
    {
        string debugDir = Path.Combine(req.OutputFolder, "debug");
        Directory.CreateDirectory(debugDir);
        // Kèm mode vào tên để chạy cả 3 chế độ trên cùng một ảnh không đè lên nhau.
        string baseName = $"{Path.GetFileNameWithoutExtension(req.SourcePath)}_{req.Mode}";

        // 3 mask theo đúng thứ tự pipeline:
        //   _raw_mask     : alpha thô từ model (chưa refine).
        //   _refined_mask : sau refinement (khử đốm / lấp lỗ / mượt biên), TRƯỚC feather.
        //   _final_mask   : alpha CUỐI CÙNG truyền cho bước composite (sau feather).
        // Ở mode Fast không có refine/feather nên cả ba giống nhau.
        SaveGray(rawMaskFull, Path.Combine(debugDir, $"{baseName}_raw_mask.png"));
        SaveGray(refinedMaskFull, Path.Combine(debugDir, $"{baseName}_refined_mask.png"));
        SaveGray(finalMaskFull, Path.Combine(debugDir, $"{baseName}_final_mask.png"));

        using (SKImage img = SKImage.FromBitmap(final))
        using (SKData d = img.Encode(SKEncodedImageFormat.Png, 100))
        using (FileStream fs = File.Create(Path.Combine(debugDir, $"{baseName}_final.png")))
            d.SaveTo(fs);

        // Cùng nội dung với chế độ "Nền caro" trong Preview, nhưng ghi ra file để
        // soi biên bằng công cụ ngoài (hoặc gửi kèm khi báo lỗi chất lượng).
        SaveChecker(foreground, finalMaskFull, Path.Combine(debugDir, $"{baseName}_checker.png"));

        if (trimap != null)
            SaveGray(TrimapBuilder.ToDebugMap(trimap, wr, hr).Resized(w, h),
                Path.Combine(debugDir, $"{baseName}_trimap.png"));

        if (matte != null)
            SaveGray(matte.Resized(w, h), Path.Combine(debugDir, $"{baseName}_matte.png"));
    }

    // Foreground (đã khử nhiễm màu biên) composite trên caro xám/trắng bằng ĐÚNG
    // alpha cuối — để lộ halo, vùng nền còn sót và mép bị cắt mà nền trắng giấu đi.
    // Ô caro ở đây theo pixel ảnh (file tĩnh 1:1); trong Preview ô vẽ theo tọa độ
    // màn hình nên không đổi kích thước khi zoom.
    private static void SaveChecker(RgbImage foreground, AlphaMap alpha, string path)
    {
        const int tile = 9;
        int w = foreground.Width;
        int h = foreground.Height;

        SKImageInfo info = new(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using SKBitmap bmp = new(info);
        byte[] px = new byte[h * info.RowBytes];
        byte[] fg = foreground.Rgb;
        float[] a = alpha.Data;

        for (int y = 0; y < h; y++)
        {
            int outRow = y * info.RowBytes;
            int aRow = y * w;
            int fgRow = y * w * 3;
            int cy = y / tile;

            for (int x = 0; x < w; x++)
            {
                float bg = (((x / tile) + cy) & 1) == 0 ? 255f : 198f;
                float av = a[aRow + x];
                av = av < 0f ? 0f : av > 1f ? 1f : av;
                float inv = 1f - av;

                int oi = outRow + (x * 4);
                int fi = fgRow + (x * 3);
                px[oi] = ClampByte((fg[fi] * av) + (bg * inv));
                px[oi + 1] = ClampByte((fg[fi + 1] * av) + (bg * inv));
                px[oi + 2] = ClampByte((fg[fi + 2] * av) + (bg * inv));
                px[oi + 3] = 255;
            }
        }

        Marshal.Copy(px, 0, bmp.GetPixels(), px.Length);

        using SKImage img = SKImage.FromBitmap(bmp);
        using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream fs = File.Create(path);
        data.SaveTo(fs);
    }

    private static void SaveGray(AlphaMap map, string path)
    {
        using SKBitmap bmp = map.ToGray8Bitmap();
        using SKImage img = SKImage.FromBitmap(bmp);
        using SKData data = img.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream fs = File.Create(path);
        data.SaveTo(fs);
    }

    // Không overwrite: ABC123_white-background.png, rồi _2, _3...
    private static string BuildOutputPath(string outputFolder, string sourcePath)
    {
        Directory.CreateDirectory(outputFolder);
        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string candidate = Path.Combine(outputFolder, $"{baseName}_white-background.png");

        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(outputFolder, $"{baseName}_white-background_{suffix}.png");
            suffix++;
        }

        return candidate;
    }
}
