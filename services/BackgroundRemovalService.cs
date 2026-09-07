using System.Runtime.InteropServices;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

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
}

// Xóa nền ảnh chạy hoàn toàn local bằng ONNX Runtime (model isnet-general-use,
// kiến trúc IS-Net/DIS, Apache-2.0 — bản đóng gói ONNX lấy từ dự án rembg mã
// nguồn mở: https://github.com/danielgatis/rembg).
//
// Preprocessing/postprocessing bên dưới lấy nguyên từ chính source code xử lý
// ảnh của rembg (rembg/sessions/base.py + dis_general_use.py) — không đoán —
// để khớp đúng những gì model được huấn luyện/kỳ vọng:
//   - Resize RGB về 1024x1024.
//   - Chuẩn hoá: (pixel / max(pixel trong ảnh)) rồi trừ 0.5 (mean=0.5, std=1.0).
//   - Output lấy channel 0, kéo giãn min-max toàn cục về [0..1] (không có sigmoid
//     trong đồ thị) rồi resize mask mềm về đúng kích thước ảnh gốc.
//
// InferenceSession được tạo một lần và tái sử dụng cho mọi ảnh (EnsureLoaded) —
// KHÔNG được new InferenceSession trong vòng lặp xử lý từng ảnh.
public sealed class BackgroundRemovalService : IDisposable
{
    private const int ModelSize = 1024;

    private readonly object _sessionLock = new();
    private InferenceSession? _session;
    private string? _inputName;
    private InferenceDevice? _loadedDevice;

    public string ModelPath { get; } = Path.Combine(AppContext.BaseDirectory, "assets", "Models", "isnet-general-use.onnx");

    public bool IsModelAvailable => File.Exists(ModelPath);

    // Provider ONNX Runtime THỰC SỰ đang chạy — không chỉ "AppendExecutionProvider_DML
    // không throw". Xem VerifyActiveProvider: bật profiling, chạy 1 lần suy luận
    // khởi động, rồi đọc trace thật của ORT để biết node có chạy trên
    // DmlExecutionProvider hay bị rơi về CPUExecutionProvider.
    public string ActiveProviderDescription { get; private set; } = "Chưa tải model";

    // Gọi trước khi xử lý batch để lỗi load model (thiếu file, EP không hỗ trợ...)
    // hiện ra ngay, thay vì rơi vào giữa danh sách ảnh đang xử lý.
    public void EnsureLoaded(InferenceDevice device)
    {
        lock (_sessionLock)
        {
            if (_session != null && _loadedDevice == device)
                return;

            _session?.Dispose();
            _session = null;

            if (!IsModelAvailable)
                throw new FileNotFoundException($"Không tìm thấy model ONNX: {ModelPath}");

            _session = CreateSession(device);
            _inputName = _session.InputMetadata.Keys.First();
            _loadedDevice = device;
        }
    }

    private InferenceSession CreateSession(InferenceDevice device)
    {
        // DirectML chạy được trên GPU của mọi hãng có driver DirectX 12 (không
        // riêng NVIDIA/CUDA). Nếu máy không có GPU phù hợp hoặc driver không hỗ
        // trợ, AppendExecutionProvider_DML ném exception ngay tại đây — bắt lại
        // và rơi về CPU thay vì để app crash.
        if (device != InferenceDevice.Cpu)
        {
            InferenceSession? gpuSession = null;

            try
            {
                using SessionOptions gpuOptions = new();
                gpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                gpuOptions.AppendExecutionProvider_DML(0);

                // Bắt buộc để VerifyActiveProvider có trace thật để đọc — không bật
                // thì EndProfiling() không trả về gì đáng tin cậy.
                gpuOptions.EnableProfiling = true;

                gpuSession = new InferenceSession(ModelPath, gpuOptions);

                // AppendExecutionProvider_DML không throw KHÔNG có nghĩa là các node
                // thực sự chạy trên GPU — ORT có thể lặng lẽ rơi từng node không hỗ
                // trợ về CPUExecutionProvider. Xác nhận bằng trace thật của ORT.
                bool actuallyOnDml = VerifyActiveProvider(gpuSession, "DmlExecutionProvider");

                if (actuallyOnDml)
                {
                    ActiveProviderDescription = "DirectML";
                    BackgroundRemovalDiagnosticsLog.Write("ONNX Provider: DirectML (xác nhận từ ORT profiling trace)");
                    return gpuSession;
                }

                gpuSession.Dispose();
                BackgroundRemovalDiagnosticsLog.Write(
                    "DirectML EP đã đăng ký nhưng trace không thấy DmlExecutionProvider chạy node nào " +
                    "(có thể driver hỗ trợ 1 phần) — fallback to CPU.");
            }
            catch (Exception ex)
            {
                gpuSession?.Dispose();
                BackgroundRemovalDiagnosticsLog.Write(
                    $"DirectML unavailable, fallback to CPU ({ex.GetType().Name}: {ex.Message})");
            }
        }

        SessionOptions cpuOptions = new();
        cpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        InferenceSession cpuSession = new(ModelPath, cpuOptions);

        ActiveProviderDescription = "CPU";
        BackgroundRemovalDiagnosticsLog.Write("ONNX Provider: CPU");
        return cpuSession;
    }

