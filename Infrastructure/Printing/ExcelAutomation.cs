using System.Diagnostics;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Services.Warehouse;
using Microsoft.Win32;

namespace KiotVietLabelPrinter.Infrastructure.Printing;

/// <summary>
/// Một phiên Microsoft Excel ẨN, RIÊNG của ứng dụng (không bao giờ gắn vào
/// cửa sổ Excel người dùng đang mở). Mọi đối tượng COM tạo qua phiên được ghi
/// lại để giải phóng khi kết thúc; nếu tiến trình Excel của phiên vẫn còn sau
/// khi Quit thì chỉ đúng tiến trình đó bị dừng — không để Excel chạy ngầm.
///
/// Late binding (dynamic) là cố ý: không phụ thuộc phiên bản Office cụ thể và
/// không phải kèm Interop assembly vào bộ cài.
/// </summary>
public sealed class ExcelSession : IDisposable
{
    private const string DevicesKey = @"Software\Microsoft\Windows NT\CurrentVersion\Devices";

    private readonly List<object> _owned = new();
    private readonly List<dynamic> _openWorkbooks = new();
    private bool _disposed;

    public dynamic App { get; }

    /// <summary>Tiến trình EXCEL.EXE của riêng phiên này (0 = không xác định được).</summary>
    public int ProcessId { get; }

    private ExcelSession(dynamic app, int processId)
    {
        App = app;
        ProcessId = processId;
    }

    public static bool IsExcelInstalled() => Type.GetTypeFromProgID("Excel.Application") != null;

    public static ExcelSession Start()
    {
        Type excelType = Type.GetTypeFromProgID("Excel.Application")
                         ?? throw new WarehouseException(WarehouseErrorKind.ExcelNotAvailable,
                             "Máy này chưa cài Microsoft Excel.");

        dynamic app = Activator.CreateInstance(excelType)!;
        int pid = 0;
        try
        {
            GetWindowThreadProcessId(new IntPtr((int)app.Hwnd), out uint id);
            pid = (int)id;
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"Không đọc được tiến trình Excel: {ex.Message}");
        }

        ExcelSession session = new(app, pid);
        try
        {
            app.Visible = false;
            app.DisplayAlerts = false;
            app.ScreenUpdating = false;
            app.Interactive = false;
            // Không chạy macro/add-in tự động của file phiếu.
            app.AutomationSecurity = 3; // msoAutomationSecurityForceDisable
        }
        catch (COMException)
        {
            session.Dispose();
            throw;
        }

