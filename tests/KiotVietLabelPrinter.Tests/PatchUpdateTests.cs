using KiotVietLabelPrinter.Infrastructure.Update;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Cập nhật từng phần: build tạo đúng gói chênh lệch, máy ở bản liền trước chỉ
/// thay đúng các file đã đổi, dữ liệu người dùng không bị đụng, lỗi giữa chừng
/// thì thư mục cài trở lại nguyên trạng, máy không đủ điều kiện thì dùng bộ cài.
/// </summary>
public class PatchUpdateTests
{
    private readonly string _root = TestData.NewTempDir();

    [Fact]
    public void Manifest_SkipsUserData_AndPatchListsOnlyChangedOrNewFiles()
    {
        string v1 = Release("v1", ("app.dll", "một"), ("lib.dll", "thư viện"), ("assets/model.onnx", "mô hình"), ("config/config.json", "{}"));
        string v2 = Release("v2", ("app.dll", "hai"), ("lib.dll", "thư viện"), ("assets/model.onnx", "mô hình"), ("vi/new.dll", "mới"), ("config/config.json", "{}"));

        UpdateManifest manifest = UpdateManifest.Create(v2, "2.0.0");
        manifest.SetPatchFrom(UpdateManifest.Create(v1, "1.0.0"));

        Assert.DoesNotContain(manifest.Files, f => f.Path.StartsWith("config/"));
        Assert.Equal("1.0.0", manifest.Patch!.From);
        Assert.Equal(["app.dll", "vi/new.dll"], manifest.Patch.Files);
    }

    [Fact]
    public void PreviousVersion_PatchReplacesOnlyChangedFiles_KeepsUserData()
    {
        (string install, string zip, UpdateManifest manifest) = Scenario();
        File.WriteAllText(Path.Combine(install, "config", "config.json"), "cấu hình của người dùng");

        PatchPlan plan = PatchUpdater.Plan(manifest, "https://example/patch.zip", 0, install)!;
        Assert.Equal(["app.dll", "vi/new.dll"], plan.Files.Select(f => f.Path));

        string staging = Path.Combine(_root, "staging");
        PatchUpdater.Extract(zip, staging, plan.Files);
        PatchUpdater.Apply(install, staging, plan.Files);

        Assert.Empty(manifest.FilesToUpdate(install));
        Assert.Equal("cấu hình của người dùng", File.ReadAllText(Path.Combine(install, "config", "config.json")));

        // File cũ được đổi tên (lúc chạy thật còn bị khoá), bản mới mở lên thì xoá.
        Assert.True(File.Exists(Path.Combine(install, "app.dll.update-old")));
        PatchUpdater.CleanupLeftovers(install);
        Assert.Empty(Directory.GetFiles(install, "*.update-old", SearchOption.AllDirectories));
        Assert.Empty(manifest.FilesToUpdate(install));
    }

    [Fact]
    public void FailureMidway_RollsBackEverything()
    {
        (string install, string zip, UpdateManifest manifest) = Scenario();
        UpdateManifest before = UpdateManifest.Create(install, "1.0.0");
        PatchPlan plan = PatchUpdater.Plan(manifest, "https://example/patch.zip", 0, install)!;

        string staging = Path.Combine(_root, "staging");
        PatchUpdater.Extract(zip, staging, plan.Files);
        File.Delete(Path.Combine(staging, "vi", "new.dll")); // file thứ hai hỏng → lỗi sau khi đã thay file đầu

        Assert.ThrowsAny<IOException>(() => PatchUpdater.Apply(install, staging, plan.Files));
        Assert.Empty(before.FilesToUpdate(install));
        Assert.False(File.Exists(Path.Combine(install, "vi", "new.dll")));
        Assert.Empty(Directory.GetFiles(install, "*.update-old", SearchOption.AllDirectories));
    }

    [Fact]
    public void FileNotInPatch_Differs_FallsBackToInstaller()
    {
        (string install, _, UpdateManifest manifest) = Scenario();
        // Máy bỏ qua một bản: lib.dll khác cả bản trước lẫn bản mới, gói không có.
        File.WriteAllText(Path.Combine(install, "lib.dll"), "thư viện bản rất cũ");

        Assert.Null(PatchUpdater.Plan(manifest, "https://example/patch.zip", 0, install));
    }

    [Fact]
    public void CorruptFileInPackage_IsRejectedBeforeTouchingInstall()
    {
        (string install, string zip, UpdateManifest manifest) = Scenario();
        PatchPlan plan = PatchUpdater.Plan(manifest, "https://example/patch.zip", 0, install)!;
        manifest.Files.Single(f => f.Path == "app.dll").Sha256 = new string('0', 64);

        Assert.Throws<InvalidDataException>(() => PatchUpdater.Extract(zip, Path.Combine(_root, "staging"), plan.Files));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("C:/Windows/evil.dll")]
    [InlineData("config/config.json")]
    public void Parse_RejectsPathsOutsideProgramFiles(string path)
    {
        string json = $$"""{"version":"2.0.0","files":[{"path":"{{path}}","sha256":"{{new string('a', 64)}}","size":1}]}""";
        Assert.Throws<InvalidDataException>(() => UpdateManifest.Parse(json));
    }

    /// <summary>Máy đang cài bản 1.0.0; bản 2.0.0 đổi app.dll và thêm vi/new.dll.</summary>
    private (string Install, string Zip, UpdateManifest Manifest) Scenario()
    {
        (string, string)[] v1Files = [("app.dll", "một"), ("lib.dll", "thư viện"), ("config/config.json", "{}")];
        string v1 = Release("v1", v1Files);
        string v2 = Release("v2", ("app.dll", "hai"), ("lib.dll", "thư viện"), ("vi/new.dll", "mới"), ("config/config.json", "{}"));
        string install = Release("install", v1Files);

        string output = Path.Combine(_root, "out");
        string zip = UpdatePackageCli.Build(v2, output, "2.0.0", UpdateManifest.Create(v1, "1.0.0"));
        UpdateManifest manifest = UpdateManifest.Parse(File.ReadAllText(Path.Combine(output, UpdateManifest.AssetName)));
        return (install, zip, manifest);
    }

    private string Release(string name, params (string Path, string Content)[] files)
    {
        string dir = Path.Combine(_root, name);
        foreach ((string path, string content) in files)
        {
            string file = Path.Combine(dir, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, content);
        }
        return dir;
    }
}
