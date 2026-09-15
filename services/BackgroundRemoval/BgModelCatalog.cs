namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

public enum BgModelId
{
    // IS-Net / DIS general — model cũ, vẫn đóng gói sẵn trong assets/Models làm
    // fallback offline khi chưa tải được BiRefNet.
    IsNetGeneral,

    // BiRefNet_lite (backbone Swin-V1-Tiny) — segmentation chính cho mọi mode.
    BiRefNetLite,

    // BiRefNet-matting (trimap-free, alpha liên tục) — chỉ dùng ở mode Ultra để
    // refine dải biên unknown.
    BiRefNetMatting
}

// Cách hậu xử lý output thô của model thành soft alpha [0..1].
public enum MaskPostProcess
{
    // Không sigmoid (graph đã có sẵn / model DIS) → chỉ min-max normalize toàn cục.
    MinMaxOnly,

    // sigmoid(x) → min-max normalize. Dùng cho BiRefNet segmentation (theo rembg).
    SigmoidThenMinMax,

    // sigmoid(x) → chỉ clamp [0..1], GIỮ NGUYÊN độ mềm (không kéo giãn tương phản).
    // Dùng cho model matting.
    SigmoidClampOnly
}

public sealed class BgModelInfo
{
    public required BgModelId Id { get; init; }
    public required string FileName { get; init; }

    // Bundled = nằm sẵn trong assets/Models (đi kèm bản build). Ngược lại phải tải.
    public bool Bundled { get; init; }

    public string? PrimaryUrl { get; init; }
    public string? FallbackUrl { get; init; }

    // SHA-256 hex (lowercase) + kích thước byte để verify sau khi tải.
    public string? Sha256 { get; init; }
    public long SizeBytes { get; init; }

    public int InputSize { get; init; } = 1024;

    // Chuẩn hoá ImageNet mặc định (BiRefNet). IS-Net dùng mean 0.5 / std 1.0.
    public float[] Mean { get; init; } = [0.485f, 0.456f, 0.406f];
    public float[] Std { get; init; } = [0.229f, 0.224f, 0.225f];

    // IS-Net (theo rembg) chia pixel cho max(pixel toàn ảnh); BiRefNet dùng /255
    // đúng chuẩn ToTensor gốc. Khác biệt chỉ xuất hiện khi ảnh không có pixel sáng
    // tối đa — giữ đúng từng model để không lệch so với bản tham chiếu.
    public bool DivideByImageMax { get; init; }

    public MaskPostProcess PostProcess { get; init; } = MaskPostProcess.SigmoidThenMinMax;

    public string DisplayName { get; init; } = "";

    public string ApproxDownloadSize =>
        SizeBytes <= 0 ? "" : $"{SizeBytes / 1024d / 1024d:0} MB";
}

public static class BgModelCatalog
{
    // Nguồn (đã kiểm chứng lúc lập kế hoạch):
    //   BiRefNet_lite  : rembg release v0.0.0 (byte-identical với onnx-community/BiRefNet_lite-ONNX)
    //   BiRefNet-matting: emrikol/birefnet-matting-onnx (export fp32 của ZhengPeng7/BiRefNet-matting)
    // Cả hai licence MIT. Sau khi tải, inference chạy hoàn toàn offline/local.
    public static readonly BgModelInfo IsNetGeneral = new()
    {
        Id = BgModelId.IsNetGeneral,
        FileName = "isnet-general-use.onnx",
        Bundled = true,
        InputSize = 1024,
        Mean = [0.5f, 0.5f, 0.5f],
        Std = [1.0f, 1.0f, 1.0f],
        DivideByImageMax = true,
        PostProcess = MaskPostProcess.MinMaxOnly,
        DisplayName = "IS-Net general (dự phòng)"
    };

    public static readonly BgModelInfo BiRefNetLite = new()
    {
        Id = BgModelId.BiRefNetLite,
        FileName = "birefnet-lite-swinT-epoch232.onnx",
        Bundled = false,
        PrimaryUrl = "https://github.com/danielgatis/rembg/releases/download/v0.0.0/BiRefNet-general-bb_swin_v1_tiny-epoch_232.onnx",
        FallbackUrl = "https://huggingface.co/onnx-community/BiRefNet_lite-ONNX/resolve/main/onnx/model.onnx",
        Sha256 = "5600024376f572a557870a5eb0afb1e5961636bef4e1e22132025467d0f03333",
        SizeBytes = 224_005_088,
        InputSize = 1024,
        PostProcess = MaskPostProcess.SigmoidThenMinMax,
        DisplayName = "BiRefNet-lite"
    };

    public static readonly BgModelInfo BiRefNetMatting = new()
    {
        Id = BgModelId.BiRefNetMatting,
        FileName = "birefnet-matting-fp32.onnx",
        Bundled = false,
        PrimaryUrl = "https://huggingface.co/emrikol/birefnet-matting-onnx/resolve/main/birefnet-matting.onnx",
        FallbackUrl = null,
        Sha256 = "f0843e38f6a4e88efc8c5fad4178ad7ed6c818346ce12f82e7b579324fe7e0c5",
        SizeBytes = 940_840_787,
        InputSize = 1024,
        PostProcess = MaskPostProcess.SigmoidClampOnly,
        DisplayName = "BiRefNet-matting"
    };

    public static BgModelInfo Get(BgModelId id) => id switch
    {
        BgModelId.IsNetGeneral => IsNetGeneral,
        BgModelId.BiRefNetLite => BiRefNetLite,
        BgModelId.BiRefNetMatting => BiRefNetMatting,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };
}