        return session;
    }

    /// <summary>Ghi nhận một đối tượng COM để giải phóng khi kết thúc phiên.</summary>
    public dynamic Track(object comObject)
    {
        _owned.Add(comObject);
        return comObject;
    }

    /// <summary>Mở workbook CHỈ ĐỌC, không cập nhật liên kết — phiên này không bao giờ lưu file.</summary>
    public dynamic OpenReadOnly(string path)
    {
        dynamic workbooks = Track(App.Workbooks);
        dynamic workbook = Track(workbooks.Open(path, 0, true));
        _openWorkbooks.Add(workbook);
        return workbook;
    }

    public dynamic Worksheet(dynamic workbook, string sheetName)
    {
        dynamic sheets = Track(workbook.Worksheets);
        return Track(sheets.Item(sheetName));
    }

    /// <summary>
    /// Chọn máy in cho RIÊNG phiên Excel này (phân trang và in theo máy in
    /// đó). Đã kiểm tra: không đổi máy in mặc định của Windows. Excel chỉ
    /// nhận đổi máy in khi đã có workbook mở.
    /// </summary>
    public string SetActivePrinter(string printerName)
    {
        string excelName = ResolveExcelPrinterName(printerName);
        try
        {
            App.ActivePrinter = excelName;
        }
        catch (COMException ex)
        {
            throw new WarehouseException(WarehouseErrorKind.PrinterNotFound,
                $"Microsoft Excel không chọn được máy in \"{printerName}\".\n\n" +
                "Hãy bấm Làm mới danh sách máy in, chọn lại, hoặc kiểm tra máy in trong Windows.", ex);
        }

        return (string)App.ActivePrinter;
    }

    /// <summary>
    /// Excel đòi tên máy in kèm cổng ("HP LaserJet on Ne02:") ở ActivePrinter.
    /// Cổng lấy từ khoá Devices của người dùng hiện tại ("winspool,Ne02:").
    /// </summary>
    public static string ResolveExcelPrinterName(string printerName)
    {
        using RegistryKey? devices = Registry.CurrentUser.OpenSubKey(DevicesKey);
        if (devices?.GetValue(printerName) is string value)
        {
            string? port = value.Split(',').Skip(1).FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(port))
                return $"{printerName} on {port}";
        }

        return printerName;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach (dynamic workbook in _openWorkbooks)
            Try(() => workbook.Close(false), "đóng workbook");
        Try(() => App.Quit(), "thoát Excel");

        for (int i = _owned.Count - 1; i >= 0; i--)
            Release(_owned[i]);
        Release(App);
        _owned.Clear();
        _openWorkbooks.Clear();

        // Các đối tượng COM trung gian (vd ws.PageSetup) do dynamic tạo ra chỉ
        // được giải phóng khi GC chạy — chạy ngay để Excel thoát.
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        GC.WaitForPendingFinalizers();

        EnsureProcessExited(ProcessId, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Chờ tiến trình Excel CỦA PHIÊN thoát; quá hạn thì dừng đúng tiến trình
    /// đó (kiểm tra tên tiến trình để không bao giờ dừng nhầm chương trình khác).
    /// </summary>
    internal static void EnsureProcessExited(int processId, TimeSpan wait)
    {
        if (processId <= 0)
            return;

        try
        {
            using Process process = Process.GetProcessById(processId);
            if (!process.ProcessName.Equals("EXCEL", StringComparison.OrdinalIgnoreCase))
                return;
            if (process.WaitForExit((int)wait.TotalMilliseconds))
                return;

            process.Kill();
            process.WaitForExit(5000);
        }
        catch (ArgumentException)
        {
            // Tiến trình đã thoát.
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"Không dừng được tiến trình Excel {processId}: {ex.Message}");
        }
    }

    private static void Release(object comObject)
    {
        try
        {
            if (Marshal.IsComObject(comObject))
                Marshal.FinalReleaseComObject(comObject);
        }
        catch (ArgumentException ex)
        {
            Debug.WriteLine($"Giải phóng COM lỗi: {ex.Message}");
        }
    }

    private static void Try(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (ex is COMException or InvalidComObjectException)
        {
            // Excel đã đóng / bị dừng — không còn gì để đóng.
            Debug.WriteLine($"Excel: không {what} được: {ex.Message}");
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}

/// <summary>
/// Chạy một thao tác Excel trên luồng STA riêng (COM của Excel cần STA), có
/// giới hạn thời gian: Excel treo (hộp thoại ẩn, máy in lỗi...) thì tiến
/// trình Excel của phiên bị dừng và báo lỗi — giao diện không bao giờ bị treo.
/// </summary>
public static class ExcelAutomation
{
    // Tiến trình Excel của các phiên đang chạy. Luồng STA là luồng nền nên khi
    // app thoát giữa chừng (người dùng xác nhận thoát lúc đang xem trước / in)
    // nó bị cắt ngang trước khi kịp Quit — dừng các tiến trình này lúc thoát để
    // không để EXCEL.EXE chạy ngầm.
    private static readonly HashSet<int> ActiveProcessIds = new();
    private static int _exitHookInstalled;

    public static Task<T> RunAsync<T>(Func<ExcelSession, T> work, TimeSpan timeout, string operation,
        WarehouseErrorKind failureKind)
    {
        if (!ExcelSession.IsExcelInstalled())
            throw new WarehouseException(WarehouseErrorKind.ExcelNotAvailable,
                $"Máy này chưa cài Microsoft Excel nên không {operation} được.");

        if (Interlocked.Exchange(ref _exitHookInstalled, 1) == 0)
            AppDomain.CurrentDomain.ProcessExit += (_, _) => StopActiveSessions();

        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int sessionPid = 0;

        Thread thread = new(() =>
        {
            int registeredPid = 0;
            try
            {
                T result;
                using (ExcelSession session = ExcelSession.Start())
                {
                    Volatile.Write(ref sessionPid, session.ProcessId);
                    registeredPid = session.ProcessId;
                    Register(registeredPid);
                    result = work(session);
                }

                // Chỉ báo xong khi phiên Excel đã đóng hẳn.
                completion.TrySetResult(result);
            }
            catch (WarehouseException ex)
            {
                completion.TrySetException(ex);
            }
            catch (Exception ex) when (ex is COMException or InvalidComObjectException or
                                           Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException)
            {
                completion.TrySetException(new WarehouseException(failureKind,
                    $"Microsoft Excel báo lỗi khi {operation}.\n\nHãy thử lại; nếu vẫn lỗi, mở file phiếu bằng Excel để kiểm tra.", ex));
            }
            catch (Exception ex)
            {
                // Lỗi khác (IO, đối số...) không được lọt ra khỏi luồng riêng này:
                // exception chưa bắt ở luồng nền làm sập cả ứng dụng, còn người gọi
                // thì chờ tới hết thời gian chờ mà không biết lỗi gì.
                completion.TrySetException(new WarehouseException(failureKind,
                    $"Không {operation} được: {ex.Message}\n\nHãy thử lại; nếu vẫn lỗi, mở file phiếu bằng Excel để kiểm tra.", ex));
            }
            finally
            {
                Unregister(registeredPid);
            }
        })
        {
            IsBackground = true,
            Name = "WarehouseExcel"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        return WithTimeout(completion.Task, timeout, operation, () => Volatile.Read(ref sessionPid));
    }

    private static void Register(int processId)
    {
        if (processId <= 0)
            return;
        lock (ActiveProcessIds)
            ActiveProcessIds.Add(processId);
    }

    private static void Unregister(int processId)
    {
        if (processId <= 0)
            return;
        lock (ActiveProcessIds)
            ActiveProcessIds.Remove(processId);
    }

    private static void StopActiveSessions()
    {
        int[] pids;
        lock (ActiveProcessIds)
            pids = ActiveProcessIds.ToArray();

        // Chỉ các tiến trình Excel riêng do app tạo; EnsureProcessExited còn kiểm tra
        // tên tiến trình nên không bao giờ dừng nhầm Excel người dùng tự mở.
        foreach (int pid in pids)
            ExcelSession.EnsureProcessExited(pid, TimeSpan.Zero);
    }

    private static async Task<T> WithTimeout<T>(Task<T> task, TimeSpan timeout, string operation, Func<int> pid)
    {
        if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
            return await task;

        // Excel không phản hồi: dừng tiến trình của phiên (luồng STA sẽ tự kết
        // thúc vì lời gọi COM đang chờ trả lỗi). Lỗi đó tới SAU khi đã báo hết
        // giờ và không còn ai chờ — quan sát nó để không bị ghi thành lỗi
        // "TASK chưa xử lý" trong log app-errors.
        _ = task.ContinueWith(t => _ = t.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        ExcelSession.EnsureProcessExited(pid(), TimeSpan.Zero);
        throw new WarehouseException(WarehouseErrorKind.ExcelTimeout,
            $"Microsoft Excel không phản hồi sau {timeout.TotalSeconds:0} giây khi {operation} — đã dừng phiên Excel này.\n\n" +
            "Hãy kiểm tra máy in / file phiếu rồi thử lại.");
    }
}
