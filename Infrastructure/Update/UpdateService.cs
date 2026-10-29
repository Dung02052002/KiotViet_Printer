using System.Diagnostics;
using System.Net.Http.Headers;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using Newtonsoft.Json.Linq;

namespace KiotVietLabelPrinter.Infrastructure.Update;

/// <summary>Một bản phát hành mới hơn bản đang chạy.</summary>
/// <param name="DownloadUrl">File Setup đầy đủ — luôn có, dùng khi không cập nhật từng phần được.</param>
/// <param name="ManifestUrl">Manifest + gói cập nhật từng phần (null nếu release không đính kèm, vd bản cũ).</param>
public sealed record UpdateInfo(
    Version Version, string Tag, string Notes, string DownloadUrl, string FileName, long Size,
    string? ManifestUrl = null, string? PatchUrl = null, long PatchSize = 0);

/// <summary>
/// Tự cập nhật qua GitHub Releases: đọc release mới nhất của repo, so với
/// <see cref="AppInfo.Version"/>. Ưu tiên cập nhật từng phần (chỉ tải các
/// file đã đổi — xem <see cref="PatchUpdater"/>); không được thì tải file
/// Setup (.exe đính kèm release) về thư mục tạm rồi chạy bộ cài ở chế độ im
/// lặng. Bộ cài (installer.iss) tự tắt app, cài đè giữ nguyên Config/Data và
/// mở lại app sau khi cài xong.
///
/// Cách phát hành bản mới: tăng &lt;Version&gt; trong .csproj, chạy
/// build-installer.bat, tạo Release trên GitHub với tag "v{Version}" và đính
/// kèm MỌI file trong installer_output: Setup .exe, update-manifest.json và
/// gói _Patch.zip (nếu có).
/// </summary>
public static class UpdateService
{
    internal const string Repo = "Dung02052002/KiotViet_Printer";
    private const string LatestReleaseApi = $"https://api.github.com/repos/{Repo}/releases/latest";

    internal static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        HttpClient client = new() { Timeout = TimeSpan.FromMinutes(10) };
        // GitHub API từ chối request không có User-Agent.
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("InTemKiotViet", AppInfo.Version));
        return client;
    }

    /// <returns>Bản mới hơn bản đang chạy, hoặc null nếu đã là bản mới nhất.</returns>
    /// <exception cref="Exception">Lỗi mạng / GitHub — người gọi tự quyết định có báo hay không.</exception>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken ct = default)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        using HttpRequestMessage request = new(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using HttpResponseMessage response = await Http.SendAsync(request, timeout.Token).ConfigureAwait(false);
        // 404: repo chưa có release nào.
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        JObject release = JObject.Parse(await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false));
        if (release.Value<bool>("draft") || release.Value<bool>("prerelease"))
            return null;

        string tag = release.Value<string>("tag_name") ?? "";
        if (!TryParseVersion(tag, out Version latest) || !TryParseVersion(AppInfo.Version, out Version current))
            return null;
        if (latest <= current)
            return null;

        JArray assets = release["assets"] as JArray ?? new JArray();
        JToken? FindAsset(Func<string, bool> match) => assets.FirstOrDefault(a => match(a.Value<string>("name") ?? ""));

        JToken? asset = FindAsset(name => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        JToken? manifest = FindAsset(name => name.Equals(UpdateManifest.AssetName, StringComparison.OrdinalIgnoreCase));
        JToken? patch = FindAsset(name => name.EndsWith(UpdateManifest.PatchAssetSuffix, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            StartupLog.Stage($"Cập nhật: release {tag} không có file .exe đính kèm — bỏ qua");
            return null;
        }

        return new UpdateInfo(
            latest,
            tag,
            (release.Value<string>("body") ?? "").Trim(),
            asset.Value<string>("browser_download_url") ?? "",
            asset.Value<string>("name") ?? "Setup.exe",
            asset.Value<long?>("size") ?? 0,
            manifest?.Value<string>("browser_download_url"),
            patch?.Value<string>("browser_download_url"),
            patch?.Value<long?>("size") ?? 0);
    }

    /// <summary>Tải file Setup về %TEMP%\InTemKiotViet_Update. progress nhận 0..100.</summary>
    public static async Task<string> DownloadAsync(UpdateInfo update, IProgress<int>? progress, CancellationToken ct = default)
    {
        string folder = Path.Combine(Path.GetTempPath(), "InTemKiotViet_Update");
        Directory.CreateDirectory(folder);
        string target = Path.Combine(folder, update.FileName);
        await DownloadFileAsync(update.DownloadUrl, target, update.Size, progress, ct).ConfigureAwait(false);
        return target;
    }

    /// <summary>Tải về <paramref name="target"/> (ghi ra file tạm, đủ dữ liệu mới đổi tên). progress nhận 0..100.</summary>
    internal static async Task DownloadFileAsync(string url, string target, long expectedSize, IProgress<int>? progress, CancellationToken ct)
    {
        string partial = target + ".download";

        using (HttpResponseMessage response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            long total = response.Content.Headers.ContentLength ?? expectedSize;

            await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using FileStream file = new(partial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            byte[] buffer = new byte[81920];
            long received = 0;
            int lastPercent = -1;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                received += read;
                if (total > 0)
                {
                    int percent = (int)(received * 100 / total);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress?.Report(percent);
                    }
                }
            }

            // File tải dở (mất mạng giữa chừng) không được đem đi chạy.
            if (total > 0 && received != total)
                throw new IOException($"Tải chưa đủ dữ liệu ({received}/{total} byte).");
        }

        File.Move(partial, target, overwrite: true);
    }

    /// <summary>
    /// Chạy bộ cài ở chế độ /SILENT (chỉ hiện thanh tiến trình, không hỏi gì).
    /// Windows có thể hỏi quyền quản trị (UAC). Người gọi phải thoát app ngay
    /// sau đó để bộ cài ghi đè được file .exe.
    /// </summary>
    public static void LaunchInstaller(string installerPath)
    {
        StartupLog.Stage($"Cập nhật: chạy bộ cài {installerPath}");
        Process.Start(new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true
        });
    }

    private static bool TryParseVersion(string text, out Version version)
    {
        text = text.Trim().TrimStart('v', 'V');
        int dash = text.IndexOfAny(['-', '+']);
        if (dash > 0)
            text = text[..dash];
        if (!Version.TryParse(text, out Version? parsed))
        {
            version = new Version();
            return false;
        }

        // "2.3" và "2.3.0" phải bằng nhau (Version coi Build = -1 nhỏ hơn 0).
        version = new Version(parsed.Major, parsed.Minor, Math.Max(parsed.Build, 0), Math.Max(parsed.Revision, 0));
        return true;
    }
}
