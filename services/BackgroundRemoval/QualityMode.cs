namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Ba mức chất lượng cho việc xóa nền — đánh đổi giữa tốc độ và độ chính xác biên.
//
//   Fast        : chỉ AI segmentation + composite trên nền trắng.
//   HighQuality : + mask refinement + khử nhiễm màu biên (edge decontamination).
//   Ultra       : + pass matting ONNX thứ hai (BiRefNet-matting) chỉ để refine
//                 vùng biên "unknown" lấy từ trimap của mask segmentation.
public enum QualityMode
{
    Fast,
    HighQuality,
    Ultra
}

public static class QualityModeExtensions
{
    public static string ToDisplayName(this QualityMode mode) => mode switch
    {
        QualityMode.Fast => "Nhanh",
        QualityMode.HighQuality => "Chất lượng cao",
        QualityMode.Ultra => "Tối đa",
        _ => mode.ToString()
    };

    // Mode cần model matting thứ hai được tải về hay không.
    public static bool NeedsMattingModel(this QualityMode mode) => mode == QualityMode.Ultra;
}
