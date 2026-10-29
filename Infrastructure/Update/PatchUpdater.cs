using System.Diagnostics;
using System.IO.Compression;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;

namespace KiotVietLabelPrinter.Infrastructure.Update;

/// <summary>Các file cần thay để thư mục cài thành đúng bản mới.</summary>
public sealed record PatchPlan(string AppDirectory, UpdateManifest Manifest, IReadOnlyList<UpdateManifest.Entry> Files, string PatchUrl, long PatchSize);

/// <summary>
/// Cập nhật từng phần: chỉ tải các file đã đổi thay vì cả bộ cài (~240 MB).
///
/// 1. <see cref="PlanAsync"/>: đọc manifest của bản mới, so với file đang có
///    trong thư mục cài (theo SHA-256). Dùng được khi mọi file khác biệt đều
///    nằm trong gói cập nhật — tức máy đang ở đúng bản liền trước. Máy bỏ qua
///    nhiều bản, file bị sửa / hỏng, thư mục cài không ghi được... → null, người
///    gọi tải bộ cài đầy đủ như cũ.
/// 2. <see cref="DownloadAndApplyAsync"/>: tải gói, giải nén vào thư mục tạm
///    cạnh app, kiểm tra từng file đúng SHA-256 rồi mới thay. Windows cho đổi
///    tên (không cho ghi đè) file .exe/.dll đang chạy: file cũ được đổi tên
///    thành *.update-old, file mới chuyển vào chỗ. Lỗi giữa chừng thì trả lại
///    toàn bộ như trước — không bao giờ để thư mục cài nửa cũ nửa mới.
/// 3. <see cref="Restart"/>: mở bản mới (bản mới đợi bản cũ thoát hẳn, xem
///    <see cref="WaitForPreviousInstance"/>) rồi người gọi thoát app.
/// 4. Bản mới mở lên được thì <see cref="CleanupLeftovers"/> xoá các file
///    *.update-old (lúc chạy bản cũ chúng còn bị khoá, không xoá được).
///
/// Dữ liệu người dùng không nằm trong manifest nên không bao giờ bị đụng tới.
/// Thư mục cài được bộ cài cấp quyền ghi cho Users (installer.iss, [Dirs]) nên
/// không cần hỏi quyền quản trị.
/// </summary>
public static class PatchUpdater
{
    public const string AfterUpdateArg = "--updated-from-pid";

    private const string LeftoverSuffix = ".update-old";
    private const string StagingFolder = ".update-staging";

    /// <returns>Kế hoạch cập nhật từng phần, hoặc null nếu phải dùng bộ cài đầy đủ.</returns>
    public static async Task<PatchPlan?> PlanAsync(UpdateInfo update, string appDirectory, CancellationToken ct = default)
    {
        if (update.ManifestUrl == null || update.PatchUrl == null)
            return Skip($"release {update.Tag} không có gói cập nhật từng phần");

        string json = await UpdateService.Http.GetStringAsync(update.ManifestUrl, ct).ConfigureAwait(false);
        UpdateManifest manifest = UpdateManifest.Parse(json);
        return await Task.Run(() => Plan(manifest, update.PatchUrl, update.PatchSize, appDirectory), ct).ConfigureAwait(false);
    }

    internal static PatchPlan? Plan(UpdateManifest manifest, string patchUrl, long patchSize, string appDirectory)
    {
        if (manifest.Patch is not { } patch)
            return Skip($"manifest {manifest.Version} không có gói cập nhật từng phần");

        List<UpdateManifest.Entry> needed = manifest.FilesToUpdate(appDirectory);
        if (needed.Count == 0)
            return Skip("mọi file đã đúng bản mới");

        HashSet<string> inPatch = new(patch.Files, StringComparer.OrdinalIgnoreCase);
        if (needed.FirstOrDefault(f => !inPatch.Contains(f.Path)) is { } missing)
            return Skip($"\"{missing.Path}\" khác bản {patch.From} mà gói không có (máy chưa ở bản {patch.From}?)");

        if (!CanWrite(appDirectory))
            return Skip($"không ghi được vào {appDirectory}");

        StartupLog.Stage($"Cập nhật từng phần: {needed.Count} file, gói {patchSize / 1024} KB");
        return new PatchPlan(appDirectory, manifest, needed, patchUrl, patchSize);
    }

