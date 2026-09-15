using System.Security.Cryptography;

namespace KiotVietLabelPrinter.Services.BackgroundRemoval;

public sealed class BgModelDownloadProgress
{
    public required string ModelDisplayName { get; init; }
    public long BytesReceived { get; init; }
    public long TotalBytes { get; init; }
    public string Phase { get; init; } = "";

    public double Fraction => TotalBytes > 0
        ? Math.Clamp((double)BytesReceived / TotalBytes, 0, 1)
        : 0;
}

// Định vị + tải model ONNX.
//
//   - Model Bundled (isnet)  : đọc thẳng từ assets/Models đi kèm bản build.
//   - Model tải về (BiRefNet): lưu ở %LOCALAPPDATA%\KiotVietLabelPrinter\Models,
//     verify SHA-256 + kích thước, ghi ra file .part rồi File.Move atomic.
//
// Sau khi có file trên đĩa, mọi suy luận diễn ra offline — việc tải chỉ xảy ra
// một lần cho mỗi model.
public sealed class BgModelStore
{
    private static readonly HttpClient Http = CreateHttpClient();

    private readonly string _bundledDir =
        Path.Combine(AppContext.BaseDirectory, "assets", "Models");

    public string CacheDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KiotVietLabelPrinter", "Models");

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            // Model lớn (~900 MB) trên đường truyền chậm — cho hạn rộng, tiến độ
            // vẫn báo về đều nên người dùng không tưởng bị treo.
            Timeout = TimeSpan.FromHours(2)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KiotVietLabelPrinter/2.0");
        return client;
    }

    public string ResolvePath(BgModelId id)
    {
        BgModelInfo info = BgModelCatalog.Get(id);

        if (info.Bundled)
            return Path.Combine(_bundledDir, info.FileName);

        return Path.Combine(CacheDir, info.FileName);
    }

    public bool IsAvailable(BgModelId id)
    {
        string path = ResolvePath(id);
        if (!File.Exists(path))
            return false;

        BgModelInfo info = BgModelCatalog.Get(id);

        // Với model tải về: kiểm tra nhanh bằng kích thước (verify SHA-256 đầy đủ
        // chỉ chạy ngay sau khi tải xong, không lặp lại mỗi lần mở app).
        if (!info.Bundled && info.SizeBytes > 0)
        {
            try
            {
                if (new FileInfo(path).Length != info.SizeBytes)
                    return false;
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    public IReadOnlyList<BgModelId> MissingFor(QualityMode mode)
    {
        List<BgModelId> missing = new();

        if (!IsAvailable(BgModelId.BiRefNetLite))
            missing.Add(BgModelId.BiRefNetLite);

        if (mode.NeedsMattingModel() && !IsAvailable(BgModelId.BiRefNetMatting))
            missing.Add(BgModelId.BiRefNetMatting);

        return missing;
    }

    public async Task EnsureAsync(
        IEnumerable<BgModelId> ids,
        IProgress<BgModelDownloadProgress>? progress,
        CancellationToken token)
    {
        foreach (BgModelId id in ids)
        {
            if (IsAvailable(id))
                continue;

            await DownloadAsync(id, progress, token).ConfigureAwait(false);
        }
    }

    private async Task DownloadAsync(
        BgModelId id,
        IProgress<BgModelDownloadProgress>? progress,
        CancellationToken token)
    {
        BgModelInfo info = BgModelCatalog.Get(id);

        if (string.IsNullOrWhiteSpace(info.PrimaryUrl))
            throw new InvalidOperationException($"Model {info.DisplayName} không có nguồn tải.");

        Directory.CreateDirectory(CacheDir);

        string finalPath = ResolvePath(id);
        string partPath = finalPath + ".part";

        List<string> urls = new() { info.PrimaryUrl! };
        if (!string.IsNullOrWhiteSpace(info.FallbackUrl))
            urls.Add(info.FallbackUrl!);

        Exception? lastError = null;

        for (int attempt = 0; attempt < urls.Count; attempt++)
        {
            token.ThrowIfCancellationRequested();
            string url = urls[attempt];

            try
            {
                BackgroundRemovalDiagnosticsLog.Write(
                    $"Tải model {info.DisplayName} từ {url}");

                await DownloadToFileAsync(url, partPath, info, progress, token).ConfigureAwait(false);
                VerifyFile(partPath, info);

                if (File.Exists(finalPath))
                    File.Delete(finalPath);
                File.Move(partPath, finalPath);

                BackgroundRemovalDiagnosticsLog.Write(
                    $"Tải xong + verify model {info.DisplayName}: {finalPath}");
                return;
            }
            catch (OperationCanceledException)
            {
                TryDelete(partPath);
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
                TryDelete(partPath);
                BackgroundRemovalDiagnosticsLog.Write(
                    $"Tải model {info.DisplayName} từ {url} lỗi: {ex.GetType().Name}: {ex.Message}");
            }
        }

        throw new IOException(
            $"Không tải được model {info.DisplayName} sau {urls.Count} nguồn. " +
            $"Lỗi cuối: {lastError?.Message}", lastError);
    }

    private static async Task DownloadToFileAsync(
        string url,
        string partPath,
        BgModelInfo info,
        IProgress<BgModelDownloadProgress>? progress,
        CancellationToken token)
    {
        using HttpResponseMessage response = await Http
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? info.SizeBytes;

        await using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using FileStream target = new(
            partPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true);

        byte[] buffer = new byte[1 << 20];
        long received = 0;
        int lastReportedPercent = -1;
        int read;

        while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            received += read;

            int percent = total > 0 ? (int)(received * 100 / total) : -1;
            if (percent != lastReportedPercent)
            {
                lastReportedPercent = percent;
                progress?.Report(new BgModelDownloadProgress
                {
                    ModelDisplayName = info.DisplayName,
                    BytesReceived = received,
                    TotalBytes = total,
                    Phase = "Đang tải"
                });
            }
        }

        await target.FlushAsync(token).ConfigureAwait(false);
    }

    private static void VerifyFile(string path, BgModelInfo info)
    {
        long length = new FileInfo(path).Length;
        if (info.SizeBytes > 0 && length != info.SizeBytes)
        {
            throw new InvalidDataException(
                $"Model {info.DisplayName} sai kích thước: nhận {length:N0} B, cần {info.SizeBytes:N0} B.");
        }

        if (string.IsNullOrWhiteSpace(info.Sha256))
            return;

        using FileStream fs = File.OpenRead(path);
        byte[] hash = SHA256.HashData(fs);
        string hex = Convert.ToHexString(hash).ToLowerInvariant();

        if (!string.Equals(hex, info.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Model {info.DisplayName} sai SHA-256 (file tải về có thể hỏng). " +
                $"Nhận {hex}, cần {info.Sha256}.");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Dọn file .part lỗi là phụ trợ.
        }
    }
}
