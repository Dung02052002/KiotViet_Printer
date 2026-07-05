namespace KiotVietLabelPrinter.Models;

public class AppConfig
{
    public string BarTenderExe { get; set; } = "";
    public string LastFolder { get; set; } = "";
    public bool AutoOpenLastFolder { get; set; } = true;

    public bool RememberEmployee { get; set; } = true;
    public string DefaultEmployee { get; set; } = "";

    public List<LabelDefinition> Labels { get; set; } = new();
}