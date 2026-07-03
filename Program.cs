using KiotViet_Label_Printer_Pro_V2;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        ConfigService.Instance.Load();

        Application.Run(new Form1());
    }
}