    /// <summary>Tải gói, kiểm tra rồi thay file. Lỗi thì thư mục cài giữ nguyên như trước.</summary>
    public static async Task DownloadAndApplyAsync(PatchPlan plan, IProgress<int>? progress, CancellationToken ct = default)
    {
        string staging = Path.Combine(plan.AppDirectory, StagingFolder);
        DeleteDirectory(staging);
        Directory.CreateDirectory(staging);
        try
        {
            string zip = Path.Combine(staging, "patch.zip");
            await UpdateService.DownloadFileAsync(plan.PatchUrl, zip, plan.PatchSize, progress, ct).ConfigureAwait(false);
            await Task.Run(() =>
            {
                Extract(zip, staging, plan.Files);
                Apply(plan.AppDirectory, staging, plan.Files);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            DeleteDirectory(staging);
        }
    }

    /// <summary>Giải nén đúng các file cần thay và kiểm tra SHA-256 từng file.</summary>
    internal static void Extract(string zipPath, string staging, IReadOnlyList<UpdateManifest.Entry> files)
    {
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        foreach (UpdateManifest.Entry file in files)
        {
            ZipArchiveEntry entry = zip.GetEntry(file.Path)
                                    ?? throw new InvalidDataException($"Gói cập nhật thiếu file \"{file.Path}\".");
            string target = UpdateManifest.ResolvePath(staging, file.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
            if (!file.MatchesFileIn(staging))
                throw new InvalidDataException($"File \"{file.Path}\" trong gói cập nhật bị hỏng (sai SHA-256).");
        }
    }

    /// <summary>Thay file đã giải nén vào thư mục cài; lỗi giữa chừng thì hoàn tác toàn bộ.</summary>
    internal static void Apply(string appDirectory, string staging, IReadOnlyList<UpdateManifest.Entry> files)
    {
        // (đích, file cũ đã đổi tên — null nếu là file mới)
        Stack<(string Target, string? Backup)> done = new();
        try
        {
            foreach (UpdateManifest.Entry file in files)
            {
                string target = UpdateManifest.ResolvePath(appDirectory, file.Path);
                string? backup = null;
                if (File.Exists(target))
                {
                    backup = target + LeftoverSuffix;
                    File.Move(target, backup, overwrite: true);
                }
                done.Push((target, backup));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(UpdateManifest.ResolvePath(staging, file.Path), target);
            }
        }
        catch
        {
            Rollback(done);
            throw;
        }
    }

    private static void Rollback(Stack<(string Target, string? Backup)> done)
    {
        while (done.TryPop(out (string Target, string? Backup) step))
        {
            try
            {
                if (File.Exists(step.Target))
                    File.Delete(step.Target);
                if (step.Backup != null)
                    File.Move(step.Backup, step.Target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StartupLog.Error($"Cập nhật từng phần: không hoàn tác được {step.Target}", ex);
            }
        }
    }

    /// <summary>Mở bản vừa cập nhật; người gọi thoát app ngay sau đó.</summary>
    public static void Restart()
    {
        StartupLog.Stage("Cập nhật từng phần xong — mở lại app");
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
        {
            Arguments = $"{AfterUpdateArg} {Environment.ProcessId}",
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        });
    }

    /// <summary>
    /// Mở từ <see cref="Restart"/>: đợi bản cũ thoát hẳn trước khi giành
    /// single-instance mutex — không thì bản mới tưởng app đang chạy rồi.
    /// </summary>
    public static void WaitForPreviousInstance(string[] args)
    {
        int i = Array.FindIndex(args, a => string.Equals(a, AfterUpdateArg, StringComparison.OrdinalIgnoreCase));
        if (i < 0 || i + 1 >= args.Length || !int.TryParse(args[i + 1], out int pid))
            return;

        try
        {
            using Process previous = Process.GetProcessById(pid);
            previous.WaitForExit(TimeSpan.FromSeconds(30));
        }
        catch (ArgumentException)
        {
            // Bản cũ đã thoát.
        }
    }

    /// <summary>Xoá file cũ còn sót lại sau lần cập nhật trước (lúc đó còn bị khoá). Lỗi thì để lần sau.</summary>
    public static void CleanupLeftovers(string appDirectory)
    {
        DeleteDirectory(Path.Combine(appDirectory, StagingFolder));
        foreach (string file in Directory.EnumerateFiles(appDirectory, "*" + LeftoverSuffix, SearchOption.AllDirectories))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                StartupLog.Error($"Cập nhật: chưa xoá được {file}", ex);
            }
        }
    }

    internal static bool IsUpdateLeftover(string relativePath) =>
        relativePath.EndsWith(LeftoverSuffix, StringComparison.OrdinalIgnoreCase) ||
        relativePath.StartsWith(StagingFolder + "/", StringComparison.OrdinalIgnoreCase);

    private static bool CanWrite(string directory)
    {
        string probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void DeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StartupLog.Error($"Cập nhật: không xoá được {directory}", ex);
        }
    }

    private static PatchPlan? Skip(string reason)
    {
        StartupLog.Stage($"Cập nhật từng phần: bỏ qua — {reason}; dùng bộ cài đầy đủ");
        return null;
    }
}
