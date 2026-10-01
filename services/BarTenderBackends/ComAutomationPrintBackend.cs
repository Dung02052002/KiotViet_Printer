using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Backend in qua ActiveX Automation (COM "BarTender.Application").
///
/// Lý do tồn tại: khi người dùng ĐANG MỞ BarTender (có nhiều file .btw đang
/// làm dở), 2 backend dòng lệnh (/F /P và /XMLScript=) đều chuyển lệnh cho
/// CHÍNH cửa sổ BarTender đó → BarTender đóng hết các file đang mở, chỉ để
/// lại file tem vừa in. Backend này tạo một instance BarTender RIÊNG, ẨN,
/// không có file nào khác, mở template → in → đóng → thoát. Cửa sổ BarTender
/// của người dùng không bị đụng tới (kể cả file chưa lưu).
///
/// Hỗ trợ cả Named Sub-String (tem kính) nên thay được cho /XMLScript=.
///
/// Chỉ dùng khi ProgID BarTender.Application có đăng ký và edition có
/// Automation. Nếu không tạo được instance (hoặc treo ngay từ đầu, ví dụ
/// hộp thoại giới hạn edition) → ném <see cref="ComAutomationUnavailableException"/>
/// TRƯỚC khi gửi lệnh in, để <see cref="BarTenderService"/> quay về backend
/// dòng lệnh cũ — không bao giờ in trùng.
/// </summary>
public sealed class ComAutomationPrintBackend : IBarTenderPrintBackend
{
    private const string ProgId = "BarTender.Application";

    // BtSaveOptions.btDoNotSaveChanges
    private const int DoNotSaveChanges = 1;

    // BtPrintResult.btSuccess
    private const int PrintSuccess = 0;

    // Thời gian tối đa cho giai đoạn khởi động + mở template (chưa in).
    private const int SetupTimeoutMs = 60000;

    // Thời gian tối đa cho giai đoạn in (lệnh in lớn, nhiều bản).
    private const int PrintTimeoutMs = 600000;

    // null = chưa biết, false = phiên này đã xác nhận không dùng được COM.
    private static bool? s_comUsable;

    public string Name => "ComAutomation";

