namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

public sealed class MaskRefineOptions
{
    // Bỏ đốm foreground cô lập nhỏ hơn tỉ lệ này so với diện tích ảnh.
    public float SmallForegroundBlobRatio { get; init; } = 0.0003f;

    // Lấp lỗ (vùng nền kín nằm lọt trong foreground) nhỏ hơn tỉ lệ này.
    public float SmallHoleRatio { get; init; } = 0.004f;

    // Bán kính cross-bilateral cho làm mượt biên (rất nhẹ).
    public int EdgeSmoothRadius { get; init; } = 2;
    public float EdgeSmoothSpatialSigma { get; init; } = 1.5f;
    public float EdgeSmoothRangeSigma { get; init; } = 0.10f;

    // Nới rộng dải biên (số pixel) trước khi làm mượt.
    public int EdgeBandDilate { get; init; } = 3;

    public bool RemoveSmallBlobs { get; init; } = true;
    public bool FillSmallHoles { get; init; } = true;
    public bool EdgeAwareSmoothing { get; init; } = true;
}

// Pipeline làm sạch mask — chạy ở working-resolution, thứ tự đúng theo yêu cầu:
//   clamp → khử đốm cô lập → lấp lỗ nhỏ → làm mượt biên edge-aware rất nhẹ.
// KHÔNG Gaussian blur mạnh toàn mask ở bất kỳ bước nào. Feather cuối làm ở
// full-resolution trong BgRemovalPipeline.
public sealed class MaskRefiner
{
    private readonly MaskRefineOptions _opt;

    public MaskRefiner(MaskRefineOptions? options = null)
        => _opt = options ?? new MaskRefineOptions();

    public AlphaMap Refine(AlphaMap alpha, float[] guideGray)
    {
        int w = alpha.Width;
        int h = alpha.Height;
        float[] a = alpha.Data;
        long totalPx = (long)w * h;

        // 1. clamp [0..1]
        for (int i = 0; i < a.Length; i++)
            a[i] = a[i] < 0f ? 0f : a[i] > 1f ? 1f : a[i];

        // 2. khử đốm foreground cô lập nhỏ
        if (_opt.RemoveSmallBlobs)
            RemoveSmallForegroundBlobs(a, w, h, totalPx);

        // 3. lấp lỗ nhỏ (vùng nền kín lọt trong foreground)
        if (_opt.FillSmallHoles)
            FillSmallHoles(a, w, h, totalPx);

        // 4. làm mượt biên edge-aware rất nhẹ
        if (_opt.EdgeAwareSmoothing && guideGray.Length == a.Length)
            EdgeAwareSmooth(a, guideGray, w, h);

        return alpha;
    }

    private void RemoveSmallForegroundBlobs(float[] a, int w, int h, long totalPx)
    {
        bool[] fg = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            fg[i] = a[i] > 0.5f;

        (int[] labels, List<MaskMath.Component> comps) = MaskMath.ConnectedComponents(fg, w, h);
        if (comps.Count <= 1)
            return;

        MaskMath.Component main = comps.MaxBy(c => c.Area)!;
        int threshold = (int)(totalPx * _opt.SmallForegroundBlobRatio);

        // Chủ thể quá nhỏ (ảnh macro / vật thể bé) — không mạo hiểm xoá gì.
        if (main.Area < threshold * 4)
            return;

        // Chỉ xoá blob NHỎ và NẰM TÁCH XA chủ thể chính — giữ lại các phần đính
        // kèm (dây treo, khoá móc...) dù bị ngưỡng 0.5 cắt rời.
        int gap = (int)(Math.Max(w, h) * 0.02);
        HashSet<int> drop = new();
        foreach (MaskMath.Component c in comps)
        {
            if (c.Label == main.Label || c.Area >= threshold)
                continue;
            bool nearMain =
                c.MaxX >= main.MinX - gap && c.MinX <= main.MaxX + gap &&
                c.MaxY >= main.MinY - gap && c.MinY <= main.MaxY + gap;
            if (!nearMain)
                drop.Add(c.Label);
        }
        if (drop.Count == 0)
            return;

        for (int i = 0; i < a.Length; i++)
            if (drop.Contains(labels[i]))
                a[i] = 0f;
    }

    private void FillSmallHoles(float[] a, int w, int h, long totalPx)
    {
        bool[] bg = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            bg[i] = a[i] < 0.5f;

        (int[] labels, List<MaskMath.Component> comps) = MaskMath.ConnectedComponents(bg, w, h);
        int threshold = (int)(totalPx * _opt.SmallHoleRatio);

        HashSet<int> fill = new(
            comps.Where(c => !c.TouchesBorder && c.Area < threshold).Select(c => c.Label));
        if (fill.Count == 0)
            return;

        for (int i = 0; i < a.Length; i++)
            if (fill.Contains(labels[i]))
                a[i] = MathF.Max(a[i], 0.98f);
    }

    private void EdgeAwareSmooth(float[] a, float[] guide, int w, int h)
    {
        // Dải biên = pixel bán trong suốt, nới rộng vài pixel.
        bool[] band = new bool[a.Length];
        for (int i = 0; i < a.Length; i++)
            band[i] = a[i] > 0.02f && a[i] < 0.98f;

        if (_opt.EdgeBandDilate > 0)
            band = MaskMath.Dilate(band, w, h, _opt.EdgeBandDilate);

        int r = _opt.EdgeSmoothRadius;
        float spatialDen = 2f * _opt.EdgeSmoothSpatialSigma * _opt.EdgeSmoothSpatialSigma;
        float rangeDen = 2f * _opt.EdgeSmoothRangeSigma * _opt.EdgeSmoothRangeSigma;

        float[] src = (float[])a.Clone();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int c = (y * w) + x;
                if (!band[c])
                    continue;

                float gc = guide[c];
                float accW = 0f, accV = 0f;

                for (int dy = -r; dy <= r; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;

                        int n = (ny * w) + nx;
                        float spatial = MathF.Exp(-((dx * dx) + (dy * dy)) / spatialDen);
                        float dg = guide[n] - gc;
                        float range = MathF.Exp(-(dg * dg) / rangeDen);
                        float wgt = spatial * range;

                        accW += wgt;
                        accV += wgt * src[n];
                    }
                }

                if (accW > 1e-6f)
                    a[c] = accV / accW;
            }
        }
    }
}
