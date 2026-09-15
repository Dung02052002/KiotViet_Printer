using KiotVietLabelPrinter.Services.BackgroundRemoval;

namespace KiotVietLabelPrinter.Services;

public enum InferenceDevice
{
    Auto,
    Cpu,
    Gpu
}

public class BackgroundRemovalResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public BgRemovalTimings? Timings { get; set; }

    // Ảnh kiểm tra chất lượng mask cho Preview (mask cuối + cutout). Chỉ khác null
    // khi ProcessAsync được gọi với inspect = true. Người gọi phải Dispose.
    public BgRemovalInspection? Inspection { get; set; }
}

// Façade cho tính năng "Xóa nền ảnh".
//
// Vòng đời model: mọi InferenceSession được tạo MỘT lần trong EnsureLoaded và
// tái sử dụng cho toàn bộ batch — không bao giờ new InferenceSession trong vòng
// lặp xử lý từng ảnh. Đổi thiết bị (CPU/GPU) mới dispose + nạp lại.
//
// Model:
//   - BiRefNet-lite  : segmentation chính (tải tự động lần đầu, verify SHA-256).
//   - BiRefNet-matting: chỉ nạp khi QualityMode.Ultra.
//   - IS-Net general  : đóng gói sẵn, fallback khi chưa tải được BiRefNet.
//
// Inference chạy 100% local (CPU hoặc DirectML). Không API, không cloud.
public sealed class BackgroundRemovalService : IDisposable
{
    private readonly object _sessionLock = new();
    private readonly BgModelStore _store = new();
    private readonly Dictionary<BgModelId, OnnxModelSession> _sessions = new();

    private InferenceDevice? _loadedDevice;
    private BgModelId? _segmentationModel;

    public string ModelCacheDir => _store.CacheDir;

    public string ActiveProviderDescription { get; private set; } = "Chưa tải model";

    public string ActiveSegmentationModelName =>
        _segmentationModel is { } id ? BgModelCatalog.Get(id).DisplayName : "chưa tải";

    // Có thể chạy được ngay (ít nhất có model segmentation nào đó trên đĩa).
    public bool CanRunOffline =>
        _store.IsAvailable(BgModelId.BiRefNetLite) || _store.IsAvailable(BgModelId.IsNetGeneral);

    public bool IsMattingModelAvailable => _store.IsAvailable(BgModelId.BiRefNetMatting);

    public IReadOnlyList<BgModelId> GetMissingModels(QualityMode mode) => _store.MissingFor(mode);

    public IReadOnlyList<BgModelInfo> DescribeMissingModels(QualityMode mode) =>
        _store.MissingFor(mode).Select(BgModelCatalog.Get).ToList();

    // Tải các model còn thiếu cho mode. An toàn khi gọi nhiều lần (bỏ qua model đã có).
    public Task EnsureModelsDownloadedAsync(
        QualityMode mode,
        IProgress<BgModelDownloadProgress>? progress,
        CancellationToken token)
        => _store.EnsureAsync(_store.MissingFor(mode), progress, token);

    // Nạp/xác nhận session TRƯỚC batch để lỗi (thiếu file, EP không hỗ trợ...)
    // hiện ra ngay thay vì rơi vào giữa danh sách ảnh.
    public void EnsureLoaded(QualityMode mode, InferenceDevice device)
    {
        lock (_sessionLock)
        {
            if (_loadedDevice != device)
            {
                DisposeSessions();
                _loadedDevice = device;
            }

            BgModelId segId = _store.IsAvailable(BgModelId.BiRefNetLite)
                ? BgModelId.BiRefNetLite
                : BgModelId.IsNetGeneral;

            LoadSession(segId, device);
            _segmentationModel = segId;
            ActiveProviderDescription = _sessions[segId].ProviderDescription;

            if (mode.NeedsMattingModel())
            {
                if (!_store.IsAvailable(BgModelId.BiRefNetMatting))
                    throw new FileNotFoundException(
                        "Mode Tối đa cần model BiRefNet-matting nhưng chưa tải về.");
                LoadSession(BgModelId.BiRefNetMatting, device);
            }
        }
    }

    private void LoadSession(BgModelId id, InferenceDevice device)
    {
        if (_sessions.ContainsKey(id))
            return;

        string path = _store.ResolvePath(id);
        BgModelInfo info = BgModelCatalog.Get(id);
        _sessions[id] = OnnxModelSession.Create(path, info, device);
    }

    public Task<BackgroundRemovalResult> ProcessAsync(
        string sourcePath,
        string outputFolder,
        QualityMode mode,
        InferenceDevice device,
        bool debug,
        bool inspect,
        CancellationToken token)
        => Task.Run(() => ProcessCore(sourcePath, outputFolder, mode, device, debug, inspect, token), token);

    private BackgroundRemovalResult ProcessCore(
        string sourcePath,
        string outputFolder,
        QualityMode mode,
        InferenceDevice device,
        bool debug,
        bool inspect,
        CancellationToken token)
    {
        try
        {
            EnsureLoaded(mode, device);
            token.ThrowIfCancellationRequested();

            OnnxModelSession segmenter;
            OnnxModelSession? matting = null;
            lock (_sessionLock)
            {
                segmenter = _sessions[_segmentationModel!.Value];
                if (mode.NeedsMattingModel())
                    matting = _sessions[BgModelId.BiRefNetMatting];
            }

            BgRemovalPipeline pipeline = new(segmenter, matting);
            BgRemovalOutcome outcome = pipeline.Run(
                new BgRemovalRequest
                {
                    SourcePath = sourcePath,
                    OutputFolder = outputFolder,
                    Mode = mode,
                    Debug = debug,
                    Inspect = inspect
                },
                token);

            return new BackgroundRemovalResult
            {
                Success = true,
                OutputPath = outcome.OutputPath,
                Timings = outcome.Timings,
                Inspection = outcome.Inspection
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            BackgroundRemovalDiagnosticsLog.Write(
                $"Lỗi xử lý {Path.GetFileName(sourcePath)}: {ex.GetType().Name}: {ex.Message}");
            return new BackgroundRemovalResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private void DisposeSessions()
    {
        foreach (OnnxModelSession session in _sessions.Values)
            session.Dispose();
        _sessions.Clear();
        _segmentationModel = null;
    }

    public void Dispose()
    {
        lock (_sessionLock)
            DisposeSessions();
    }
}
