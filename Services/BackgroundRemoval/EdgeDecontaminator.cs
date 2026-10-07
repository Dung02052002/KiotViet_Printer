namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

public sealed class EdgeDecontaminateOptions
{
    public float ConfidentForegroundAlpha { get; init; } = 0.95f;
    public float ConfidentBackgroundAlpha { get; init; } = 0.05f;

    // Chỉ xử lý pixel trong dải chuyển tiếp này.
    public float BandLow { get; init; } = 0.05f;
    public float BandHigh { get; init; } = 0.95f;

    // Trộn kết quả unmix về màu foreground lân cận cho ổn định (0 = unmix thuần).
    public float StabilityBlend { get; init; } = 0.25f;

    // Chặn lệch mỗi kênh so với màu foreground lân cận → chống dark/white halo.
    public int MaxChannelDelta { get; init; } = 60;

    // Bán kính tối đa (pixel) lan màu foreground/background vào dải biên.
    public int MaxPropagationRadius { get; init; } = 16;
}

// Foreground Edge Color Decontamination.
//
// Pixel biên quan sát được:  C = a·F + (1−a)·B   (B = màu nền cũ).
// Ước lượng B_local, F_local bằng lan truyền màu từ vùng chắc chắn, rồi:
//   F_unmix = (C − (1−a)·B_local) / a
//   F_est   = lerp(F_unmix, F_local, StabilityBlend), chặn quanh F_local.
// Không bao giờ bơm trắng/xám → không tạo white/gray/dark halo. Chỉ đụng dải
// 0.05 < a < 0.95; foreground chắc chắn giữ nguyên màu.
public sealed class EdgeDecontaminator
{
    private readonly EdgeDecontaminateOptions _opt;

    public EdgeDecontaminator(EdgeDecontaminateOptions? options = null)
        => _opt = options ?? new EdgeDecontaminateOptions();

    // Trả về ảnh RGB "màu foreground đã khử nhiễm" — bằng src ở ngoài dải biên,
    // đã chỉnh trong dải biên. Người gọi tự composite trên nền trắng.
    public RgbImage Decontaminate(RgbImage src, AlphaMap alphaFullRes)
    {
        int w = src.Width;
        int h = src.Height;
        if (alphaFullRes.Width != w || alphaFullRes.Height != h)
            throw new ArgumentException("Alpha map phải cùng kích thước ảnh gốc.");

        float[] a = alphaFullRes.Data;
        RgbImage outImg = new(w, h);
        Array.Copy(src.Rgb, outImg.Rgb, src.Rgb.Length);

        bool[] band = new bool[a.Length];
        bool[] inTransition = new bool[a.Length];
        bool[] fgSeed = new bool[a.Length];
        bool[] bgSeed = new bool[a.Length];

        // Cờ per-pixel thuần, không phụ thuộc lẫn nhau — song song hoá an toàn.
        Parallel.For(0, a.Length, i =>
        {
            inTransition[i] = a[i] > _opt.BandLow && a[i] < _opt.BandHigh;
            band[i] = a[i] > 0.02f && a[i] < 0.98f;
            fgSeed[i] = a[i] >= _opt.ConfidentForegroundAlpha;
            bgSeed[i] = a[i] <= _opt.ConfidentBackgroundAlpha;
        });

        // Vùng cho phép lan màu = dải biên nới rộng MaxPropagationRadius.
        bool[] allow = _opt.MaxPropagationRadius > 0
            ? MaskMath.Dilate(band, w, h, _opt.MaxPropagationRadius)
            : band;

        (byte[] fLocal, bool[] fHas) = PropagateColor(src, fgSeed, allow, w, h);
        (byte[] bLocal, bool[] bHas) = PropagateColor(src, bgSeed, allow, w, h);

        float k = _opt.StabilityBlend;
        int maxDelta = _opt.MaxChannelDelta;

        // Mỗi pixel i chỉ đọc mảng bất biến trong vòng lặp (fLocal/bLocal/...) và
        // chỉ ghi vào outImg.Rgb tại đúng offset của nó (p3..p3+2) — độc lập giữa
        // các pixel, song song hoá an toàn theo pixel.
        Parallel.For(0, a.Length, i =>
        {
            if (!inTransition[i] || !fHas[i])
                return;

            float alpha = a[i];
            // Fade → 0 ngay sát foreground chắc chắn (không tạo viền do chính bước
            // khử); giữ full lực gần hết dải để dứt điểm màu nền còn sót.
            float fade = MaskMath.SmoothStep(0.985f, 0.88f, alpha)
                       * MaskMath.SmoothStep(_opt.BandLow - 0.03f, _opt.BandLow + 0.03f, alpha);
            if (fade <= 0f)
                return;

            int p3 = i * 3;
            float denom = MathF.Max(alpha, 0.15f);
            bool hasB = bHas[i];

            for (int c = 0; c < 3; c++)
            {
                float observed = src.Rgb[p3 + c];
                float fl = fLocal[p3 + c];
                float bl = hasB ? bLocal[p3 + c] : observed;

                float unmix = (observed - ((1f - alpha) * bl)) / denom;
                float est = unmix + ((fl - unmix) * k);

                // Chặn quanh màu foreground thật lân cận → không dark/white halo.
                float lo = fl - maxDelta;
                float hi = fl + maxDelta;
                if (est < lo) est = lo;
                if (est > hi) est = hi;
                if (est < 0f) est = 0f;
                if (est > 255f) est = 255f;

                float blended = observed + ((est - observed) * fade);
                outImg.Rgb[p3 + c] = (byte)(blended < 0f ? 0f : blended > 255f ? 255f : blended + 0.5f);
            }
        });

        return outImg;
    }

