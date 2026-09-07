using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ImageToolCatalogService
{
    public List<ToolDefinition> GetAllEnabled()
    {
        return ConfigService.Instance.Config.Tools
            .Where(x => x.IsEnabled)
            .ToList();
    }
}
