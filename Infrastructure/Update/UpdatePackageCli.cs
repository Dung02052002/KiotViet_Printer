using System.IO.Compression;
using System.Net;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;

namespace KiotVietLabelPrinter.Infrastructure.Update;

/// <summary>
/// Chế độ dòng lệnh build-installer.bat gọi trên CHÍNH bản vừa publish:
///   "KiotViet Label Printer Pro V2.exe" --make-update-package &lt;thư mục publish&gt; &lt;thư mục output&gt;
/// Ghi ra update-manifest.json và (nếu release mới nhất trên GitHub có
/// manifest để so) gói InTemKiotViet_V{Version}_Patch.zip chứa các file đã
/// đổi. Phiên bản lấy từ chính exe đang chạy nên luôn khớp &lt;Version&gt;.
/// Dùng chung <see cref="UpdateManifest"/> với app → cách lập danh sách file
/// lúc build và lúc cập nhật không bao giờ lệch nhau.
/// </summary>
public static class UpdatePackageCli
{
    private const string Arg = "--make-update-package";

    public static bool ShouldRun(string[] args) => args.Any(a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase));

    public static int Run(string[] args)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, Arg, StringComparison.OrdinalIgnoreCase));
        if (i + 2 >= args.Length)
        {
            Console.Error.WriteLine($"Cách dùng: {Arg} <thư mục publish> <thư mục output>");
            return 2;
        }

        try
        {
            UpdateManifest? previous = DownloadLatestManifest();
            string patch = Build(args[i + 1], args[i + 2], AppInfo.Version, previous);
            Console.WriteLine(patch.Length == 0
                ? "Không có manifest của release trước — chỉ tạo update-manifest.json (máy khách sẽ tải bộ cài đầy đủ)."
                : $"Đã tạo gói cập nhật từng phần: {patch}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"LỖI tạo gói cập nhật: {ex}");
            return 1;
        }
    }

    /// <returns>Đường dẫn gói _Patch.zip, hoặc "" nếu không có bản trước để so.</returns>
    internal static string Build(string publishDir, string outputDir, string version, UpdateManifest? previous)
    {
        UpdateManifest manifest = UpdateManifest.Create(publishDir, version);
        Directory.CreateDirectory(outputDir);
        string patchPath = Path.Combine(outputDir, $"InTemKiotViet_V{version}{UpdateManifest.PatchAssetSuffix}");
        File.Delete(patchPath);

        // Build lại đúng bản đã phát hành thì "bản trước" là chính nó — gói rỗng, vô nghĩa.
        if (previous != null && previous.Version != version)
        {
            manifest.SetPatchFrom(previous);
            using ZipArchive zip = ZipFile.Open(patchPath, ZipArchiveMode.Create);
            foreach (string file in manifest.Patch!.Files)
                zip.CreateEntryFromFile(UpdateManifest.ResolvePath(publishDir, file), file, CompressionLevel.Optimal);
        }

        File.WriteAllText(Path.Combine(outputDir, UpdateManifest.AssetName), manifest.ToJson());
        return manifest.Patch == null ? "" : patchPath;
    }

    private static UpdateManifest? DownloadLatestManifest()
    {
        string url = $"https://github.com/{UpdateService.Repo}/releases/latest/download/{UpdateManifest.AssetName}";
        using HttpResponseMessage response = UpdateService.Http.GetAsync(url).GetAwaiter().GetResult();
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        UpdateManifest previous = UpdateManifest.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        Console.WriteLine($"So với release trước: v{previous.Version}");
        return previous;
    }
}