    // BFS đa nguồn từ toàn bộ seed vào vùng allow: mỗi pixel nhận trung bình màu
    // các lân cận đã biết (seed dùng màu gốc, pixel đã tô dùng màu lan truyền) —
    // xấp xỉ "màu seed gần nhất theo khoảng cách" nhưng mượt ở góc.
    private static (byte[] rgb, bool[] has) PropagateColor(
        RgbImage src, bool[] seed, bool[] allow, int w, int h)
    {
        int n = seed.Length;
        byte[] rgb = new byte[src.Rgb.Length];
        bool[] has = new bool[n];

        int frontierBound = 0;
        for (int i = 0; i < n; i++)
            if (seed[i] || allow[i]) frontierBound++;

        int[] queue = new int[Math.Max(1, frontierBound)];
        int qHead = 0, qTail = 0;

        for (int i = 0; i < n; i++)
            if (seed[i])
                queue[qTail++] = i;

        while (qHead < qTail)
        {
            int cur = queue[qHead++];
            int cx = cur % w;
            int cy = cur / w;

            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = cy + dy;
                if (ny < 0 || ny >= h) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx;
                    if (nx < 0 || nx >= w) continue;

                    int ni = (ny * w) + nx;
                    if (seed[ni] || has[ni] || !allow[ni])
                        continue;

                    int sum0 = 0, sum1 = 0, sum2 = 0, cnt = 0;
                    for (int ey = -1; ey <= 1; ey++)
                    {
                        int my = ny + ey;
                        if (my < 0 || my >= h) continue;
                        for (int ex = -1; ex <= 1; ex++)
                        {
                            if (ex == 0 && ey == 0) continue;
                            int mx = nx + ex;
                            if (mx < 0 || mx >= w) continue;
                            int mi = (my * w) + mx;
                            int m3 = mi * 3;
                            if (seed[mi])
                            {
                                sum0 += src.Rgb[m3]; sum1 += src.Rgb[m3 + 1]; sum2 += src.Rgb[m3 + 2];
                                cnt++;
                            }
                            else if (has[mi])
                            {
                                sum0 += rgb[m3]; sum1 += rgb[m3 + 1]; sum2 += rgb[m3 + 2];
                                cnt++;
                            }
                        }
                    }

                    if (cnt == 0)
                        continue;

                    int ni3 = ni * 3;
                    rgb[ni3] = (byte)(sum0 / cnt);
                    rgb[ni3 + 1] = (byte)(sum1 / cnt);
                    rgb[ni3 + 2] = (byte)(sum2 / cnt);
                    has[ni] = true;
                    if (qTail < queue.Length)
                        queue[qTail++] = ni;
                }
            }
        }

        return (rgb, has);
    }
}
