namespace KiotVietLabelPrinter.Models;

public class CompressionItem
{
    // Định danh ổn định — không đổi khi danh sách bị lọc/sắp xếp.
    public Guid Id { get; } = Guid.NewGuid();

    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);

    public long OriginalBytes { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public string OriginalResolution =>
        OriginalWidth > 0 && OriginalHeight > 0 ? $"{OriginalWidth} × {OriginalHeight}" : "";

    public CompressionStatus Status { get; set; } = CompressionStatus.Waiting;

    public string? OutputPath { get; set; }
    public long OutputBytes { get; set; }
    public int OutputWidth { get; set; }
    public int OutputHeight { get; set; }
    public double SavedPercent { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsFinished =>
        Status is CompressionStatus.Done or CompressionStatus.OriginalKept
            or CompressionStatus.Error or CompressionStatus.Cancelled;

    // Tính vào tổng dung lượng trước/sau — chỉ những ảnh đã có file output thật
    // (nén hoặc giữ nguyên), không tính ảnh lỗi/hủy/đang chờ.
    public bool CountsTowardTotals => Status is CompressionStatus.Done or CompressionStatus.OriginalKept;

    public string StatusText => Status switch
    {
        CompressionStatus.Waiting => "Waiting",
        CompressionStatus.Compressing => "Compressing",
        CompressionStatus.Done => "Done",
        CompressionStatus.OriginalKept => "Original kept",
        CompressionStatus.Error => "Error",
        CompressionStatus.Cancelled => "Cancelled",
        _ => ""
    };
}
