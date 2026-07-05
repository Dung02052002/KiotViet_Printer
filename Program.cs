using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        ConfigService.Instance.Load();

        Application.Run(new FormMain());
    }
}