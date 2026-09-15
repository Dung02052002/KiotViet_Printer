namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

public enum TrimapLabel : byte
{
    Background = 0,
    Unknown = 1,
    Foreground = 2
}

// Sinh trimap từ soft alpha của model segmentation:
//   alpha >= ForegroundThreshold  → definite foreground
//   alpha <= BackgroundThreshold  → definite background
//   còn lại                       → unknown (được nới rộng để pass matting có ngữ cảnh)
public sealed class TrimapBuilder
{
    public float ForegroundThreshold { get; init; } = 0.92f;
    public float BackgroundThreshold { get; init; } = 0.08f;
    public int UnknownDilate { get; init; } = 8;

    public TrimapLabel[] Build(AlphaMap alpha)
    {
        int w = alpha.Width;
        int h = alpha.Height;
        float[] a = alpha.Data;

        TrimapLabel[] tri = new TrimapLabel[a.Length];
        bool[] unknown = new bool[a.Length];

        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] >= ForegroundThreshold)
                tri[i] = TrimapLabel.Foreground;
            else if (a[i] <= BackgroundThreshold)
                tri[i] = TrimapLabel.Background;
            else
            {
                tri[i] = TrimapLabel.Unknown;
                unknown[i] = true;
            }
        }

        if (UnknownDilate > 0)
        {
            bool[] widened = MaskMath.Dilate(unknown, w, h, UnknownDilate);
            for (int i = 0; i < tri.Length; i++)
                if (widened[i] && tri[i] != TrimapLabel.Unknown)
                    tri[i] = TrimapLabel.Unknown;
        }

        return tri;
    }

    public static AlphaMap ToDebugMap(TrimapLabel[] tri, int w, int h)
    {
        float[] v = new float[tri.Length];
        for (int i = 0; i < tri.Length; i++)
            v[i] = tri[i] switch
            {
                TrimapLabel.Foreground => 1f,
                TrimapLabel.Unknown => 0.5f,
                _ => 0f
            };
        return new AlphaMap(w, h, v);
    }
}
