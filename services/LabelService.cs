using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.BarTenderBackends;

namespace KiotVietLabelPrinter.Services;

public class LabelService
{
    private readonly ExcelService _excelService = new();
    private readonly HistoryService _historyService = new();
    private readonly LabelHandlerFactory _handlerFactory = new();
    private readonly LabelCatalogService _catalogService = new();

    public List<ProductRow> ReadProducts(string sourceExcelFile)
    {
        if (string.IsNullOrWhiteSpace(sourceExcelFile))
            throw new Exception("Vui lòng chọn file Excel KiotViet.");

        if (!File.Exists(sourceExcelFile))
            throw new Exception($"Không tìm thấy file Excel KiotViet:\n{sourceExcelFile}");

        List<ProductRow> products = _excelService.ReadProducts(sourceExcelFile);

        if (products.Count == 0)
            throw new Exception("Không có dữ liệu sản phẩm trong file Excel.");

        return products;
    }

    public List<PreviewRow> BuildPreview(
        string sourceExcelFile,
        string labelCode,
        string employeeCode)
    {
        List<ProductRow> products = ReadProducts(sourceExcelFile);

        LabelDefinition label = _catalogService.GetByCode(labelCode);

        // GÁN FILE NGUỒN VÀO LABEL
        label.SourceExcelFile = sourceExcelFile;

        var handler = _handlerFactory.GetHandler(label.HandlerType);

        return handler.BuildPreview(products, label, employeeCode);
    }

    // Chỉ cho MỘT lệnh in chạy tại một thời điểm trong toàn app (màn hình
    // chính + Xem trước). Khoá bao cả bước ghi file data lẫn bước gọi
    // BarTender: nếu 2 lệnh chạy chồng nhau, lệnh sau ghi đè file data trong
    // lúc BarTender của lệnh trước đang đọc → tem in lẫn dữ liệu.
    private static readonly SemaphoreSlim PrintLock = new(1, 1);

    /// <summary>
    /// Có lệnh in đang chạy (đang giữ PrintLock) — dùng để cảnh báo khi
    /// người dùng đóng app/cửa sổ giữa lúc in.
    /// </summary>
    public static bool IsPrinting => PrintLock.CurrentCount == 0;

    /// <summary>
    /// Cảnh báo của lần Print gần nhất khi in THÀNH CÔNG nhưng có việc phụ
    /// không làm được (ví dụ không ghi được lịch sử). null = không có.
    /// </summary>
    public string? LastWarning { get; private set; }

    public int Print(
        string sourceExcelFile,
        string labelCode,
        string employeeCode,
        PriceOverride? priceOverride = null,
        Dictionary<string, string>? nameOverrides = null)
    {
        LastWarning = null;

        // Không xếp hàng chờ: người dùng bấm In lần 2 trong lúc lần 1 chưa
        // xong thường là bấm nhầm/bấm lặp — báo rõ thay vì âm thầm in thêm.
        if (!PrintLock.Wait(0))
            throw new Exception(
                "Đang có một lệnh in khác chưa xong.\n\n" +
                "Vui lòng chờ lệnh in đó hoàn tất rồi bấm In lại.");

        List<ProductRow> products;
        LabelDefinition label;
        List<string> warnings = new();
        PartialPrintException? partial = null;

        try
        {
            PrintWarnings.Reset();

            products = ReadProducts(sourceExcelFile);

            label = _catalogService.GetByCode(labelCode);

            // GÁN FILE NGUỒN VÀO LABEL
            label.SourceExcelFile = sourceExcelFile;

            var handler = _handlerFactory.GetHandler(label.HandlerType);

            try
            {
                handler.PrepareDataAndPrint(products, label, employeeCode, priceOverride, nameOverrides);
            }
            catch (PartialPrintException ex)
            {
                // In được một phần: vẫn phải ghi lịch sử cho các mã đã in
                // (bên dưới), sau đó mới báo lỗi "in thiếu" cho người dùng.
                partial = ex;
            }

            // Cảnh báo không làm lệnh in thất bại (VD máy in đang tạm dừng /
            // offline — job vẫn nằm trong hàng đợi, sẽ tự in khi máy in sẵn sàng).
            warnings.AddRange(PrintWarnings.TakeAll());
        }
        finally
        {
            PrintWarnings.Reset();
            PrintLock.Release();
        }

        // Chỉ ghi lịch sử cho những mã thật sự đã in.
        List<ProductRow> printed = partial != null
            ? partial.PrintedProducts.ToList()
            : products;

        // Tem ĐÃ in xong — lỗi ghi lịch sử không được biến lệnh in thành
        // "thất bại" (người dùng sẽ in lại → tem bị in trùng). Chỉ cảnh báo.
        try
        {
            _historyService.Add(new PrintHistory
            {
                PrintTime = DateTime.Now,
                SourceExcelFile = sourceExcelFile,
                LabelCode = label.Code,
                LabelName = label.Name,
                EmployeeCode = employeeCode,
                ProductCount = printed.Count,
                TotalLabels = printed.Sum(x => x.Quantity),
                MachineName = Environment.MachineName,
                UserName = Environment.UserName
            });
        }
        catch (Exception ex)
        {
            PrintDiagnosticsLog.Write($"HISTORY write failed label={label.Code} error={ex.Message}");

            warnings.Add(
                (partial != null
                    ? "Không ghi được lịch sử cho các mã ĐÃ in ở trên (KHÔNG cần in lại các mã đó).\n\n"
                    : "Tem đã được gửi in xong, nhưng không ghi được lịch sử in.\n" +
                      "KHÔNG cần in lại.\n\n") +
                $"Chi tiết: {ex.Message}");
        }

        // Lệnh in vẫn là "lỗi" để người dùng biết phải in bù các mã thiếu —
        // nhưng lịch sử của phần đã in đã được ghi ở trên.
        if (partial != null)
        {
            string separator = "\n\n────────────\n\n";
            string message = warnings.Count > 0
                ? partial.Message + separator + string.Join(separator, warnings)
                : partial.Message;

            throw new Exception(message, partial);
        }

        if (warnings.Count > 0)
            LastWarning = string.Join("\n\n────────────\n\n", warnings);

        return products.Count;
    }
}