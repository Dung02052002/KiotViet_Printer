using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Services.Warehouse;

namespace KiotVietLabelPrinter.Infrastructure.Printing;

/// <summary>
/// Máy in của chức năng Phiếu xuất kho: liệt kê, kiểm tra, và in file phiếu
/// .xlsx/.xls ra ĐÚNG máy in được chọn.
///
/// In bằng chính Microsoft Excel (qua COM) để bản in giống hệt khi mở file
/// bằng Excel — khổ giấy, lề, vùng in, tự giãn dòng của file mẫu đều được giữ.
/// Mỗi lần in dùng một phiên Excel ẨN RIÊNG (<see cref="ExcelSession"/>, không
/// đụng tới cửa sổ Excel người dùng đang mở) và truyền máy in qua tham số
/// ActivePrinter của PrintOut: máy in mặc định của Windows và cấu hình máy in
/// tem (BarTender) không bị thay đổi.
/// </summary>
public class WarehousePrinterService
{
    private static readonly TimeSpan PrintTimeout = TimeSpan.FromMinutes(3);

    public List<string> GetInstalledPrinters()
    {
        List<string> printers = new();
        foreach (string name in PrinterSettings.InstalledPrinters)
            printers.Add(name);
        return printers;
    }

    /// <summary>Tên máy in mặc định của Windows (chỉ đọc — không bao giờ đặt lại).</summary>
    public string GetWindowsDefaultPrinter() => new PrinterSettings().PrinterName;

    public bool IsPrinterAvailable(string? printerName) => CheckPrinter(printerName) == PrinterCheckResult.Ready;

    /// <summary>
    /// Kiểm tra máy in, phân biệt "đã bị gỡ khỏi Windows" với "còn đó nhưng
    /// Windows không mở được". Gọi spooler đồng bộ — UI phải gọi qua
    /// <see cref="SpoolerQuery"/> (spooler treo thì hết giờ, không treo app).
    /// </summary>
    public PrinterCheckResult CheckPrinter(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return PrinterCheckResult.NotSelected;

        if (!GetInstalledPrinters().Contains(printerName, StringComparer.OrdinalIgnoreCase))
            return PrinterCheckResult.NotInstalled;

        return new PrinterSettings { PrinterName = printerName }.IsValid
            ? PrinterCheckResult.Ready
            : PrinterCheckResult.Invalid;
    }

    public static bool IsExcelAvailable() => ExcelSession.IsExcelInstalled();

    /// <summary>
    /// In file phiếu ra máy in đã chọn. <paramref name="sheetName"/> = chỉ in
    /// sheet phiếu (null = cả workbook). <paramref name="printToFile"/> chỉ
    /// dùng cho máy in ảo (vd "Microsoft Print to PDF") để in ra file mà
    /// không hiện hộp thoại lưu — phục vụ kiểm tra.
    /// </summary>
    public async Task PrintWorkbookAsync(string workbookPath, string printerName, short copies = 1,
        string? printToFile = null, string? sheetName = null)
    {
        if (!File.Exists(workbookPath))
            throw new WarehouseException(WarehouseErrorKind.FileNotFound,
                $"Không tìm thấy file phiếu để in:\n{workbookPath}\n\nHãy xuất phiếu Excel trước khi in.");

        // Hỏi spooler ở luồng nền (có giới hạn thời gian): máy in lỗi làm
        // spooler treo thì báo lỗi, không làm đơ giao diện.
        bool printerAvailable;
        try
        {
            printerAvailable = await SpoolerQuery.RunAsync(() => IsPrinterAvailable(printerName));
        }
        catch (TimeoutException)
        {
            throw new WarehouseException(WarehouseErrorKind.PrinterNotFound,
                $"Windows không phản hồi khi kiểm tra máy in \"{printerName}\" (máy in có thể đang lỗi).\n\n" +
                "Bật lại máy in rồi bấm Kiểm tra kết nối; vẫn lỗi thì khởi động lại máy tính.");
        }

        if (!printerAvailable)
            throw new WarehouseException(WarehouseErrorKind.PrinterNotFound,
                $"Máy in \"{printerName}\" không còn tồn tại hoặc không dùng được.\n\nHãy bấm Kiểm tra kết nối hoặc Đổi máy in.");

        if (!IsExcelAvailable())
            throw new WarehouseException(WarehouseErrorKind.ExcelNotAvailable,
                "Máy này chưa cài Microsoft Excel nên không in trực tiếp được.\n\n" +
                "File phiếu đã được xuất — hãy mở file bằng phần mềm bảng tính khác để in.");

        string activePrinter = ExcelSession.ResolveExcelPrinterName(printerName);
        await ExcelAutomation.RunAsync(session =>
        {
            PrintWithExcel(session, workbookPath, activePrinter, copies, printToFile, sheetName);
            return true;
        }, PrintTimeout, $"in phiếu ra máy in \"{printerName}\"", WarehouseErrorKind.PrintFailed);
    }

    /// <summary>Giữ cho mã cũ: tên máy in theo dạng Excel cần ("Tên on Ne02:").</summary>
    public static string ResolveExcelPrinterName(string printerName) => ExcelSession.ResolveExcelPrinterName(printerName);

    private static void PrintWithExcel(ExcelSession session, string workbookPath, string activePrinter, short copies,
        string? printToFile, string? sheetName)
    {
        dynamic workbook = session.OpenReadOnly(workbookPath);
        dynamic target = sheetName == null ? workbook : session.Worksheet(workbook, sheetName);

        object missing = Type.Missing;
        bool toFile = !string.IsNullOrEmpty(printToFile);
        try
        {
            target.PrintOut(missing, missing, copies, false, activePrinter, toFile, missing,
                toFile ? printToFile : missing);
        }
        catch (COMException ex)
        {
            throw new WarehouseException(WarehouseErrorKind.PrintFailed,
                $"Excel không gửi được lệnh in tới máy in \"{activePrinter}\".\n\n" +
                "Hãy kiểm tra máy in đang bật, còn giấy, và thử in lại. Nếu vẫn lỗi, mở file phiếu bằng Excel để in tay.", ex);
        }
    }
}

/// <summary>Kết quả kiểm tra máy in (xem <see cref="WarehousePrinterService.CheckPrinter"/>).</summary>
public enum PrinterCheckResult
{
    Ready,
    NotSelected,
    /// <summary>Không còn trong danh sách máy in của Windows (đã gỡ / đổi tên).</summary>
    NotInstalled,
    /// <summary>Còn trong danh sách nhưng Windows không mở được (driver / kết nối lỗi).</summary>
    Invalid
}
