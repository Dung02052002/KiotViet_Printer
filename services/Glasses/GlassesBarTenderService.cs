using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesBarTenderService
{
    private readonly BarTenderService _barTenderService = new();

    public void Print(
        LabelDefinition label,
        string glassesInfo)
    {
        if (string.IsNullOrWhiteSpace(label.TemplatePath))
            throw new Exception("Chưa cấu hình file BarTender.");

        if (!File.Exists(label.TemplatePath))
            throw new Exception(
                $"Không tìm thấy file:\n{label.TemplatePath}");

        Dictionary<string, string> namedSubStrings = new()
        {
            ["GLASSES_INFO"] = glassesInfo
        };

        _barTenderService.Print(
            label.TemplatePath,
            namedSubStrings);
    }
}