using Newtonsoft.Json;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ConfigService
{
    private static ConfigService? _instance;
    public static ConfigService Instance => _instance ??= new ConfigService();

    private readonly string _configPath;

    public AppConfig Config { get; private set; } = new();

    private ConfigService()
    {
        _configPath = Path.Combine(
            Application.StartupPath,
            "Config",
            "config.json");

        Load();
    }

    /// <summary>
    /// Thông báo cho người dùng khi lần Load gần nhất phải khôi phục config
    /// từ bản sao lưu hoặc tạo mới (null = đọc bình thường).
    /// </summary>
    public string? LoadWarning { get; private set; }

    public void ClearLoadWarning() => LoadWarning = null;

    private string BackupPath => _configPath + ".bak";

    // Không xoá LoadWarning ở đầu Load(): constructor đã Load() một lần rồi
    // Program gọi Load() lại — lần 2 đọc được bản vừa khôi phục nhưng cảnh
    // báo của lần 1 vẫn phải tới được người dùng (Program hiển thị rồi xoá).
    public void Load()
    {
        _preserveRealFileBeforeSave = false;

        try
        {
            string? folder = Path.GetDirectoryName(_configPath);

            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
        }
        catch
        {
            // Không tạo được thư mục — TryRead bên dưới sẽ tự thất bại.
        }

        // KHÔNG BAO GIỜ ghi đè config đang có bằng config mặc định chỉ vì đọc
        // lỗi (JSON hỏng do mất điện giữa lúc ghi, file đang bị OneDrive/
        // antivirus khoá...): thử bản sao lưu .bak trước, và luôn giữ lại file
        // hỏng để có thể khôi phục bằng tay.
        if (TryRead(_configPath, out AppConfig? config, out string? error))
        {
            Config = config!;
        }
        else if (TryRead(BackupPath, out AppConfig? backup, out _))
        {
            PreserveBrokenFile();
            Config = backup!;
            LoadWarning =
                "File cấu hình bị lỗi nên đã được khôi phục từ bản sao lưu gần nhất." +
                (error == null ? "" : $"\n\nChi tiết: {error}");
            TrySave();
        }
        else if (!File.Exists(_configPath) && !File.Exists(BackupPath))
        {
            // Lần chạy đầu tiên: chưa có config.
            Config = CreateDefaultConfig();
            TrySave();
            return;
        }
        else
        {
            bool preserved = PreserveBrokenFile();
            Config = CreateDefaultConfig();
            LoadWarning =
                "Không đọc được file cấu hình và không có bản sao lưu hợp lệ — " +
                "phần mềm tạm dùng cấu hình mặc định, vui lòng kiểm tra lại Cấu hình." +
                (preserved ? $"\n\nFile cũ đã được giữ lại trong thư mục:\n{Path.GetDirectoryName(_configPath)}" : "") +
                (error == null ? "" : $"\n\nChi tiết: {error}");

            // File gốc còn đó (đang bị khoá) thì không ghi đè lên nó — và các
            // lần Save() sau trong phiên này (lưu thư mục gần nhất, cache
            // BarTender...) phải giữ lại bản thật trước khi ghi cấu hình tạm.
            if (preserved || !File.Exists(_configPath))
                TrySave();
            else
                _preserveRealFileBeforeSave = true;

            return;
        }

        NormalizeLoadedConfig();
    }

    private static bool TryRead(string path, out AppConfig? config, out string? error)
    {
        config = null;
        error = null;

        if (!File.Exists(path))
            return false;

        // Retry ngắn khi file đang bị tiến trình khác giữ tạm thời.
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                string json = File.ReadAllText(path);

                if (string.IsNullOrWhiteSpace(json))
                {
                    error = "File cấu hình rỗng.";
                    return false;
                }

                config = JsonConvert.DeserializeObject<AppConfig>(json);

                if (config == null)
                {
                    error = "File cấu hình không có dữ liệu.";
                    return false;
                }

                return true;
            }
            catch (IOException ex) when (attempt < 5)
            {
                error = ex.Message;
                Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Đổi tên file config hỏng thành config.json.bad-&lt;thời gian&gt; để không
    /// bị mất. Trả về false nếu không làm được (ví dụ file đang bị khoá).
    /// </summary>
    private bool PreserveBrokenFile()
    {
        try
        {
            if (!File.Exists(_configPath))
                return false;

            File.Move(_configPath, $"{_configPath}.bad-{DateTime.Now:yyyyMMdd_HHmmss}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void TrySave()
    {
        try
        {
            Save();
        }
        catch
        {
            // Không lưu được lúc khởi động — giữ config trong bộ nhớ, lần
            // Save() sau (khi người dùng đổi cấu hình) sẽ báo lỗi thật.
        }
    }

    private void NormalizeLoadedConfig()
    {
        bool changed = false;

        // Phòng trường hợp config cũ hoặc labels null — chỉ bổ sung danh
        // sách tem mặc định, KHÔNG xoá các thiết lập khác (BarTender, máy in,
        // thư mục gần nhất...).
        if (Config.Labels == null || Config.Labels.Count == 0)
        {
            Config.Labels = CreateDefaultConfig().Labels;
            changed = true;
        }

        // Config cũ (trước khi có CÔNG CỤ HÌNH ẢNH) sẽ không có Tools —
        // bổ sung mặc định thay vì bắt người dùng xoá config để có lại card.
        if (Config.Tools == null || Config.Tools.Count == 0)
        {
            Config.Tools = DefaultTools();
            changed = true;
        }
        // Config cũ (trước khi có "Giảm dung lượng ảnh") sẽ có Tools nhưng
        // thiếu đúng tool này — bổ sung thay vì bắt người dùng xoá config.
        else if (!Config.Tools.Any(t => t.Code == "IMAGE_COMPRESS"))
        {
            Config.Tools.Add(ImageCompressTool());
            changed = true;
        }

        // Lỗi lưu phần bổ sung không được làm mất config đã đọc được.
        if (changed)
            TrySave();
    }

    private static readonly object SaveLock = new();

    // true = Config đang là cấu hình TẠM (mặc định) vì không đọc được file
    // thật đang bị khoá — xem Load() và Save().
    private bool _preserveRealFileBeforeSave;

    /// <summary>
    /// Ghi nguyên tử: ghi ra config.json.tmp rồi thay thế file thật, file cũ
    /// được giữ làm config.json.bak. Mất điện / app bị tắt giữa lúc ghi không
    /// thể để lại file config bị cụt.
    /// </summary>
    public void Save()
    {
        string? folder = Path.GetDirectoryName(_configPath);

        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string json = JsonConvert.SerializeObject(
            Config,
            Formatting.Indented);

        lock (SaveLock)
        {
            // Đang dùng cấu hình TẠM vì file thật bị khoá lúc mở app: chép
            // file thật ra .bad-* trước. Nếu không, lần lưu này đẩy bản thật
            // sang .bak và lần lưu kế tiếp ghi đè luôn .bak → mất trắng cấu hình.
            if (_preserveRealFileBeforeSave && File.Exists(_configPath))
            {
                try
                {
                    File.Copy(_configPath, $"{_configPath}.bad-{DateTime.Now:yyyyMMdd_HHmmss}", overwrite: true);
                }
                catch (Exception ex)
                {
                    throw new IOException(
                        "Không lưu cấu hình vì file cấu hình gốc vẫn đang bị khoá " +
                        "(ghi đè lúc này sẽ làm mất cấu hình thật). Hãy đóng và mở lại phần mềm.",
                        ex);
                }

                _preserveRealFileBeforeSave = false;
            }

            string tempPath = _configPath + ".tmp";

            using (FileStream fs = new(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (StreamWriter writer = new(fs, new System.Text.UTF8Encoding(false)))
            {
                writer.Write(json);
                writer.Flush();
                fs.Flush(true);
            }

            if (File.Exists(_configPath))
                File.Replace(tempPath, _configPath, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, _configPath);
        }
    }

    public bool IsConfigured()
    {
        if (string.IsNullOrWhiteSpace(Config.BarTenderExe))
            return false;

        if (Config.Labels == null || Config.Labels.Count == 0)
            return false;

        return Config.Labels.Any(x =>
            !string.IsNullOrWhiteSpace(x.TemplatePath) &&
            !string.IsNullOrWhiteSpace(x.DataFilePath));
    }

    private AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            BarTenderExe = "",
            LastFolder = "",
            LastExcelFile = "",
            AutoOpenLastFolder = true,
            RememberEmployee = true,
            DefaultEmployee = "",
            Labels = new List<LabelDefinition>
            {
                new LabelDefinition
                {
                    Code = "FULL",
                    Name = "Tem đầy đủ",
                    Description = "Tên + thuộc tính + mã vạch + giá",
                    IconText = "🧾",
                    IsEnabled = true,
                    TemplatePath = "",
                    DataFilePath = "",
                    HandlerType = "FULL",
                    RequiresEmployeeCode = false,
                    UseBarcodeParser = false,
                    AppendEmployeeCode = false,
                    TargetNameColumnIndex = 5
                },
                new LabelDefinition
                {
                    Code = "BARCODE",
                    Name = "Tem mã vạch",
                    Description = "Mã parser + mã nhân viên",
                    IconText = "🏷",
                    IsEnabled = true,
                    TemplatePath = "",
                    DataFilePath = "",
                    HandlerType = "BARCODE",
                    RequiresEmployeeCode = true,
                    UseBarcodeParser = true,
                    AppendEmployeeCode = true,
                    TargetNameColumnIndex = 5
                }
            },
            Tools = DefaultTools()
        };
    }

    private static List<ToolDefinition> DefaultTools()
    {
        return new List<ToolDefinition>
        {
            new ToolDefinition
            {
                Code = "BG_REMOVE",
                Name = "Xóa nền ảnh",
                Description = "Xóa nền tự động + thay nền trắng",
                IsEnabled = true
            },
            ImageCompressTool()
        };
    }

    private static ToolDefinition ImageCompressTool()
    {
        return new ToolDefinition
        {
            Code = "IMAGE_COMPRESS",
            Name = "Giảm dung lượng ảnh",
            Description = "Nén và tối ưu dung lượng ảnh hàng loạt",
            IsEnabled = true
        };
    }
}