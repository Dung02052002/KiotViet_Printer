using System.Text;
using KiotVietLabelPrinter.Models.Warehouse;
using Newtonsoft.Json;

namespace KiotVietLabelPrinter.Infrastructure.Storage;

/// <summary>
/// Lưu thiết lập của chức năng Phiếu xuất kho vào file RIÊNG
/// (Config/warehouse-export.json). Không đi qua ConfigService để việc lưu ở
/// đây không bao giờ ghi lại config.json — nơi chứa máy in và danh mục tem.
/// </summary>
public class WarehouseSettingsStore
{
    private readonly string _path;

    public WarehouseSettingsStore()
        : this(Path.Combine(Application.StartupPath, "Config", "warehouse-export.json"))
    {
    }

    public WarehouseSettingsStore(string path)
    {
        _path = path;
    }

    public string FilePath => _path;

    /// <summary>Thông báo khi file thiết lập hỏng và phải dùng mặc định (null = bình thường).</summary>
    public string? LoadWarning { get; private set; }

    public WarehouseExportSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(_path))
            return new WarehouseExportSettings();

        try
        {
            return JsonConvert.DeserializeObject<WarehouseExportSettings>(File.ReadAllText(_path))
                   ?? new WarehouseExportSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadWarning = $"Không đọc được thiết lập Phiếu xuất kho ({Path.GetFileName(_path)}) — tạm dùng mặc định.\nChi tiết: {ex.Message}";
            return new WarehouseExportSettings();
        }
    }

    /// <summary>Ghi nguyên tử: file tạm rồi thay thế, mất điện giữa chừng không để lại file cụt.</summary>
    public void Save(WarehouseExportSettings settings)
    {
        string? folder = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        string temp = _path + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(settings, Formatting.Indented), new UTF8Encoding(false));
        File.Move(temp, _path, overwrite: true);
    }
}
