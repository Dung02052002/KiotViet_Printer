namespace KiotVietLabelPrinter.Models;

public class BackgroundRemovalItem
{
    // Định danh ổn định (không đổi khi danh sách bị sắp xếp/lọc) — dùng đặt tên
    // file cache preview cho từng ảnh.
    public Guid Id { get; } = Guid.NewGuid();

    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);

    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "";

    public ImageProcessStatus Status { get; set; } = ImageProcessStatus.Waiting;
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }

    // Dữ liệu soi + sửa mask, ghi ra thư mục tạm sau khi xử lý xong (xem
    // BackgroundRemoverView.CachePreviewLayers). Chỉ dùng trong Preview, không
    // phải file xuất.
    //   MaskPreviewPath       : PNG gray8, alpha cuối cùng.
    //   ForegroundPreviewPath : PNG đục, màu foreground đã khử nhiễm biên.
    // Tách đôi thay vì một ảnh RGBA để màu không bị mất ở vùng alpha = 0 — xem
    // BgRemovalInspection.
    public string? MaskPreviewPath { get; set; }
    public string? ForegroundPreviewPath { get; set; }

    // Người dùng đã sửa mask bằng cọ và bấm áp dụng.
    public bool MaskEdited { get; set; }

    public bool HasPreviewLayers =>
        !string.IsNullOrEmpty(MaskPreviewPath) && !string.IsNullOrEmpty(ForegroundPreviewPath);

    public string StatusText => Status switch
    {
        ImageProcessStatus.Waiting => "Waiting",
        ImageProcessStatus.Processing => "Processing",
        ImageProcessStatus.Done => MaskEdited ? "Done ✎" : "Done",
        ImageProcessStatus.Error => "Error",
        _ => ""
    };
}
