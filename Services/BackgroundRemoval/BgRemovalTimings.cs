namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Thời gian (ms) từng giai đoạn xử lý một ảnh. Ghi log từng ảnh + cộng dồn để
// hiển thị trung bình trong UI.
public sealed class BgRemovalTimings
{
    public long DecodeMs { get; set; }
    public long PreprocessMs { get; set; }
    public long InferenceMs { get; set; }
    public long MattingMs { get; set; }
    public long RefineMs { get; set; }
    public long DecontamMs { get; set; }
    public long CompositeMs { get; set; }
    public long SaveMs { get; set; }
    public long TotalMs { get; set; }

    public string ToLogLine(string fileName, QualityMode mode, string provider) =>
        $"[{mode}] [{provider}] {fileName} — " +
        $"decode {DecodeMs} · pre {PreprocessMs} · infer {InferenceMs} · " +
        $"matting {MattingMs} · refine {RefineMs} · decontam {DecontamMs} · " +
        $"composite {CompositeMs} · save {SaveMs} · TOTAL {TotalMs} ms";
}
