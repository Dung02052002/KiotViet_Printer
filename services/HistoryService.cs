using Newtonsoft.Json;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class HistoryService
{
    private readonly string _historyPath;

    public HistoryService()
    {
        _historyPath = Path.Combine(
            Application.StartupPath,
            "data",
            "history.json");
    }

    public List<PrintHistory> Load()
    {
        if (!File.Exists(_historyPath))
            return new List<PrintHistory>();

        string json = File.ReadAllText(_historyPath);

        return JsonConvert.DeserializeObject<List<PrintHistory>>(json)
               ?? new List<PrintHistory>();
    }

    public void Save(List<PrintHistory> histories)
    {
        string? folder = Path.GetDirectoryName(_historyPath);

        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string json = JsonConvert.SerializeObject(
            histories,
            Formatting.Indented);

        File.WriteAllText(_historyPath, json);
    }

    public void Add(PrintHistory history)
    {
        List<PrintHistory> histories = Load();

        histories.Insert(0, history);

        Save(histories);
    }
}