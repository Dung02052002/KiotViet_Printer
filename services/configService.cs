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

    public void Load()
    {
        try
        {
            string? folder = Path.GetDirectoryName(_configPath);

            if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(_configPath))
            {
                Config = CreateDefaultConfig();
                Save();
                return;
            }

            string json = File.ReadAllText(_configPath);

            if (string.IsNullOrWhiteSpace(json))
            {
                Config = CreateDefaultConfig();
                Save();
                return;
            }

            Config = JsonConvert.DeserializeObject<AppConfig>(json)
                     ?? CreateDefaultConfig();

            // Phòng trường hợp config cũ hoặc labels null
            if (Config.Labels == null)
                Config.Labels = new List<LabelDefinition>();

            if (Config.Labels.Count == 0)
            {
                Config = CreateDefaultConfig();
                Save();
            }
        }
        catch
        {
            Config = CreateDefaultConfig();
            Save();
        }
    }

    public void Save()
    {
        string? folder = Path.GetDirectoryName(_configPath);

        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string json = JsonConvert.SerializeObject(
            Config,
            Formatting.Indented);

        File.WriteAllText(_configPath, json);
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
            }
        };
    }
}