    /// <summary>
    /// Máy này có dùng được COM Automation không — chỉ đọc registry, KHÔNG
    /// khởi động BarTender.
    /// </summary>
    public static bool IsAvailable(string bartenderExe)
    {
        if (s_comUsable == false)
            return false;

        if (Type.GetTypeFromProgID(ProgId, throwOnError: false) == null)
            return false;

        BarTenderCapabilityService cap = BarTenderCapabilityService.Instance;
        cap.EnsureProbed(bartenderExe);

        // Edition đọc được và không có Automation (Starter/Professional...)
        // → COM sẽ không in được, dùng dòng lệnh như cũ.
        string? edition = cap.DetectedEdition;
        if (!string.IsNullOrWhiteSpace(edition) &&
            !edition.Contains("automation", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public string DescribeCommand(BarTenderPrintRequest request)
    {
        return
            $"COM {ProgId} (instance riêng, ẩn — không đụng BarTender đang mở)\n" +
            $"  Formats.Open(\"{request.TemplatePath}\", CloseOutFirstFormat=false, Printer=\"{request.PrinterName}\")\n" +
            "  EnablePrompting=false\n" +
            $"  NamedSubStrings: {request.DescribeNamedSubStrings()}\n" +
            "  PrintOut(ShowStatusWindow=false, ShowPrintDialog=false) → Close(DoNotSave) → Quit";
    }

    public void Print(BarTenderPrintRequest request, Stopwatch printStopwatch)
    {
        BarTenderCommandLog.WriteCommandBlock(
            Name,
            ProgId,
            "(COM Automation — instance riêng)",
            request.TemplatePath,
            request.PrinterName,
            request.DescribeNamedSubStrings());

        bool hasRunningBarTender = BarTenderProcess.IsBarTenderRunning(request.BarTenderExe);
        HashSet<int> pidsBefore = SnapshotBarTenderPids(request.BarTenderExe);

        PrintJob job = new(request);

        Thread worker = new(job.Run)
        {
            IsBackground = true,
            Name = "BarTender COM print"
        };
        // BarTender ActiveX là COM STA — gọi từ thread STA riêng, không phụ
        // thuộc thread gọi (UI hay thread pool).
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();

        if (!WaitForJob(worker, job))
        {
            // Treo: chỉ kill instance ẨN do chính lệnh in này tạo ra.
            KillOwnInstances(request.BarTenderExe, pidsBefore);
            worker.Join(5000);

            if (job.Stage < JobStage.Printing)
            {
                s_comUsable = false;
                throw new ComAutomationUnavailableException(
                    $"Treo ở bước {job.Stage} (chưa gửi lệnh in).");
            }

            throw new Exception(
                "BarTender xử lý lệnh in quá thời gian chờ.\n\n" +
                $"Template: {request.TemplatePath}\n" +
                $"Printer: {request.PrinterName}\n\n" +
                "Vui lòng kiểm tra máy in / hàng đợi in.");
        }

        if (job.Error != null)
        {
            // Lỗi trước khi gửi lệnh in ⇒ chưa in gì, an toàn để thử lại bằng
            // backend dòng lệnh. Không tạo được instance ⇒ cả phiên không dùng COM.
            if (job.Stage < JobStage.Printing)
            {
                if (job.Stage == JobStage.Creating)
                    s_comUsable = false;

                throw new ComAutomationUnavailableException(
                    $"Lỗi ở bước {job.Stage}: {job.Error.Message}",
                    job.Error);
            }

            throw new Exception(
                "BarTender báo lỗi khi in.\n\n" +
                $"Template: {request.TemplatePath}\n" +
                $"Printer: {request.PrinterName}\n\n" +
                job.Error.Message,
                job.Error);
        }

        s_comUsable = true;

        if (job.SkippedSubStrings.Count > 0)
            BarTenderCommandLog.Write(
                "COM bỏ qua Named Sub-String không có trong template: " +
                string.Join(", ", job.SkippedSubStrings));

        // Instance ẩn thường tự thoát sau Quit; nếu còn sót thì dọn.
        CleanupLeftoverInstances(request.BarTenderExe, pidsBefore);

        PrintCompletion.Confirm(
            request.PrinterName,
            request.TemplatePath,
            hasRunningBarTender,
            printStopwatch,
            Name);
    }

    //---------------------------------------------------------

    private static bool WaitForJob(Thread worker, PrintJob job)
    {
        Stopwatch stageWatch = Stopwatch.StartNew();
        JobStage lastStage = job.Stage;

        while (!worker.Join(200))
        {
            JobStage stage = job.Stage;

            if (stage != lastStage)
            {
                lastStage = stage;
                stageWatch.Restart();
            }

            int limit = stage >= JobStage.Printing ? PrintTimeoutMs : SetupTimeoutMs;

            if (stageWatch.ElapsedMilliseconds > limit)
                return false;
        }

        return true;
    }

    private static string GetProcessName(string bartenderExe)
    {
        string name = Path.GetFileNameWithoutExtension(bartenderExe);
        return string.IsNullOrWhiteSpace(name) ? "bartend" : name;
    }

    private static HashSet<int> SnapshotBarTenderPids(string bartenderExe)
    {
        HashSet<int> pids = new();

        foreach (Process p in Process.GetProcessesByName(GetProcessName(bartenderExe)))
        {
            pids.Add(p.Id);
            p.Dispose();
        }

        return pids;
    }

    /// <summary>
    /// Các tiến trình BarTender MỚI xuất hiện sau khi bắt đầu lệnh in và
    /// KHÔNG có cửa sổ hiển thị — tức instance ẩn của COM. Không bao giờ trả
    /// về BarTender người dùng đang mở (có từ trước, hoặc có cửa sổ).
    /// </summary>
    private static List<Process> FindOwnInstances(string bartenderExe, HashSet<int> pidsBefore)
    {
        List<Process> result = new();

        foreach (Process p in Process.GetProcessesByName(GetProcessName(bartenderExe)))
        {
            bool own = false;

            try
            {
                own = !pidsBefore.Contains(p.Id) && p.MainWindowHandle == IntPtr.Zero;
            }
            catch
            {
                // tiến trình vừa thoát
            }

            if (own)
                result.Add(p);
            else
                p.Dispose();
        }

        return result;
    }

    private static void KillOwnInstances(string bartenderExe, HashSet<int> pidsBefore)
    {
        foreach (Process p in FindOwnInstances(bartenderExe, pidsBefore))
        {
            using (p)
            {
                try
                {
                    BarTenderCommandLog.Write($"COM kill instance ẩn bị treo pid={p.Id}");
                    p.Kill();
                }
                catch
                {
                    // best-effort
                }
            }
        }
    }

    private static void CleanupLeftoverInstances(string bartenderExe, HashSet<int> pidsBefore)
    {
        foreach (Process p in FindOwnInstances(bartenderExe, pidsBefore))
        {
            using (p)
            {
                try
                {
                    if (!p.WaitForExit(5000))
                    {
                        BarTenderCommandLog.Write($"COM instance ẩn chưa thoát sau Quit, kill pid={p.Id}");
                        p.Kill();
                    }
                }
                catch
                {
                    // best-effort
                }
            }
        }
    }

    //---------------------------------------------------------

    private enum JobStage
    {
        Creating = 0,
        Opening = 1,
        Printing = 2,
        Closing = 3
    }

    private sealed class PrintJob
    {
        private readonly BarTenderPrintRequest _request;
        private volatile int _stage;

        public PrintJob(BarTenderPrintRequest request)
        {
            _request = request;
        }

        public JobStage Stage => (JobStage)_stage;

        public Exception? Error { get; private set; }

        public List<string> SkippedSubStrings { get; } = new();

        private void SetStage(JobStage stage) => _stage = (int)stage;

        public void Run()
        {
            object? app = null;
            object? formats = null;
            object? format = null;

            try
            {
                Type type = Type.GetTypeFromProgID(ProgId, throwOnError: true)!;
                app = Activator.CreateInstance(type)
                    ?? throw new Exception($"Không tạo được {ProgId}.");

                dynamic bt = app;
                bt.Visible = false;

                SetStage(JobStage.Opening);

                formats = bt.Formats;
                format = ((dynamic)formats).Open(
                    _request.TemplatePath,
                    false,
                    _request.PrinterName);

                dynamic fmt = format!;

                // Không hiện data entry form / hộp thoại hỏi — giống
                // EnablePrompting=false của XMLScript.
                fmt.EnablePrompting = false;

                if (_request.NamedSubStrings != null)
                {
                    foreach (KeyValuePair<string, string> kv in _request.NamedSubStrings)
                    {
                        if (string.IsNullOrWhiteSpace(kv.Key))
                            continue;

                        try
                        {
                            fmt.SetNamedSubStringValue(kv.Key, kv.Value ?? string.Empty);
                        }
                        catch (COMException)
                        {
                            // Template không dùng tên này (ví dụ template cũ chỉ
                            // có GLASSES_INFO) — bỏ qua như XMLScript.
                            SkippedSubStrings.Add(kv.Key);
                        }
                    }
                }

                SetStage(JobStage.Printing);

                object? result = fmt.PrintOut(false, false);

                WaitWhileBusy(bt);

                SetStage(JobStage.Closing);

                if (result != null && Convert.ToInt32(result) != PrintSuccess)
                    throw new Exception($"BarTender trả về kết quả in = {result} (không thành công).");
            }
            catch (Exception ex)
            {
                Error = ex;
            }
            finally
            {
                try
                {
                    if (format != null)
                        ((dynamic)format).Close(DoNotSaveChanges);
                }
                catch
                {
                    // best-effort
                }

                try
                {
                    if (app != null)
                        ((dynamic)app).Quit(DoNotSaveChanges);
                }
                catch
                {
                    // best-effort — instance còn sót sẽ được dọn ở thread gọi
                }

                Release(format);
                Release(formats);
                Release(app);
            }
        }

        /// <summary>
        /// Chờ BarTender gửi xong lệnh in cho spooler trước khi Quit, để
        /// không cắt ngang job.
        /// </summary>
        private static void WaitWhileBusy(dynamic bt)
        {
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < PrintTimeoutMs)
            {
                bool busy;

                try
                {
                    busy = (bool)bt.IsPrinting;
                }
                catch
                {
                    return; // edition/phiên bản không có thuộc tính này
                }

                if (!busy)
                    return;

                Thread.Sleep(100);
            }
        }

        private static void Release(object? comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                    Marshal.FinalReleaseComObject(comObject);
            }
            catch
            {
                // best-effort
            }
        }
    }
}

/// <summary>
/// COM Automation không dùng được trên máy này — ném TRƯỚC khi gửi lệnh in,
/// nên an toàn để quay về backend dòng lệnh mà không in trùng.
/// </summary>
public sealed class ComAutomationUnavailableException : Exception
{
    public ComAutomationUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}
