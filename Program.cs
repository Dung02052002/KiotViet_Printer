using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Ten mutex phai khop voi AppMutex trong installer.iss de Inno Setup
        // co the phat hien va dong app dang chay truoc khi cai/ghi de.
        using var mutex = new Mutex(true, "KiotVietLabelPrinterProV2Mutex", out bool isNewInstance);

        if (!isNewInstance)
        {
            MessageBox.Show("Ung dung dang chay roi.", "In Tem KiotViet",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();

        ConfigService.Instance.Load();

        Application.Run(new FormMain());
    }
}