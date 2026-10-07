using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

// Chạy pipeline xóa nền không cần UI — phục vụ test/benchmark từ dòng lệnh:
//
//   "KiotViet Label Printer Pro V2.exe" --bg-test <ảnh|thư mục> <thư mục lưu>
//        [--mode fast|high|ultra|all] [--device cpu|gpu|auto] [--debug]
//
// In thời gian từng giai đoạn cho mỗi ảnh/mode. Model tự tải lần đầu như trong app.
public static class BgRemovalCli
{
    private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".webp", ".bmp" };

    public static bool ShouldRun(string[] args)
        => args.Any(a => string.Equals(a, "--bg-test", StringComparison.OrdinalIgnoreCase));

    public static int Run(string[] args)
    {
        try
        {
            int i = Array.FindIndex(args, a => string.Equals(a, "--bg-test", StringComparison.OrdinalIgnoreCase));
            List<string> rest = args.Skip(i + 1).ToList();

            string input = rest.ElementAtOrDefault(0)
                ?? throw new ArgumentException("Thiếu đường dẫn ảnh/thư mục đầu vào.");
            string outputFolder = rest.ElementAtOrDefault(1)
                ?? throw new ArgumentException("Thiếu thư mục lưu kết quả.");

            string modeArg = GetOption(rest, "--mode") ?? "all";
            string deviceArg = GetOption(rest, "--device") ?? "auto";
            bool debug = rest.Any(a => string.Equals(a, "--debug", StringComparison.OrdinalIgnoreCase));

            QualityMode[] modes = modeArg.ToLowerInvariant() switch
            {
                "fast" => new[] { QualityMode.Fast },
                "high" => new[] { QualityMode.HighQuality },
                "ultra" => new[] { QualityMode.Ultra },
                _ => new[] { QualityMode.Fast, QualityMode.HighQuality, QualityMode.Ultra }
            };

            InferenceDevice device = deviceArg.ToLowerInvariant() switch
            {
                "cpu" => InferenceDevice.Cpu,
                "gpu" => InferenceDevice.Gpu,
                _ => InferenceDevice.Auto
            };

            List<string> images = ResolveImages(input);
            if (images.Count == 0)
                throw new FileNotFoundException($"Không tìm thấy ảnh hợp lệ tại: {input}");

            Directory.CreateDirectory(outputFolder);

            Console.WriteLine($"== Background Remover CLI ==");
            Console.WriteLine($"Ảnh: {images.Count} · modes: {string.Join(",", modes)} · device: {device} · debug: {debug}");

            using BackgroundRemovalService service = new();
            using CancellationTokenSource cts = new();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            foreach (QualityMode mode in modes)
            {
                IReadOnlyList<BgModelInfo> missing = service.DescribeMissingModels(mode);
                if (missing.Count > 0)
                {
                    Console.WriteLine($"[{mode}] Tải model: {string.Join(", ", missing.Select(m => $"{m.DisplayName} (~{m.ApproxDownloadSize})"))}");
                    Progress<BgModelDownloadProgress> p = new(pr =>
                        Console.Write($"\r  {pr.ModelDisplayName}: {pr.Fraction:P0} ({pr.BytesReceived / 1024 / 1024}/{pr.TotalBytes / 1024 / 1024} MB)   "));
                    service.EnsureModelsDownloadedAsync(mode, p, cts.Token).GetAwaiter().GetResult();
                    Console.WriteLine();
                }

                service.EnsureLoaded(mode, device);
                Console.WriteLine($"[{mode}] Model: {service.ActiveSegmentationModelName} · Provider: {service.ActiveProviderDescription}");

                List<BgRemovalTimings> all = new();
                foreach (string img in images)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    BackgroundRemovalResult r = service
                        .ProcessAsync(img, outputFolder, mode, device, debug, inspect: false, cts.Token)
                        .GetAwaiter().GetResult();
                    sw.Stop();

                    if (!r.Success)
                    {
                        Console.WriteLine($"  [LỖI] {Path.GetFileName(img)}: {r.ErrorMessage}");
                        continue;
                    }

                    all.Add(r.Timings!);
                    BgRemovalTimings t = r.Timings!;
                    Console.WriteLine(
                        $"  {Path.GetFileName(img),-34} decode {t.DecodeMs,4} · pre {t.PreprocessMs,4} · infer {t.InferenceMs,5} · " +
                        $"matting {t.MattingMs,5} · refine {t.RefineMs,4} · decontam {t.DecontamMs,4} · " +
                        $"composite {t.CompositeMs,4} · save {t.SaveMs,4} · TOTAL {t.TotalMs,5} ms");
                }

                if (all.Count > 0)
                {
                    Console.WriteLine(
                        $"[{mode}] TB/ảnh: infer {all.Average(x => x.InferenceMs):0} · matting {all.Average(x => x.MattingMs):0} · " +
                        $"refine {all.Average(x => x.RefineMs):0} · decontam {all.Average(x => x.DecontamMs):0} · " +
                        $"composite {all.Average(x => x.CompositeMs):0} · TOTAL {all.Average(x => x.TotalMs):0} ms");
                }
            }

            Console.WriteLine("Xong. Output: " + outputFolder);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Đã hủy.");
            return 2;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LỖI: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }

    private static string? GetOption(List<string> args, string name)
    {
        int idx = args.FindIndex(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 && idx + 1 < args.Count ? args[idx + 1] : null;
    }

    private static List<string> ResolveImages(string input)
    {
        if (Directory.Exists(input))
        {
            return Directory.GetFiles(input)
                .Where(f => Extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f)
                .ToList();
        }

        if (File.Exists(input) && Extensions.Contains(Path.GetExtension(input).ToLowerInvariant()))
            return new List<string> { input };

        return new List<string>();
    }
}
