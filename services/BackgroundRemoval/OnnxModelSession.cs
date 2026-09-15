using System.Diagnostics;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

internal readonly record struct InferResult(AlphaMap Alpha, long PreprocessMs, long InferenceMs);

// Bọc một InferenceSession ONNX cho đúng một model. Tạo một lần, tái sử dụng cho
// mọi ảnh (chủ sở hữu vòng đời là BackgroundRemovalService). Preprocess/postprocess
// bám theo cấu hình trong BgModelInfo — không đoán tham số.
internal sealed class OnnxModelSession : IDisposable
{
    private readonly BgModelInfo _info;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly object _runLock = new();

    public string ProviderDescription { get; }

    private OnnxModelSession(BgModelInfo info, InferenceSession session, string providerDescription)
    {
        _info = info;
        _session = session;
        _inputName = session.InputMetadata.Keys.First();
        ProviderDescription = providerDescription;
    }

    public static OnnxModelSession Create(string modelPath, BgModelInfo info, InferenceDevice device)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException($"Không tìm thấy model ONNX: {modelPath}");

        if (device != InferenceDevice.Cpu)
        {
            InferenceSession? gpuSession = null;
            try
            {
                using SessionOptions gpuOptions = new();
                gpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                gpuOptions.AppendExecutionProvider_DML(0);
                gpuOptions.EnableProfiling = true;
                // File trace ghi ra thư mục tạm (không phải CWD) để không rơi vãi
                // vào thư mục app khi verify/dispose gặp lỗi.
                gpuOptions.ProfileOutputPathPrefix =
                    Path.Combine(Path.GetTempPath(), $"ort_bg_probe_{Guid.NewGuid():N}");

                gpuSession = new InferenceSession(modelPath, gpuOptions);

                bool onDml = VerifyActiveProvider(gpuSession, "DmlExecutionProvider");
                if (onDml)
                {
                    BackgroundRemovalDiagnosticsLog.Write(
                        $"ONNX [{info.DisplayName}] Provider: DirectML (xác nhận từ ORT profiling trace)");
                    return new OnnxModelSession(info, gpuSession, "DirectML");
                }

                DisposeAndDropProfile(gpuSession);
                BackgroundRemovalDiagnosticsLog.Write(
                    $"ONNX [{info.DisplayName}] DirectML EP đăng ký được nhưng trace không thấy node chạy trên DML — fallback CPU.");
            }
            catch (Exception ex)
            {
                if (gpuSession != null)
                    DisposeAndDropProfile(gpuSession);
                BackgroundRemovalDiagnosticsLog.Write(
                    $"ONNX [{info.DisplayName}] DirectML unavailable, fallback CPU ({ex.GetType().Name}: {ex.Message})");
            }
        }

        SessionOptions cpuOptions = new();
        cpuOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        InferenceSession cpuSession = new(modelPath, cpuOptions);

