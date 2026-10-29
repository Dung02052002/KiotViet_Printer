using System.Security.Cryptography;
using Newtonsoft.Json;

namespace KiotVietLabelPrinter.Infrastructure.Update;

/// <summary>
/// Danh sách file chương trình của một bản phát hành (đường dẫn tương đối +
/// SHA-256 + kích thước), đính kèm release dưới tên <see cref="AssetName"/>.
/// <see cref="Patch"/> là các file đã đổi so với bản phát hành liền trước —
/// chúng nằm trong file zip cập nhật từng phần (<see cref="PatchAssetSuffix"/>).
///
/// Chỉ gồm file CHƯƠNG TRÌNH: dữ liệu người dùng (Config, Data, Output, logs...)
/// không bao giờ nằm trong manifest nên không bao giờ bị cập nhật ghi đè.
/// </summary>
public sealed class UpdateManifest
{
    public const string AssetName = "update-manifest.json";
    public const string PatchAssetSuffix = "_Patch.zip";

    /// <summary>Thư mục cấp 1 chứa dữ liệu người dùng / file app tự sinh khi chạy.</summary>
    private static readonly string[] UserDataFolders = ["config", "data", "output", "logs", "print_logs", "debug_xml"];

    [JsonProperty("version")]
    public string Version { get; set; } = "";

    [JsonProperty("files")]
    public List<Entry> Files { get; set; } = new();

    [JsonProperty("patch")]
    public PatchInfo? Patch { get; set; }

    public sealed class Entry
    {
        /// <summary>Đường dẫn tương đối, phân cách bằng '/'.</summary>
        [JsonProperty("path")]
        public string Path { get; set; } = "";

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonProperty("size")]
        public long Size { get; set; }

        /// <summary>File tại <paramref name="root"/> đúng y bản này (so kích thước trước, khớp mới băm).</summary>
        public bool MatchesFileIn(string root)
        {
            FileInfo file = new(ResolvePath(root, Path));
            return file.Exists && file.Length == Size && string.Equals(HashFile(file.FullName), Sha256, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class PatchInfo
    {
        /// <summary>Bản phát hành liền trước — gói cập nhật từng phần tính chênh lệch so với bản này.</summary>
        [JsonProperty("from")]
        public string From { get; set; } = "";

        /// <summary>Các file có trong gói cập nhật từng phần.</summary>
        [JsonProperty("files")]
        public List<string> Files { get; set; } = new();
    }

    /// <summary>Lập manifest cho thư mục bản build (publish), bỏ qua dữ liệu người dùng.</summary>
    public static UpdateManifest Create(string root, string version)
    {
        root = System.IO.Path.GetFullPath(root);
        UpdateManifest manifest = new() { Version = version };
        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
            if (!IsProgramFile(relative))
                continue;
            manifest.Files.Add(new Entry { Path = relative, Sha256 = HashFile(file), Size = new FileInfo(file).Length });
        }
        manifest.Files.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));
        return manifest;
    }

    /// <summary>Ghi phần <see cref="Patch"/>: các file khác <paramref name="previous"/> (đổi nội dung hoặc mới thêm).</summary>
    public void SetPatchFrom(UpdateManifest previous)
    {
        Dictionary<string, string> before = previous.Files.ToDictionary(f => f.Path, f => f.Sha256, StringComparer.OrdinalIgnoreCase);
        Patch = new PatchInfo
        {
            From = previous.Version,
            Files = Files
                .Where(f => !before.TryGetValue(f.Path, out string? hash) || !string.Equals(hash, f.Sha256, StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Path)
                .ToList()
        };
    }

    /// <summary>Các file của bản này mà thư mục cài <paramref name="root"/> chưa có đúng nội dung.</summary>
    public List<Entry> FilesToUpdate(string root) => Files.Where(f => !f.MatchesFileIn(root)).ToList();

    public static bool IsProgramFile(string relativePath)
    {
        string[] parts = relativePath.Split('/', '\\');
        if (parts.Length > 1 && UserDataFolders.Contains(parts[0], StringComparer.OrdinalIgnoreCase))
            return false;
        return !PatchUpdater.IsUpdateLeftover(relativePath);
    }

    public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

    /// <exception cref="InvalidDataException">Manifest hỏng / có đường dẫn trỏ ra ngoài thư mục cài.</exception>
    public static UpdateManifest Parse(string json)
    {
        UpdateManifest manifest = JsonConvert.DeserializeObject<UpdateManifest>(json)
                                  ?? throw new InvalidDataException("Manifest cập nhật rỗng.");
        foreach (Entry entry in manifest.Files)
        {
            if (!IsSafeRelativePath(entry.Path) || !IsProgramFile(entry.Path) || entry.Sha256.Length != 64 || entry.Size < 0)
                throw new InvalidDataException($"Manifest cập nhật có mục không hợp lệ: \"{entry.Path}\".");
        }
        return manifest;
    }

    /// <summary>Đường dẫn tuyệt đối của một mục trong <paramref name="root"/>.</summary>
    public static string ResolvePath(string root, string relativePath) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar)));

    // Không cho manifest (tải từ mạng) ghi ra ngoài thư mục cài: cấm đường dẫn tuyệt đối và "..".
    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        !System.IO.Path.IsPathRooted(path) &&
        !path.Split('/', '\\').Any(part => part is "" or "." or "..");

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