    // Bật profiling, chạy đúng 1 lần suy luận "khởi động" với tensor rỗng (đúng
    // shape model cần), rồi đọc file trace Chrome-Trace-Format mà chính ORT ghi
    // ra — trace liệt kê từng node đã thực thi kèm tên execution provider thật.
    // Đây là cách duy nhất được ORT hỗ trợ để biết provider nào THỰC SỰ chạy,
    // AppendExecutionProvider_DML không throw chỉ nói lên là EP đăng ký được.
    private static bool VerifyActiveProvider(InferenceSession session, string providerName)
    {
        string prefix = Path.Combine(Path.GetTempPath(), $"ort_probe_{Guid.NewGuid():N}_");
        string? traceFile = null;

        try
        {
            string inputName = session.InputMetadata.Keys.First();
            int[] rawDims = session.InputMetadata[inputName].Dimensions;

            // Trục động (batch...) báo về -1/0 — thay bằng 1 để dựng tensor khởi
            // động hợp lệ; model thật của tính năng này luôn cố định 1x3x1024x1024
            // nên chỉ trục batch (nếu có) mới rơi vào trường hợp này.
            int[] dims = rawDims.Select(d => d <= 0 ? 1 : d).ToArray();
            int size = dims.Aggregate(1, (acc, d) => acc * d);

            DenseTensor<float> warmupInput = new(new float[size], dims);
            List<NamedOnnxValue> inputs = new() { NamedOnnxValue.CreateFromTensor(inputName, warmupInput) };

            // EnableProfiling đã bật khi tạo session (CreateSession) — chỉ cần chạy
            // suy luận một lần để ORT ghi dữ liệu trace thật.
            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs))
            {
                _ = results;
            }

            traceFile = session.EndProfiling();

            if (string.IsNullOrWhiteSpace(traceFile) || !File.Exists(traceFile))
                return false;

