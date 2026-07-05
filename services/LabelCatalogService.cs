using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class LabelCatalogService
{
    public List<LabelDefinition> GetAllEnabled()
    {
        return ConfigService.Instance.Config.Labels
            .Where(x => x.IsEnabled)
            .OrderBy(x => x.Name)
            .ToList();
    }

    public List<LabelDefinition> GetAll()
    {
        return ConfigService.Instance.Config.Labels
            .OrderBy(x => x.Name)
            .ToList();
    }

    public LabelDefinition GetByCode(string code)
    {
        return ConfigService.Instance.Config.Labels
            .FirstOrDefault(x => x.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"Không tìm thấy cấu hình tem: {code}");
    }
}