using System.Diagnostics;

namespace KiotVietLabelPrinter.Services.Glasses.Logging;

public static class GlassesDebug
{
#if DEBUG
    public static bool Enabled = true;
#else
    public static bool Enabled = false;
#endif

    public static void Title(string title)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine("");
        System.Diagnostics.Debug.WriteLine(new string('=', 60));
        System.Diagnostics.Debug.WriteLine(title);
        System.Diagnostics.Debug.WriteLine(new string('=', 60));
    }

    public static void Info(string message)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine(message);
    }

    public static void Success(string message)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine("[OK] " + message);
    }

    public static void Warning(string message)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine("[WARN] " + message);
    }

    public static void Error(string message)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine("[ERROR] " + message);
    }

    public static void Separator()
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine(new string('-', 60));
    }
}