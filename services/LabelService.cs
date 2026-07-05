namespace KiotVietLabelPrinter.Services;

public class LabelService
{
    private readonly BarTenderService _barTenderService = new();

    public void Print(bool printFull, bool printBarcode)
    {
        var config = ConfigService.Instance.Config;

        if (printFull)
        {
            _barTenderService.Print(config.FullLabel.Template);
        }

        if (printBarcode)
        {
            _barTenderService.Print(config.BarcodeLabel.Template);
        }
    }
}