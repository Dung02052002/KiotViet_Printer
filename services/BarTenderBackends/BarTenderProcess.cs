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
    // Chờ máy in xử lý xong job (hàng đợi Windows Spooler).
    //
    // CHỈ theo dõi job do chính lệnh in này tạo ra (job mới xuất hiện so với
    // ảnh chụp hàng đợi TRƯỚC khi in). Trước đây hàm đếm tổng số job của cả
    // máy in: còn 1 job cũ bị kẹt / job của phần mềm khác / máy in tạm dừng
    // là mỗi lệnh in phải chờ đủ 10 phút, app kẹt ở "Đang in...".
    //---------------------------------------------------------

    public readonly record struct JobWaitResult(string Status, string? Warning);

    // Job / máy in ở trạng thái này thì sẽ không tự in xong nếu không có
    // người xử lý (hết giấy, offline, tạm dừng, lỗi...).
    private const uint JobStuckMask =
        0x1      // JOB_STATUS_PAUSED
        | 0x2    // JOB_STATUS_ERROR
        | 0x20   // JOB_STATUS_OFFLINE
        | 0x40   // JOB_STATUS_PAPEROUT
        | 0x200  // JOB_STATUS_BLOCKED_DEVQ
        | 0x400; // JOB_STATUS_USER_INTERVENTION

    // Job đã in xong nhưng vẫn nằm trong hàng đợi (máy in bật "Keep printed
    // documents") — coi như đã xong.
    private const uint JobDoneMask =
        0x80      // JOB_STATUS_PRINTED
        | 0x100   // JOB_STATUS_DELETED
        | 0x1000; // JOB_STATUS_COMPLETE

    private const uint PrinterStuckMask =
        0x1         // PRINTER_STATUS_PAUSED
        | 0x2       // PRINTER_STATUS_ERROR
        | 0x8       // PRINTER_STATUS_PAPER_JAM
        | 0x10      // PRINTER_STATUS_PAPER_OUT
        | 0x80      // PRINTER_STATUS_OFFLINE
        | 0x1000    // PRINTER_STATUS_NOT_AVAILABLE
        | 0x100000  // PRINTER_STATUS_USER_INTERVENTION
        | 0x400000; // PRINTER_STATUS_DOOR_OPEN

    /// <summary>
    /// Ảnh chụp các job ĐANG có trong hàng đợi — gọi ngay TRƯỚC khi gửi lệnh
    /// in. null nếu không mở được máy in (khi đó bỏ qua bước chờ).
    /// </summary>
    public static IReadOnlySet<uint>? SnapshotJobIds(string printerName)
    {
        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            return null;

        try
        {
            List<JobInfo>? jobs = GetJobs(hPrinter);
            return jobs?.Select(j => j.Id).ToHashSet();
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    public static JobWaitResult WaitForPrintJobToFinish(
        string printerName,
        IReadOnlySet<uint>? preexistingJobIds,
        string documentHint,
        int startupGraceMs = 8000,
        int maxWaitMs = 600000,
        int stuckGiveUpMs = 5000,
        int pollIntervalMs = 200)
    {
        if (preexistingJobIds == null)
            return new JobWaitResult("no-snapshot", null);

        if (!OpenPrinter(printerName, out IntPtr hPrinter, IntPtr.Zero))
            return new JobWaitResult("no-printer-handle", null);

        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            HashSet<uint> ourJobs = new();

            // 1) Chờ job của lệnh in này xuất hiện.
            while (sw.ElapsedMilliseconds < startupGraceMs)
            {
                List<JobInfo>? jobs = GetJobs(hPrinter);
                if (jobs == null)
                    return new JobWaitResult("enum-failed", null);

                List<JobInfo> fresh = jobs.Where(j => !preexistingJobIds.Contains(j.Id)).ToList();

                if (fresh.Count > 0)
                {
                    // Ưu tiên job mang tên template (BarTender đặt tên job theo
                    // tên file .btw) để không chờ nhầm job của phần mềm khác
                    // vô tình in cùng lúc.
                    List<JobInfo> named = string.IsNullOrWhiteSpace(documentHint)
                        ? new List<JobInfo>()
                        : fresh.Where(j => j.Document.Contains(documentHint, StringComparison.OrdinalIgnoreCase)).ToList();

                    foreach (JobInfo j in named.Count > 0 ? named : fresh)
                        ourJobs.Add(j.Id);

                    break;
                }

                Thread.Sleep(pollIntervalMs);
            }

            // Không thấy job mới: hoặc đã in xong trước khi kịp thấy (job nhỏ,
            // máy in nhanh), hoặc BarTender không tạo job.
            if (ourJobs.Count == 0)
                return new JobWaitResult("no-job-seen", null);

            // 2) Chờ đúng các job đó rời hàng đợi.
            Stopwatch stuckWatch = new();

            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                List<JobInfo>? jobs = GetJobs(hPrinter);
                if (jobs == null)
                    return new JobWaitResult("enum-failed", null);

                List<JobInfo> pending = jobs
                    .Where(j => ourJobs.Contains(j.Id) && (j.Status & JobDoneMask) == 0)
                    .ToList();

                if (pending.Count == 0)
                    return new JobWaitResult("confirmed-done", null);

                uint printerStatus = GetPrinterStatus(hPrinter);
                bool stuck =
                    pending.Any(j => (j.Status & JobStuckMask) != 0) ||
                    (printerStatus & PrinterStuckMask) != 0;

                if (stuck)
                {
                    if (!stuckWatch.IsRunning)
                        stuckWatch.Start();

                    // Kẹt liên tục vài giây (không phải chớp nhoáng) ⇒ thôi chờ:
                    // cần người xử lý máy in, chờ tiếp chỉ làm app treo.
                    if (stuckWatch.ElapsedMilliseconds >= stuckGiveUpMs)
                    {
                        return new JobWaitResult(
                            $"stuck jobStatus=0x{pending[0].Status:X} printerStatus=0x{printerStatus:X}",
                            $"Lệnh in đã được gửi tới máy in \"{printerName}\" nhưng máy in đang " +
                            "tạm dừng / offline / hết giấy / báo lỗi nên tem chưa in ra.\n\n" +
                            "Hãy kiểm tra máy in — tem sẽ tự in tiếp khi máy in sẵn sàng.\n" +
                            "KHÔNG cần bấm In lại (sẽ bị in trùng).");
                    }
                }
                else
                {
                    stuckWatch.Reset();
                }

                Thread.Sleep(pollIntervalMs);
            }

            return new JobWaitResult(
                "timeout-still-queued",
                $"Máy in \"{printerName}\" vẫn đang xử lý lệnh in sau {maxWaitMs / 60000} phút.\n" +
                "Hãy kiểm tra hàng đợi máy in. KHÔNG cần bấm In lại (sẽ bị in trùng).");
        }
        finally
        {
            ClosePrinter(hPrinter);
        }
    }

    private readonly record struct JobInfo(uint Id, uint Status, string Document);

    private const int ErrorInsufficientBuffer = 122;

    private static List<JobInfo>? GetJobs(IntPtr hPrinter)
    {
        // Hỏi kích thước rồi mới đọc: nếu có job mới vào hàng đợi đúng giữa 2
        // lần gọi thì lần đọc báo thiếu buffer — trước đây trả null ngay và
        // việc chờ job bị bỏ dở ("enum-failed"). Thử lại vài lần.
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            EnumJobs(hPrinter, 0, 1000, 1, IntPtr.Zero, 0, out uint needed, out _);

            if (needed == 0)
                return new List<JobInfo>(); // hàng đợi rỗng

            IntPtr buffer = Marshal.AllocHGlobal((int)needed);

            try
            {
                if (!EnumJobs(hPrinter, 0, 1000, 1, buffer, needed, out _, out uint returned))
                {
                    if (Marshal.GetLastWin32Error() == ErrorInsufficientBuffer)
                        continue;

                    return null;
                }

                int size = Marshal.SizeOf<JOB_INFO_1>();
                List<JobInfo> jobs = new((int)returned);

                for (int i = 0; i < returned; i++)
                {
                    JOB_INFO_1 info = Marshal.PtrToStructure<JOB_INFO_1>(buffer + i * size);
                    jobs.Add(new JobInfo(info.JobId, info.Status, info.pDocument ?? string.Empty));
                }

                return jobs;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        return null;
    }

    private static uint GetPrinterStatus(IntPtr hPrinter)
    {
        GetPrinter(hPrinter, 2, IntPtr.Zero, 0, out uint neededBytes);

        if (neededBytes == 0)
            return 0;

        IntPtr buffer = Marshal.AllocHGlobal((int)neededBytes);

        try
        {
            if (!GetPrinter(hPrinter, 2, buffer, neededBytes, out _))
                return 0;

            return Marshal.PtrToStructure<PRINTER_INFO_2>(buffer).Status;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct JOB_INFO_1
    {
        public uint JobId;
        public string? pPrinterName;
        public string? pMachineName;
        public string? pUserName;
        public string? pDocument;
        public string? pDatatype;
        public string? pStatus;
        public uint Status;
        public uint Priority;
        public uint Position;
        public uint TotalPages;
        public uint PagesPrinted;
        public SYSTEMTIME Submitted;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool EnumJobs(IntPtr hPrinter, uint firstJob, uint noJobs, uint level, IntPtr pJob, uint cbBuf, out uint pcbNeeded, out uint pcReturned);

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
