using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Chạy tiến trình BarTender + chờ hàng đợi máy in xử lý xong — dùng chung
/// cho cả 2 backend (StandardCommandLine và XmlScript). Tách khỏi
/// BarTenderService để logic "gọi tiến trình / chờ spooler" chỉ có một chỗ.
/// </summary>
internal static class BarTenderProcess
{
    public readonly record struct RunResult(
        bool Exited,
        int ExitCode,
        string StdOut,
        string StdErr);

    /// <summary>
    /// Start tiến trình BarTender với psi đã dựng sẵn, chờ thoát (có kill khi
    /// timeout để không treo vô thời hạn vì hộp thoại chờ xác nhận).
    /// </summary>
    /// <param name="primaryTimeoutMs">
    /// Thời gian chờ chính. Khi đang DÒ edition lần đầu (chưa biết máy có
    /// Enterprise Automation không), truyền giá trị ngắn (~12s) để nếu dialog
    /// #3112 bật lên thì bị kill nhanh thay vì hiện 30 giây.
    /// </param>
    public static RunResult Run(
        ProcessStartInfo psi,
        bool hasRunningBarTender,
        int primaryTimeoutMs = 30000)
    {
        using Process? process = Process.Start(psi);

        if (process == null)
            throw new Exception("Không thể gửi lệnh in tới BarTender.");

        bool exited = process.WaitForExit(primaryTimeoutMs);

        // Khi BarTender đã mở sẵn, tiến trình handoff có thể thoát chậm.
        if (!exited && hasRunningBarTender)
            exited = process.WaitForExit(90000);

        if (!exited)
        {
            // Dọn tiến trình/hộp thoại bị kẹt (ví dụ dialog #3112 chờ bấm OK).
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // best-effort
            }

            return new RunResult(false, -1, string.Empty, string.Empty);
        }

        string stdOut = process.StandardOutput.ReadToEnd();
        string stdErr = process.StandardError.ReadToEnd();

        return new RunResult(true, process.ExitCode, stdOut, stdErr);
    }

    public static ProcessStartInfo BuildPsi(string bartenderExe)
    {
        return new ProcessStartInfo
        {
            FileName = bartenderExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }

    public static bool IsBarTenderRunning(string bartenderExe)
    {
        string processName = Path.GetFileNameWithoutExtension(bartenderExe);
        return Process.GetProcessesByName(processName).Length > 0;
    }

    //---------------------------------------------------------
    // Chờ máy in xử lý xong job (hàng đợi Windows Spooler). Giá trị trả về
    // chỉ để ghi log chẩn đoán — không đổi luồng in.
    //---------------------------------------------------------

    public static string WaitForPrintJobToFinish(
        string printerName,
        int startupGraceMs = 8000,
        int maxWaitMs = 600000,
        int pollIntervalMs = 200)
    {
        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            return "no-printer-handle";

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            bool sawJob = false;

            while (sw.ElapsedMilliseconds < startupGraceMs)
            {
                if (GetQueuedJobCount(hPrinter) > 0)
                {
                    sawJob = true;
                    break;
                }

                Thread.Sleep(pollIntervalMs);
            }

            if (!sawJob)
                return "no-job-seen";

            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                if (GetQueuedJobCount(hPrinter) == 0)
                    return "confirmed-empty";

                Thread.Sleep(pollIntervalMs);
            }

            return "timeout-still-queued";
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    private static int GetQueuedJobCount(IntPtr hPrinter)
    {
        GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out uint neededBytes);

        if (neededBytes == 0)
            return 0;

        IntPtr buffer = Marshal.AllocHGlobal((int)neededBytes);

        try
        {
            if (!GetPrinter(hPrinter, 2, buffer, neededBytes, out _))
                return 0;

            PRINTER_INFO_2 info = Marshal.PtrToStructure<PRINTER_INFO_2>(buffer);

            return (int)info.cJobs;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_INFO_2
    {
        public string? pServerName;
        public string? pPrinterName;
        public string? pShareName;
        public string? pPortName;
        public string? pDriverName;
        public string? pComment;
        public string? pLocation;
        public IntPtr pDevMode;
        public string? pSepFile;
        public string? pPrintProcessor;
        public string? pDatatype;
        public string? pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetPrinter(IntPtr hPrinter, uint dwLevel, IntPtr pPrinter, uint cbBuf, out uint pcbNeeded);
}
