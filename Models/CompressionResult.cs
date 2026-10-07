namespace KiotVietLabelPrinter.Models;

public class CompressionResult
{
    public required string InputPath { get; init; }
    public string? OutputPath { get; set; }

    public long OriginalBytes { get; set; }
    public long OutputBytes { get; set; }

    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int OutputWidth { get; set; }
    public int OutputHeight { get; set; }

    public long SavedBytes { get; set; }
    public double SavedPercent { get; set; }

    // true = ảnh nén ra lớn hơn/không nhỏ hơn ảnh gốc (cùng format) nên đã giữ
    // nguyên bản gốc thay vì dùng bản nén — xem ImageCompressionService.Compress.
    public bool OriginalKept { get; set; }

    public TimeSpan ProcessingTime { get; set; }
    public string? Error { get; set; }

    public bool Success => Error == null;
}