            string json = File.ReadAllText(traceFile);
            return json.Contains(providerName, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                if (traceFile != null && File.Exists(traceFile))
                    File.Delete(traceFile);
            }
            catch
            {
                // Dọn file trace tạm là phụ trợ — không quan trọng nếu xoá lỗi.
            }
        }
    }

    public Task<BackgroundRemovalResult> ProcessAsync(
        string sourcePath,
        string outputFolder,
        InferenceDevice device,
        CancellationToken token)
    {
        return Task.Run(() => ProcessCore(sourcePath, outputFolder, device, token), token);
    }

    private BackgroundRemovalResult ProcessCore(
        string sourcePath,
        string outputFolder,
        InferenceDevice device,
        CancellationToken token)
    {
        try
        {
            EnsureLoaded(device);
            token.ThrowIfCancellationRequested();

            using FileStream sourceStream = File.OpenRead(sourcePath);
            using SKBitmap decoded = SKBitmap.Decode(sourceStream)
                ?? throw new InvalidDataException("Không đọc được ảnh (định dạng không hỗ trợ hoặc file lỗi).");

            using SKBitmap original = decoded.ColorType == SKColorType.Rgba8888
                ? decoded
                : decoded.Copy(SKColorType.Rgba8888);

            int origWidth = original.Width;
            int origHeight = original.Height;

            token.ThrowIfCancellationRequested();

            byte[] maskBytes;
            using (SKBitmap modelInput = ResizeTo(original, ModelSize, ModelSize))
            {
                float[] tensorData = BuildInputTensor(modelInput);
                token.ThrowIfCancellationRequested();
                maskBytes = RunInference(tensorData);
            }

            token.ThrowIfCancellationRequested();

            using SKBitmap maskFull = ResizeMask(maskBytes, ModelSize, ModelSize, origWidth, origHeight);
            using SKBitmap composited = CompositeOnWhite(original, maskFull);

            token.ThrowIfCancellationRequested();

            string outputPath = BuildOutputPath(outputFolder, sourcePath);
            SaveAsPng(composited, outputPath);

            return new BackgroundRemovalResult { Success = true, OutputPath = outputPath };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new BackgroundRemovalResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static SKBitmap ResizeTo(SKBitmap source, int width, int height)
    {
        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKSamplingOptions sampling = new(SKCubicResampler.Mitchell);

        return source.Resize(info, sampling)
            ?? throw new InvalidOperationException("Resize ảnh thất bại.");
    }

    // Đúng theo rembg BaseSession.normalize(): RGB, /max(pixel), (x-0.5)/1.0, CHW.
    private static float[] BuildInputTensor(SKBitmap resized)
    {
        int size = resized.Width;
        byte[] px = resized.Bytes;
        int rowBytes = resized.RowBytes;

        int maxVal = 1;
        for (int y = 0; y < size; y++)
        {
            int rowStart = y * rowBytes;
            for (int x = 0; x < size; x++)
            {
                int i = rowStart + (x * 4);
                if (px[i] > maxVal) maxVal = px[i];
                if (px[i + 1] > maxVal) maxVal = px[i + 1];
                if (px[i + 2] > maxVal) maxVal = px[i + 2];
            }
        }

        float denom = maxVal;
        int plane = size * size;
        float[] tensor = new float[3 * plane];

        for (int y = 0; y < size; y++)
        {
            int rowStart = y * rowBytes;
            int rowOut = y * size;

            for (int x = 0; x < size; x++)
            {
                int i = rowStart + (x * 4);
                int p = rowOut + x;

                tensor[p] = (px[i] / denom) - 0.5f;
                tensor[plane + p] = (px[i + 1] / denom) - 0.5f;
                tensor[(2 * plane) + p] = (px[i + 2] / denom) - 0.5f;
            }
        }

        return tensor;
    }

    private byte[] RunInference(float[] tensorData)
    {
        DenseTensor<float> input = new(tensorData, new[] { 1, 3, ModelSize, ModelSize });
        List<NamedOnnxValue> inputs = new() { NamedOnnxValue.CreateFromTensor(_inputName!, input) };

        lock (_sessionLock)
        {
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session!.Run(inputs);
            Tensor<float> output = results.First().AsTensor<float>();

            int h = output.Dimensions[2];
            int w = output.Dimensions[3];
            int count = h * w;

            float[] raw = new float[count];
            float mi = float.MaxValue;
            float ma = float.MinValue;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = output[0, 0, y, x];
                    raw[(y * w) + x] = v;
                    if (v < mi) mi = v;
                    if (v > ma) ma = v;
                }
            }

            float range = Math.Max(ma - mi, 1e-6f);
            byte[] mask = new byte[count];

            for (int i = 0; i < count; i++)
            {
                float norm = (raw[i] - mi) / range;
                mask[i] = (byte)Math.Clamp(norm * 255f, 0, 255);
            }

            return mask;
        }
    }

    private static SKBitmap ResizeMask(byte[] maskBytes, int maskW, int maskH, int targetW, int targetH)
    {
        SKImageInfo srcInfo = new(maskW, maskH, SKColorType.Gray8, SKAlphaType.Opaque);
        using SKBitmap small = new(srcInfo);
        Marshal.Copy(maskBytes, 0, small.GetPixels(), maskBytes.Length);

        if (targetW == maskW && targetH == maskH)
            return small.Copy(SKColorType.Gray8);

        SKImageInfo targetInfo = new(targetW, targetH, SKColorType.Gray8, SKAlphaType.Opaque);
        SKSamplingOptions sampling = new(SKCubicResampler.Mitchell);

        return small.Resize(targetInfo, sampling)
            ?? throw new InvalidOperationException("Resize mask thất bại.");
    }

    // Output = Foreground * Alpha + White * (1 - Alpha) — giữ soft mask (không
    // threshold 0/1) để tóc/mép vật thể/dây mảnh vẫn mượt. Alpha ép về [0,1] vì
    // resize bằng bộ lọc cubic (Mitchell) có thể ringing nhẹ ra ngoài khoảng gốc.
    private static SKBitmap CompositeOnWhite(SKBitmap original, SKBitmap mask)
    {
        int width = original.Width;
        int height = original.Height;

        byte[] srcPx = original.Bytes;
        int srcRowBytes = original.RowBytes;

        byte[] maskPx = mask.Bytes;
        int maskRowBytes = mask.RowBytes;

        SKImageInfo outInfo = new(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        SKBitmap output = new(outInfo);
        byte[] outPx = new byte[height * outInfo.RowBytes];
        int outRowBytes = outInfo.RowBytes;

        for (int y = 0; y < height; y++)
        {
            int srcRow = y * srcRowBytes;
            int maskRow = y * maskRowBytes;
            int outRow = y * outRowBytes;

            for (int x = 0; x < width; x++)
            {
                int si = srcRow + (x * 4);
                int oi = outRow + (x * 4);
                float a = Math.Clamp(maskPx[maskRow + x] / 255f, 0f, 1f);
                float inv = 1f - a;

                outPx[oi] = (byte)Math.Clamp((srcPx[si] * a) + (255f * inv), 0, 255);
                outPx[oi + 1] = (byte)Math.Clamp((srcPx[si + 1] * a) + (255f * inv), 0, 255);
                outPx[oi + 2] = (byte)Math.Clamp((srcPx[si + 2] * a) + (255f * inv), 0, 255);
                outPx[oi + 3] = 255;
            }
        }

        Marshal.Copy(outPx, 0, output.GetPixels(), outPx.Length);

        return output;
    }

    private static void SaveAsPng(SKBitmap bitmap, string outputPath)
    {
        string? folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(folder))
            Directory.CreateDirectory(folder);

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream fs = File.Create(outputPath);
        data.SaveTo(fs);
    }

    // Không overwrite: ABC123_white-background.png, rồi _2, _3...
    private static string BuildOutputPath(string outputFolder, string sourcePath)
    {
        Directory.CreateDirectory(outputFolder);

        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string candidate = Path.Combine(outputFolder, $"{baseName}_white-background.png");

        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(outputFolder, $"{baseName}_white-background_{suffix}.png");
            suffix++;
        }

        return candidate;
    }

    public void Dispose()
    {
        lock (_sessionLock)
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
