using Newtonsoft.Json;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public sealed class ConfigService
{
    private static ConfigService? _instance;

    public static ConfigService Instance
    {
        get
        {
            _instance ??= new ConfigService();

            return _instance;
        }
    }

    private readonly string _configPath;

    public AppConfig Config { get; private set; } = new();

    private ConfigService()
    {
        _configPath = Path.Combine(
            Application.StartupPath,
            "Config",
            "config.json");
    }

    public void Load()
    {
        if (!File.Exists(_configPath))
        {
            Save();

            return;
        }

        string json = File.ReadAllText(_configPath);

        Config = JsonConvert.DeserializeObject<AppConfig>(json)
                 ?? new AppConfig();
    }

    public void Save()
    {
        string folder = Path.GetDirectoryName(_configPath)!;

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string json = JsonConvert.SerializeObject(
            Config,
            Formatting.Indented);

        File.WriteAllText(_configPath, json);
    }

    public bool IsConfigured()
    {
        return
            File.Exists(Config.BarTenderExe) &&
            File.Exists(Config.FullLabel.Template) &&
            File.Exists(Config.FullLabel.Data) &&
            File.Exists(Config.BarcodeLabel.Template) &&
            File.Exists(Config.BarcodeLabel.Data);
    }
}