        BackgroundRemovalDiagnosticsLog.Write($"ONNX [{info.DisplayName}] Provider: CPU");
        return new OnnxModelSession(info, cpuSession, "CPU");
    }

    // Đóng session profiling và xoá file trace nó vừa flush ra đĩa.
    private static void DisposeAndDropProfile(InferenceSession session)
    {
        string? trace = null;
        try { trace = session.EndProfiling(); } catch { /* có thể đã kết thúc */ }
        try { session.Dispose(); } catch { /* ignore */ }
        try
        {
            if (!string.IsNullOrWhiteSpace(trace) && File.Exists(trace))
                File.Delete(trace);
        }
        catch
        {
            // Dọn file trace tạm là phụ trợ.
        }
    }

    // Suy luận ra soft alpha ở đúng resolution model (InputSize×InputSize).
    // Người gọi tự resize alpha về resolution cần dùng.
    public InferResult Infer(SKBitmap sourceRgba, CancellationToken token)
    {
        int size = _info.InputSize;
        token.ThrowIfCancellationRequested();

        Stopwatch sw = Stopwatch.StartNew();
        float[] tensorData;
        using (SKBitmap resized = ResizeTo(sourceRgba, size, size))
            tensorData = BuildInputTensor(resized, size);
        long preprocessMs = sw.ElapsedMilliseconds;

        token.ThrowIfCancellationRequested();

        DenseTensor<float> input = new(tensorData, new[] { 1, 3, size, size });
        List<NamedOnnxValue> inputs = new() { NamedOnnxValue.CreateFromTensor(_inputName, input) };

        lock (_runLock)
        {
            sw.Restart();
            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            Tensor<float> output = results.First().AsTensor<float>();

            int h = output.Dimensions[^2];
            int w = output.Dimensions[^1];
            float[] raw = new float[w * h];

            // output[0, 0, y, x] — batch 0, channel 0 (khớp rembg: ort_outs[0][:,0,:,:]).
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    raw[(y * w) + x] = output[0, 0, y, x];

            float[] alpha = PostProcess(raw);
            long inferenceMs = sw.ElapsedMilliseconds;

            return new InferResult(new AlphaMap(w, h, alpha), preprocessMs, inferenceMs);
        }
    }

    private float[] PostProcess(float[] raw)
    {
        float[] v = raw;

        if (_info.PostProcess is MaskPostProcess.SigmoidThenMinMax or MaskPostProcess.SigmoidClampOnly)
        {
            for (int i = 0; i < v.Length; i++)
                v[i] = 1f / (1f + MathF.Exp(-v[i]));
        }

        if (_info.PostProcess is MaskPostProcess.SigmoidThenMinMax or MaskPostProcess.MinMaxOnly)
        {
            float mi = float.MaxValue, ma = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i] < mi) mi = v[i];
                if (v[i] > ma) ma = v[i];
            }
            float range = MathF.Max(ma - mi, 1e-6f);
            for (int i = 0; i < v.Length; i++)
                v[i] = (v[i] - mi) / range;
        }

        for (int i = 0; i < v.Length; i++)
            v[i] = v[i] < 0f ? 0f : v[i] > 1f ? 1f : v[i];

        return v;
    }

    private static SKBitmap ResizeTo(SKBitmap source, int width, int height)
    {
        SKImageInfo info = new(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKSamplingOptions sampling = new(SKCubicResampler.Mitchell);
        return source.Resize(info, sampling)
            ?? throw new InvalidOperationException("Resize ảnh đầu vào model thất bại.");
    }

    // rembg BaseSession.normalize(): resize RGB → /divisor → (x-mean)/std → CHW.
    private float[] BuildInputTensor(SKBitmap resized, int size)
    {
        byte[] px = resized.Bytes;
        int rowBytes = resized.RowBytes;

        float divisor = 255f;
        if (_info.DivideByImageMax)
        {
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
            divisor = maxVal;
        }

        float[] mean = _info.Mean;
        float[] std = _info.Std;
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
                tensor[p] = (((px[i] / divisor) - mean[0]) / std[0]);
                tensor[plane + p] = (((px[i + 1] / divisor) - mean[1]) / std[1]);
                tensor[(2 * plane) + p] = (((px[i + 2] / divisor) - mean[2]) / std[2]);
            }
        }

        return tensor;
    }

    // Bật profiling, chạy 1 lần suy luận "khởi động" với tensor rỗng đúng shape,
    // rồi đọc trace Chrome-Trace-Format thật của ORT để biết provider nào thực sự
    // chạy node — AppendExecutionProvider_DML không throw chỉ nói EP đăng ký được.
    private static bool VerifyActiveProvider(InferenceSession session, string providerName)
    {
        string? traceFile = null;
        try
        {
            string inputName = session.InputMetadata.Keys.First();
            int[] rawDims = session.InputMetadata[inputName].Dimensions;
            int[] dims = rawDims.Select(d => d <= 0 ? 1 : d).ToArray();
            int total = dims.Aggregate(1, (acc, d) => acc * d);

            DenseTensor<float> warmup = new(new float[total], dims);
            List<NamedOnnxValue> inputs = new() { NamedOnnxValue.CreateFromTensor(inputName, warmup) };

            using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs))
                _ = results;

            traceFile = session.EndProfiling();
            if (string.IsNullOrWhiteSpace(traceFile) || !File.Exists(traceFile))
                return false;

            return File.ReadAllText(traceFile).Contains(providerName, StringComparison.Ordinal);
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
                // Dọn file trace tạm là phụ trợ.
            }
        }
    }

    public void Dispose() => _session.Dispose();
}
