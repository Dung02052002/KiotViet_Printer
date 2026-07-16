namespace KiotVietLabelPrinter.Services.Glasses.Debug;

public static class GlassesDebug
{
#if DEBUG

    public static bool Enabled = true;

#else

    public static bool Enabled = false;

#endif

    public static void Log(string message)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine(message);
    }

    public static void Title(string title)
    {
        if (!Enabled)
            return;

        System.Diagnostics.Debug.WriteLine("");
        System.Diagnostics.Debug.WriteLine("======================================");
        System.Diagnostics.Debug.WriteLine(title);
        System.Diagnostics.Debug.WriteLine("======================================");
    }
}