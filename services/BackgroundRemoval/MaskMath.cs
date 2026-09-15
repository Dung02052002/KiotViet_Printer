namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Các thuật toán mask thuần mảng — không phụ thuộc thư viện ngoài.
internal static class MaskMath
{
    // Box blur phân tách (ngang + dọc) dùng prefix-sum → O(n) không phụ thuộc radius.
    // radius nhỏ (1-3) cho feather; hai lần lặp xấp xỉ Gaussian.
    public static float[] BoxBlur(float[] src, int w, int h, int radius, int passes = 1)
    {
        if (radius < 1)
            return (float[])src.Clone();

        float[] cur = (float[])src.Clone();
        float[] tmp = new float[cur.Length];

        for (int p = 0; p < passes; p++)
        {
            BlurHorizontal(cur, tmp, w, h, radius);
            BlurVertical(tmp, cur, w, h, radius);
        }

        return cur;
    }

    private static void BlurHorizontal(float[] src, float[] dst, int w, int h, int radius)
    {
        float norm = 1f / ((2 * radius) + 1);

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            float acc = src[row] * (radius + 1);
            for (int x = 1; x <= radius; x++)
                acc += src[row + Math.Min(x, w - 1)];

            for (int x = 0; x < w; x++)
            {
                dst[row + x] = acc * norm;
                float add = src[row + Math.Min(x + radius + 1, w - 1)];
                float sub = src[row + Math.Max(x - radius, 0)];
                acc += add - sub;
            }
        }
    }

    private static void BlurVertical(float[] src, float[] dst, int w, int h, int radius)
    {
        float norm = 1f / ((2 * radius) + 1);

        for (int x = 0; x < w; x++)
        {
            float acc = src[x] * (radius + 1);
            for (int y = 1; y <= radius; y++)
                acc += src[(Math.Min(y, h - 1) * w) + x];

            for (int y = 0; y < h; y++)
            {
                dst[(y * w) + x] = acc * norm;
                float add = src[(Math.Min(y + radius + 1, h - 1) * w) + x];
                float sub = src[(Math.Max(y - radius, 0) * w) + x];
                acc += add - sub;
            }
        }
    }

    public sealed class Component
    {
        public int Label;
        public int Area;
        public int MinX = int.MaxValue;
        public int MinY = int.MaxValue;
        public int MaxX = int.MinValue;
        public int MaxY = int.MinValue;
        public bool TouchesBorder;
    }

    // Gán nhãn thành phần liên thông 8-hướng cho mask nhị phân (predicate: value==true).
    // labels[i] = 0 nghĩa là không thuộc mask; >0 là id thành phần.
    public static (int[] labels, List<Component> components) ConnectedComponents(
        bool[] mask, int w, int h)
    {
        int[] labels = new int[mask.Length];
        List<Component> comps = new();
        int[] stack = new int[mask.Length];

        int next = 0;
        for (int start = 0; start < mask.Length; start++)
        {
            if (!mask[start] || labels[start] != 0)
                continue;

            next++;
            Component comp = new() { Label = next };
            comps.Add(comp);

            int sp = 0;
            stack[sp++] = start;
            labels[start] = next;

            while (sp > 0)
            {
                int idx = stack[--sp];
                int x = idx % w;
                int y = idx / w;

                comp.Area++;
                if (x < comp.MinX) comp.MinX = x;
                if (y < comp.MinY) comp.MinY = y;
                if (x > comp.MaxX) comp.MaxX = x;
                if (y > comp.MaxY) comp.MaxY = y;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                    comp.TouchesBorder = true;

                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= h) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx;
                        if (nx < 0 || nx >= w) continue;

                        int nIdx = (ny * w) + nx;
                        if (mask[nIdx] && labels[nIdx] == 0)
                        {
                            labels[nIdx] = next;
                            stack[sp++] = nIdx;
                        }
                    }
                }
            }
        }

        return (labels, comps);
    }

    // Giãn nở nhị phân bán kính `radius` (khoảng cách Chebyshev — tương đương lặp
    // giãn nở 8-hướng `radius` lần) bằng hai lượt 1D → O(n), không phụ thuộc radius².
    public static bool[] Dilate(bool[] mask, int w, int h, int radius)
    {
        if (radius <= 0)
            return (bool[])mask.Clone();

        bool[] horiz = new bool[mask.Length];
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            DilateLine(mask, horiz, row, 1, w, radius);
        }

        bool[] outMask = new bool[mask.Length];
        for (int x = 0; x < w; x++)
            DilateLine(horiz, outMask, x, w, h, radius);

        return outMask;
    }

    // Giãn nở 1D dọc theo một hàng/cột: dst[k] = true nếu có src trong [k-r, k+r].
    // Dùng khoảng cách tới phần tử true gần nhất từ hai phía (hai lượt quét).
    private static void DilateLine(bool[] src, bool[] dst, int start, int stride, int count, int radius)
    {
        int dist = radius + 1;
        for (int k = 0; k < count; k++)
        {
            int idx = start + (k * stride);
            dist = src[idx] ? 0 : dist + 1;
            if (dist <= radius)
                dst[idx] = true;
        }

        dist = radius + 1;
        for (int k = count - 1; k >= 0; k--)
        {
            int idx = start + (k * stride);
            dist = src[idx] ? 0 : dist + 1;
            if (dist <= radius)
                dst[idx] = true;
        }
    }

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        if (edge0 == edge1)
            return x < edge0 ? 0f : 1f;
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - (2f * t));
    }
}
