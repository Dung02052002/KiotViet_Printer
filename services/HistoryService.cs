using System.Text.Json;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class HistoryService
{
    private readonly string _historyFile;

    public HistoryService()
    {
        _historyFile = Path.Combine(Application.StartupPath, "Data", "history.json");

        string? folder = Path.GetDirectoryName(_historyFile);
        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        if (!File.Exists(_historyFile))
        {
            File.WriteAllText(_historyFile, "[]");
        }
    }

    public List<PrintHistory> GetAll()
    {
        try
        {
            if (!File.Exists(_historyFile))
                return new List<PrintHistory>();

            string json = File.ReadAllText(_historyFile);
            if (string.IsNullOrWhiteSpace(json))
                return new List<PrintHistory>();

            return JsonSerializer.Deserialize<List<PrintHistory>>(json) ?? new List<PrintHistory>();
        }
        catch
        {
            return new List<PrintHistory>();
        }
    }

    public void Add(PrintHistory item)
    {
        List<PrintHistory> items = GetAll();
        items.Add(item);
        SaveAll(items);
    }

    public void SaveAll(List<PrintHistory> items)
    {
        string json = JsonSerializer.Serialize(items, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(_historyFile, json);
    }
}