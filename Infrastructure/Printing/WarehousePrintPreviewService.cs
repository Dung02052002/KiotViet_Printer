using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Fonts;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace KiotVietLabelPrinter.Infrastructure.Printing;

/// <summary>
/// Dựng bản xem trước in của một file phiếu đã xuất, bằng CHÍNH Microsoft
/// Excel — cùng bộ máy phân trang với lệnh in:
/// 1. Mở file phiếu chỉ đọc trong phiên Excel ẩn riêng, chọn máy in sẽ in
///    (để khổ giấy / vùng in được của máy đó quyết định ngắt trang).
/// 2. Lấy số trang thực tế (PageSetup.Pages) và xuất đúng sheet phiếu ra PDF
///    — mỗi trang PDF là một trang Excel sẽ in.
/// 3. Đọc vị trí ngắt trang để biết dòng nào nằm trên trang nào, rồi kiểm
///    tra bố cục bằng Excel (tự giãn dòng / cột trên bản trong bộ nhớ, không
///    lưu): tên hàng bị cắt, số hiện "###", tiêu đề bảng bị tách trang,
///    trang cuối trống...
/// File phiếu không bao giờ bị ghi; mọi thay đổi để đo đạc bị huỷ khi đóng.
/// </summary>
public class WarehousePrintPreviewService
{
    private static readonly TimeSpan PreviewTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Đo bố cục tối đa bấy nhiêu dòng hàng (sheet nháp cần mỗi tên hàng một cột, giới hạn số lời gọi COM).</summary>
    private const int MaxRowsMeasured = 1000;

    /// <summary>Cỡ chữ in thực tế nhỏ hơn mức này thì cảnh báo khó đọc.</summary>
    public const double MinReadablePrintedFontPt = 7;

    private const int MaxListedRows = 8;

    private readonly InstalledFontService _fontService;

    public WarehousePrintPreviewService(InstalledFontService? fontService = null)
    {
        _fontService = fontService ?? new InstalledFontService();
    }

    public async Task<WarehousePrintPreview> BuildAsync(WarehousePreviewRequest request)
    {
        WarehouseFileStamp stamp = WarehouseFileStamp.Capture(request.WorkbookPath)
                                   ?? throw new WarehouseException(WarehouseErrorKind.FileNotFound,
                                       $"Không tìm thấy file phiếu để xem trước:\n{request.WorkbookPath}\n\nHãy xuất phiếu Excel trước.");

        // Phần đọc bằng NPOI (không cần Excel): thiết lập trang in, font dùng trên phiếu.
        WorkbookFacts facts = ReadWorkbookFacts(request);

        try
        {
            Directory.CreateDirectory(request.PdfFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WarehouseException(WarehouseErrorKind.PreviewFailed,
                $"Không tạo được thư mục tạm cho bản xem trước:\n{request.PdfFolder}", ex);
        }

        string pdfPath = Path.Combine(request.PdfFolder,
            $"{Path.GetFileNameWithoutExtension(request.WorkbookPath)}-{Guid.NewGuid():N}.pdf");

        try
        {
            return await BuildPreviewAsync(request, stamp, facts, pdfPath);
        }
        catch (Exception)
        {
            // Dựng hỏng giữa chừng: không để PDF dở dang (hoặc PDF của bản không
            // dùng được) nằm lại thư mục tạm — người gọi chỉ dọn PDF của bản thành công.
            DeletePreviewFile(pdfPath);
            throw;
        }
    }

    private static async Task<WarehousePrintPreview> BuildPreviewAsync(WarehousePreviewRequest request, WarehouseFileStamp stamp,
        WorkbookFacts facts, string pdfPath)
    {
        // Xem trước chỉ ĐỌC file phiếu: không chờ Excel thoát hẳn (2–3 giây) mới hiện bản xem trước.
        Stopwatch total = Stopwatch.StartNew();
        ExcelFacts excel = await ExcelAutomation.RunAsync(
            session => MeasureWithExcel(session, request, pdfPath),
            PreviewTimeout, "dựng bản xem trước in", WarehouseErrorKind.PreviewFailed, ExcelShutdown.Background);
        // Tổng gồm cả khởi động Excel — phần chênh so với tổng các bước là thời gian mở Excel.
        WarehouseLog.Info($"Thời gian dựng xem trước {Path.GetFileName(request.WorkbookPath)}: {excel.Timing}; " +
                          $"tổng (kể cả khởi động Excel) {total.ElapsedMilliseconds}ms");

        if (!File.Exists(pdfPath))
            throw new WarehouseException(WarehouseErrorKind.PreviewFailed, "Excel không tạo được bản xem trước (PDF) của phiếu.");

        if (!stamp.Matches(request.WorkbookPath))
            throw new WarehouseException(WarehouseErrorKind.OutputChanged,
                "File phiếu vừa bị thay đổi trong lúc dựng bản xem trước — hãy xuất lại rồi xem trước lại.");

        List<WarehousePreviewIssue> issues = new();
        issues.AddRange(excel.Issues);
        issues.AddRange(facts.Issues);

        string printedPaper;
        int renderedPages;
        using (PdfPageRenderer pdf = PdfPageRenderer.Open(pdfPath))
        {
            // PDF rỗng = không có gì để xem: báo lỗi thay vì mở cửa sổ xem trước "1 trang" trống.
            if (pdf.PageCount < 1)
                throw new WarehouseException(WarehouseErrorKind.PreviewFailed,
                    "Excel xuất bản xem trước không có trang nào — vùng in của phiếu có thể đang trống.\n\n" +
                    "Hãy mở file phiếu bằng Excel kiểm tra vùng in rồi xuất lại.");

            renderedPages = pdf.PageCount;
            if (pdf.PageCount != excel.PageCount)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Excel báo {excel.PageCount} trang nhưng bản xem trước có {pdf.PageCount} trang — hãy xem kỹ trước khi in."));

            SizeF size = pdf.GetPageSizePoints(0);
            printedPaper = DescribePaper(size);
            if (PaperMismatch(facts.Page, size) is { } mismatch)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"File đặt khổ {facts.Page.PaperName} {facts.Page.OrientationName.ToLowerInvariant()} nhưng {mismatch} với máy in " +
                    $"\"{excel.Printer}\": trang in thực tế là {printedPaper}. Excel phân trang theo khổ giấy của máy in — " +
                    "kiểm tra khay giấy / cài đặt máy in, hoặc chọn khổ giấy phù hợp ở phần thiết lập in."));
        }

        return new WarehousePrintPreview
        {
            WorkbookPath = request.WorkbookPath,
            SourceStamp = stamp,
            SheetName = request.Layout.SheetName,
            PdfPath = pdfPath,
            PageCount = excel.PageCount,
            RenderedPageCount = renderedPages,
            TemplatePageCount = excel.TemplatePageCount,
            PaginationPrinter = excel.Printer,
            Page = facts.Page,
            PrintedPaper = printedPaper,
            FontDescription = request.FontDescription,
            Pages = excel.Pages,
            Issues = issues
                .OrderByDescending(i => i.Level)
                .ThenBy(i => i.Page ?? int.MaxValue)
                .ToList()
        };
    }

    #region Khổ giấy thực tế

    // Sai số khi so kích thước trang (point): PDF làm tròn, khổ theo inch/mm.
    private const float PaperTolerancePt = 4f;

    /// <summary>Tên khổ giấy của một trang PDF (theo kích thước), vd "A4 (21 × 29,7 cm) dọc".</summary>
    public static string DescribePaper(SizeF pagePoints)
    {
        bool landscape = pagePoints.Width > pagePoints.Height;
        float shortSide = Math.Min(pagePoints.Width, pagePoints.Height);
        float longSide = Math.Max(pagePoints.Width, pagePoints.Height);
        WarehousePaperSizes.Paper? paper = WarehousePaperSizes.Common.FirstOrDefault(p =>
            Math.Abs(MmToPt(p.WidthMm) - shortSide) <= PaperTolerancePt && Math.Abs(MmToPt(p.HeightMm) - longSide) <= PaperTolerancePt);

        CultureInfo vi = CultureInfo.GetCultureInfo("vi-VN");
        string name = paper?.Display ??
                      $"{(shortSide / 72 * 2.54).ToString("0.##", vi)} × {(longSide / 72 * 2.54).ToString("0.##", vi)} cm";
        return name + (landscape ? " ngang" : " dọc");
    }

    /// <summary>
    /// Trang in thực tế khác khổ / hướng đặt trong file? Trả về mô tả khác
    /// biệt, null = khớp (hoặc file dùng khổ không biết kích thước).
    /// </summary>
    public static string? PaperMismatch(WarehousePageSetup page, SizeF pagePoints)
    {
        bool printedLandscape = pagePoints.Width > pagePoints.Height;
        if (printedLandscape != page.Landscape)
            return "hướng giấy khác";

        if (WarehousePaperSizes.Find(page.PaperSize) is not { } paper)
            return null;

        float shortSide = Math.Min(pagePoints.Width, pagePoints.Height);
        float longSide = Math.Max(pagePoints.Width, pagePoints.Height);
        bool same = Math.Abs(MmToPt(paper.WidthMm) - shortSide) <= PaperTolerancePt &&
                    Math.Abs(MmToPt(paper.HeightMm) - longSide) <= PaperTolerancePt;
        return same ? null : "khổ giấy khác";
    }

    private static float MmToPt(double mm) => (float)(mm / 25.4 * 72);

    #endregion

    #region NPOI: thiết lập trang + font

    private sealed record WorkbookFacts(WarehousePageSetup Page, List<WarehousePreviewIssue> Issues);

    private WorkbookFacts ReadWorkbookFacts(WarehousePreviewRequest request)
    {
        using IWorkbook workbook = WarehouseExcelReader.OpenWorkbook(request.WorkbookPath, "file phiếu");
        int sheetIndex = workbook.GetSheetIndex(request.Layout.SheetName);
        if (sheetIndex < 0)
            throw new WarehouseException(WarehouseErrorKind.PreviewFailed,
                $"File phiếu không còn sheet \"{request.Layout.SheetName}\" — hãy xuất lại.");

        ISheet sheet = workbook.GetSheetAt(sheetIndex);
        WarehousePageSetup page = WarehousePageSetupAccess.Read(workbook, sheetIndex);
        CellRangeAddress? area = WarehouseExcelWriter.ParsePrintArea(page.PrintArea);
        List<WarehousePreviewIssue> issues = new();

        HashSet<string> installed = new(_fontService.GetInstalledFamilies(), StringComparer.OrdinalIgnoreCase);
        SortedSet<string> missingFonts = new(StringComparer.OrdinalIgnoreCase);
        double minFontSize = double.MaxValue;
        int outsideArea = 0;

        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;

            foreach (ICell cell in row)
            {
                if (IsBlank(cell))
                    continue;

                if (area != null && !area.IsInRange(r, cell.ColumnIndex))
                {
                    outsideArea++;
                    continue;
                }

                IFont font = workbook.GetFontAt(cell.CellStyle.FontIndex);
                if (!string.IsNullOrWhiteSpace(font.FontName) && !installed.Contains(font.FontName))
                    missingFonts.Add(font.FontName);
                minFontSize = Math.Min(minFontSize, font.FontHeightInPoints);
            }
        }

        if (missingFonts.Count > 0)
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Phiếu dùng font chưa cài trên máy này: {string.Join(", ", missingFonts)} — Excel sẽ in bằng font thay thế, " +
                "chữ có thể rộng/hẹp hơn mẫu và tiếng Việt có thể lỗi."));

        if (outsideArea > 0)
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Có {outsideArea} ô có dữ liệu nằm NGOÀI vùng in ({page.PrintArea}) — các ô này sẽ không được in."));

        if (minFontSize < double.MaxValue)
        {
            if (!page.FitToPage && minFontSize * page.Scale / 100.0 < MinReadablePrintedFontPt)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Tỷ lệ in {page.Scale}%: chữ nhỏ nhất trên phiếu ({minFontSize:0.#}pt) sẽ in ra chỉ khoảng " +
                    $"{minFontSize * page.Scale / 100.0:0.#}pt — có thể khó đọc. Phóng to bản xem trước để kiểm tra."));
            else if (page.FitToPage)
                issues.Add(new(WarehousePreviewIssueLevel.Info,
                    $"Thiết lập in \"{page.ScaleDescription}\": Excel tự co nhỏ phiếu cho vừa trang, cỡ chữ in ra có thể nhỏ hơn " +
                    $"{minFontSize:0.#}pt — phóng to bản xem trước để kiểm tra chữ còn đọc được."));
        }

        return new WorkbookFacts(page, issues);
    }

    private static bool IsBlank(ICell cell) =>
        cell.CellType == CellType.Blank ||
        (cell.CellType == CellType.String && string.IsNullOrWhiteSpace(cell.StringCellValue));

    #endregion

    #region Excel: phân trang + PDF + đo bố cục

    private sealed record ExcelFacts(
        int PageCount, int? TemplatePageCount, string Printer, List<WarehousePreviewPage> Pages, List<WarehousePreviewIssue> Issues,
        string Timing);

    /// <summary>Dòng/cột (tính từ 1) Excel sẽ in: vùng in nếu có, không thì vùng có dữ liệu.</summary>
    private sealed record PrintScope(int FirstRow, int LastRow, int FirstColumn, int LastColumn);

    /// <summary>
    /// Thời gian từng bước dựng xem trước, ghi vào log. Máy khách báo "xem trước
    /// chậm" thì đọc log là biết chậm ở bước nào (mở file, phân trang theo máy in,
    /// xuất PDF, đo ô...) mà không phải đoán.
    /// </summary>
    private sealed class StageTimer
    {
        private readonly Stopwatch _watch = Stopwatch.StartNew();
        private readonly List<string> _stages = new();

        public void Mark(string stage)
        {
            _stages.Add($"{stage} {_watch.ElapsedMilliseconds}ms");
            _watch.Restart();
        }

        public override string ToString() => string.Join(", ", _stages);
    }

    private static ExcelFacts MeasureWithExcel(ExcelSession session, WarehousePreviewRequest request, string pdfPath)
    {
        StageTimer timer = new();
        List<WarehousePreviewIssue> issues = new();
        WarehouseTemplateLayout layout = request.Layout;

        dynamic workbook = session.OpenReadOnly(request.WorkbookPath);
        dynamic sheet = session.Worksheet(workbook, layout.SheetName);
        sheet.Activate();

        string printer = request.PrinterName != null
            ? session.SetActivePrinter(request.PrinterName)
            : (string)session.App.ActivePrinter;
        timer.Mark("mở phiếu + chọn máy in");

        dynamic pageSetup = session.Track(sheet.PageSetup);
        int pageCount = (int)session.Track(pageSetup.Pages).Count;
        timer.Mark("phân trang");

        // 1) PDF trước, khi workbook còn nguyên như file (các bước đo sau có đổi chiều cao dòng trong bộ nhớ).
        sheet.ExportAsFixedFormat(0 /* xlTypePDF */, pdfPath, 0 /* xlQualityStandard */, false, false);
        timer.Mark("xuất PDF");

        PrintScope scope = ReadPrintScope(session, (object)sheet, pageSetup.PrintArea as string);
        string titleRows = pageSetup.PrintTitleRows as string ?? "";

        // 2) Ngắt trang → dòng trên từng trang.
        (List<int> rowBreaks, List<int> columnBreaks)? breaks = ReadPageBreaks(session, (object)workbook, (object)sheet);
        List<WarehousePreviewPage> pages = new();
        if (breaks == null)
        {
            issues.Add(new(WarehousePreviewIssueLevel.Info,
                "Excel không cho đọc vị trí ngắt trang — xem từng trang trong bản xem trước để kiểm tra chỗ chia trang."));
        }
        else
        {
            List<int> splitColumns = breaks.Value.columnBreaks.Where(c => c > scope.FirstColumn && c <= scope.LastColumn).ToList();
            if (splitColumns.Count > 0)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Phiếu rộng hơn trang giấy: từ cột {ColumnName(splitColumns[0])} trở đi bị in sang trang riêng. " +
                    "Hãy đổi khổ giấy / hướng giấy / lề hoặc chọn \"Vừa chiều rộng trang\" ở phần thiết lập in."));
            else
                pages = BuildPages(breaks.Value.rowBreaks, scope, layout, pageCount);

            CheckPageSplits(session, (object)sheet, breaks.Value.rowBreaks, scope, layout, titleRows, pageCount, pages, issues);
        }
        timer.Mark("ngắt trang");

        // 3) Số trang so với mẫu (cùng máy in).
        int? templatePages = CountTemplatePages(session, request, printer, issues);
        if (templatePages is int tp && pageCount > tp && layout.DataRowCount <= request.TemplateRowCount)
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Số trang tăng bất thường: phiếu {pageCount} trang trong khi mẫu {tp} trang dù số dòng hàng không nhiều hơn mẫu " +
                $"({layout.DataRowCount} ≤ {request.TemplateRowCount}). Kiểm tra font / cỡ chữ / thiết lập in đã đổi."));
        timer.Mark("số trang mẫu");

        // 4) Đo từng ô (đổi chiều cao dòng / độ rộng cột trong bộ nhớ — làm SAU khi đã xuất PDF).
        MeasureCells(session, (object)workbook, (object)sheet, layout, pages, issues);
        timer.Mark("đo bố cục ô");

        return new ExcelFacts(pageCount, templatePages, printer, pages, issues, timer.ToString());
    }

    private static PrintScope ReadPrintScope(ExcelSession session, dynamic sheet, string? printArea)
    {
        dynamic range = !string.IsNullOrWhiteSpace(printArea) && !printArea.Contains(',')
            ? session.Track(sheet.Range[printArea])
            : session.Track(sheet.UsedRange);

        int firstRow = (int)range.Row;
        int firstColumn = (int)range.Column;
        int rows = (int)session.Track(range.Rows).Count;
        int columns = (int)session.Track(range.Columns).Count;
        return new PrintScope(firstRow, firstRow + rows - 1, firstColumn, firstColumn + columns - 1);
    }

    /// <summary>
    /// Vị trí ngắt trang (dòng / cột bắt đầu trang mới, tính từ 1). Excel chỉ
    /// trả đủ ngắt trang tự động khi cửa sổ ở chế độ Page Break Preview — bật
    /// tạm trên bản trong bộ nhớ. null = Excel không cho đọc.
    /// </summary>
    private static (List<int>, List<int>)? ReadPageBreaks(ExcelSession session, dynamic workbook, dynamic sheet)
    {
        try
        {
            dynamic window = session.Track(session.Track(workbook.Windows).Item(1));
            window.View = 2; // xlPageBreakPreview

            List<int> rows = new();
            dynamic hBreaks = session.Track(sheet.HPageBreaks);
            for (int i = 1; i <= (int)hBreaks.Count; i++)
                rows.Add((int)session.Track(session.Track(hBreaks.Item(i)).Location).Row);

            List<int> columns = new();
            dynamic vBreaks = session.Track(sheet.VPageBreaks);
            for (int i = 1; i <= (int)vBreaks.Count; i++)
                columns.Add((int)session.Track(session.Track(vBreaks.Item(i)).Location).Column);

            window.View = 1; // xlNormalView
            rows.Sort();
            columns.Sort();
            return (rows, columns);
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Không đọc được ngắt trang: {ex.Message}");
            return null;
        }
    }

    /// <summary>Ghép ngắt trang thành danh sách trang; không khớp số trang Excel báo thì bỏ (không đoán).</summary>
    private static List<WarehousePreviewPage> BuildPages(List<int> rowBreaks, PrintScope scope, WarehouseTemplateLayout layout, int pageCount)
    {
        List<int> starts = new() { scope.FirstRow };
        starts.AddRange(rowBreaks.Where(b => b > scope.FirstRow && b <= scope.LastRow));

        List<WarehousePreviewPage> pages = new();
        for (int i = 0; i < starts.Count; i++)
        {
            int first = starts[i];
            int last = i + 1 < starts.Count ? starts[i + 1] - 1 : scope.LastRow;
            pages.Add(new WarehousePreviewPage(i + 1, first, last, DescribeRows(first, last, layout)));
        }

        return pages.Count == pageCount ? pages : new List<WarehousePreviewPage>();
    }

    /// <summary>Mô tả nội dung trang theo các phần của phiếu (đầu phiếu / tiêu đề bảng / dòng hàng / cuối phiếu).</summary>
    public static string DescribeRows(int firstRow, int lastRow, WarehouseTemplateLayout layout)
    {
        int headerFirst = layout.HeaderFirstRow + 1;
        int headerLast = layout.HeaderLastRow + 1;
        int dataFirst = layout.DataFirstRow + 1;
        int dataLast = layout.DataFirstRow + layout.DataRowCount;

        List<string> parts = new();
        if (firstRow < headerFirst)
            parts.Add("đầu phiếu");
        if (firstRow <= headerLast && lastRow >= headerFirst)
            parts.Add("tiêu đề bảng");

        int from = Math.Max(firstRow, dataFirst);
        int to = Math.Min(lastRow, dataLast);
        if (from <= to)
            parts.Add(from == to ? $"dòng hàng {from - dataFirst + 1}" : $"dòng hàng {from - dataFirst + 1}–{to - dataFirst + 1}");

        if (lastRow > dataLast)
            parts.Add("cuối phiếu (ký tên)");

        return $"dòng Excel {firstRow}–{lastRow}: {string.Join(", ", parts)}";
    }

    private static void CheckPageSplits(ExcelSession session, dynamic sheet, List<int> rowBreaks, PrintScope scope,
        WarehouseTemplateLayout layout, string titleRows, int pageCount, List<WarehousePreviewPage> pages,
        List<WarehousePreviewIssue> issues)
    {
        int headerFirst = layout.HeaderFirstRow + 1;
        int headerLast = layout.HeaderLastRow + 1;
        int dataFirst = layout.DataFirstRow + 1;
        int dataLast = layout.DataFirstRow + layout.DataRowCount;

        List<int> inScope = rowBreaks.Where(b => b > scope.FirstRow && b <= scope.LastRow).ToList();
        for (int i = 0; i < inScope.Count; i++)
        {
            int b = inScope[i];
            int pageBefore = i + 1;

            if (b > headerFirst && b <= headerLast)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Tiêu đề bảng hàng bị cắt đôi giữa trang {pageBefore} và trang {pageBefore + 1}.", pageBefore));
            else if (b == dataFirst)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Trang {pageBefore} kết thúc ngay sau tiêu đề bảng — tiêu đề nằm riêng cuối trang, dòng hàng đầu tiên sang trang {pageBefore + 1}.",
                    pageBefore));
            else if (b > dataFirst && b <= dataLast)
                issues.Add(new(WarehousePreviewIssueLevel.Info,
                    $"Bảng hàng tiếp tục sang trang {pageBefore + 1} (từ dòng hàng {b - dataFirst + 1})" +
                    (string.IsNullOrWhiteSpace(titleRows) ? " — trang sau không lặp lại dòng tiêu đề cột." : "."),
                    pageBefore + 1));
        }

        if (pageCount <= 1)
            return;

        int lastStart = inScope.Count > 0 ? inScope[^1] : scope.FirstRow;
        if (lastStart > scope.LastRow)
            return;

        double filled = (double)session.Track(session.App.WorksheetFunction).CountA(
            session.Track(sheet.Range[session.Track(sheet.Cells[lastStart, scope.FirstColumn]),
                session.Track(sheet.Cells[scope.LastRow, scope.LastColumn])]));
        if (filled == 0)
            issues.Add(new(WarehousePreviewIssueLevel.Warning, $"Trang cuối (trang {pageCount}) không có nội dung — sẽ in ra trang trắng.", pageCount));
        else if (pages.Count == pageCount && lastStart > dataLast)
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Trang cuối (trang {pageCount}) chỉ có phần cuối phiếu (ký tên), không có dòng hàng nào.", pageCount));
    }

    /// <summary>Khoá bộ nhớ đệm số trang mẫu: đổi file mẫu (thời gian ghi / dung lượng), sheet hay máy in thì đếm lại.</summary>
    private sealed record TemplatePagesKey(string Path, WarehouseFileStamp Stamp, string Sheet, string Printer);

    /// <summary>
    /// Số trang của file mẫu đã đếm. Mở mẫu + phân trang theo máy in tốn vài
    /// giây mỗi lần, trong khi chế độ nhiều phiếu xem trước lần lượt nhiều
    /// phiếu cùng MỘT mẫu — chỉ đếm lần đầu, các phiếu sau dùng lại.
    /// </summary>
    private static readonly ConcurrentDictionary<TemplatePagesKey, int> TemplatePageCache = new();

    private static int? CountTemplatePages(ExcelSession session, WarehousePreviewRequest request, string printer,
        List<WarehousePreviewIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath) || WarehouseFileStamp.Capture(request.TemplatePath) is not { } stamp)
            return null;

        TemplatePagesKey key = new(Path.GetFullPath(request.TemplatePath).ToUpperInvariant(), stamp, request.Layout.SheetName, printer);
        if (TemplatePageCache.TryGetValue(key, out int cached))
            return cached;

        try
        {
            dynamic template = session.OpenReadOnly(request.TemplatePath);
            dynamic sheet = session.Worksheet(template, request.Layout.SheetName);
            int pages = (int)session.Track(session.Track(sheet.PageSetup).Pages).Count;
            TemplatePageCache[key] = pages;
            return pages;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Không đếm được số trang của mẫu: {ex.Message}");
            issues.Add(new(WarehousePreviewIssueLevel.Info, "Không đếm được số trang của file mẫu để so sánh."));
            return null;
        }
    }

    #region Đo bố cục ô

    // Mỗi lời gọi COM sang tiến trình Excel tốn thời gian, còn AutoFit bắt Excel
    // dàn trang lại — gọi theo TỪNG dòng (vài lần mỗi dòng) là thứ làm xem trước
    // chậm khi phiếu nhiều dòng. Nên ở đây:
    // - Đọc giá trị cả cột trong MỘT lời gọi (Value2 của cả vùng).
    // - AutoFit MỘT lần cho cả khối thay vì từng dòng / từng ô.
    // - Lọc nhanh trước, chỉ quay về kiểm tra từng ô khi bước lọc không chắc
    //   chắn (font lẫn lộn, ô gộp, cột có thể thiếu chỗ) — kết quả vẫn do chính
    //   Excel đo, không đoán theo số ký tự.

    /// <summary>Khối dòng hàng được đo (dòng Excel tính từ 1, gồm cả hai đầu).</summary>
    private readonly record struct DataBlock(int First, int Last)
    {
        public int Count => Last - First + 1;

        /// <summary>Số thứ tự dòng hàng (1, 2, ...) của một dòng Excel trong khối.</summary>
        public int ItemOf(int row) => row - First + 1;
    }

    /// <summary>Tên hàng có thể bị cắt khi in: dòng Excel + mô tả số đo.</summary>
    private sealed record ClippedName(int Row, string Detail);

    /// <summary>Định dạng chữ dùng chung của cả cột tên hàng (quyết định độ rộng chữ khi in).</summary>
    private sealed record SharedFont(string Name, double Size, bool Bold, bool Italic);

    /// <summary>
    /// Đo bằng chính Excel, theo đúng font / cỡ chữ / độ rộng ô / chế độ xuống
    /// dòng của từng ô (không đoán theo số ký tự):
    /// - Ô tên hàng CÓ xuống dòng: cho Excel tự giãn dòng; cần cao hơn dòng
    ///   đang có ⇒ chữ bị cắt (dòng đặt chiều cao cố định).
    /// - Ô tên hàng KHÔNG xuống dòng: ghi chữ sang sheet nháp, cho Excel tự
    ///   giãn cột; cần rộng hơn ô (kể cả vùng gộp) ⇒ chữ bị cắt / đè sang ô bên.
    /// - Ô số lượng hiện "###" ⇒ cột quá hẹp.
    /// </summary>
    private static void MeasureCells(ExcelSession session, dynamic workbook, dynamic sheet, WarehouseTemplateLayout layout,
        List<WarehousePreviewPage> pages, List<WarehousePreviewIssue> issues)
    {
        int count = Math.Min(layout.DataRowCount, MaxRowsMeasured);
        if (count > 0)
        {
            DataBlock block = new(layout.DataFirstRow + 1, layout.DataFirstRow + count);

            // "###" trước: bước lọc tạm giãn cột số lượng rồi trả lại độ rộng cũ, không ảnh hưởng phép đo tên hàng.
            List<int> hashRows = FindHashRows(session, (object)sheet, block, [layout.RequestedColumn + 1, layout.ActualColumn + 1]);
            List<ClippedName> clipped = FindClippedNames(session, (object)workbook, (object)sheet, block, layout.NameColumn + 1);

            foreach (ClippedName name in clipped.Take(MaxListedRows))
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Tên hàng dòng hàng {block.ItemOf(name.Row)} (dòng Excel {name.Row}) có thể bị cắt khi in: {name.Detail}.",
                    PageOf(name.Row, pages)));
            if (clipped.Count > MaxListedRows)
                issues.Add(new(WarehousePreviewIssueLevel.Warning, $"... và {clipped.Count - MaxListedRows} tên hàng khác có thể bị cắt."));

            if (hashRows.Count > 0)
                issues.Add(new(WarehousePreviewIssueLevel.Warning,
                    $"Cột số lượng quá hẹp, Excel in ra \"###\" ở dòng hàng {string.Join(", ", hashRows.Take(MaxListedRows).Select(block.ItemOf))}" +
                    (hashRows.Count > MaxListedRows ? "..." : "") + ".", PageOf(hashRows[0], pages)));
        }

        if (layout.DataRowCount > MaxRowsMeasured)
            issues.Add(new(WarehousePreviewIssueLevel.Info,
                $"Chỉ đo {MaxRowsMeasured} dòng hàng đầu; các dòng sau hãy kiểm tra trực tiếp trên bản xem trước."));
    }

    /// <summary>Các dòng (dòng Excel) có ô số lượng bị Excel hiện "###", tăng dần.</summary>
    private static List<int> FindHashRows(ExcelSession session, dynamic sheet, DataBlock block, int[] columns)
    {
        SortedSet<int> rows = new();
        foreach (int column in columns.Distinct())
        {
            dynamic range = ColumnRange(session, (object)sheet, block, column);
            if (!ColumnMayShowHashes(session, (object)sheet, (object)range, column))
                continue;

            object?[] values = ReadColumnValues((object)range, block.Count);
            for (int i = 0; i < block.Count; i++)
            {
                // Excel chỉ hiện "###" cho ô SỐ (Value2 của số / ngày là double) — ô chữ, ô trống bỏ qua.
                if (values[i] is not double)
                    continue;

                int row = block.First + i;
                string shown = session.Track(sheet.Cells[row, column]).Text as string ?? "";
                if (shown.Length > 0 && shown.All(ch => ch == '#'))
                    rows.Add(row);
            }
        }

        return rows.ToList();
    }

    /// <summary>
    /// Lọc nhanh cả cột bằng MỘT lần AutoFit trên các ô dữ liệu: Excel không cần
    /// cột rộng hơn ⇒ không ô nào hiện "###", khỏi đọc chữ hiển thị từng ô.
    /// Ô gộp (AutoFit bỏ qua ô gộp) hoặc cần rộng hơn ⇒ true, kiểm tra từng ô.
    /// Độ rộng cột được trả lại như cũ ngay sau khi đo.
    /// </summary>
    private static bool ColumnMayShowHashes(ExcelSession session, dynamic sheet, dynamic range, int column)
    {
        object merged = range.MergeCells; // false = không ô nào gộp; true / DBNull = có ô gộp
        if (merged is not false)
            return true;

        dynamic entireColumn = session.Track(sheet.Columns[column]);
        double widthBefore = (double)entireColumn.Width;
        object columnWidth = entireColumn.ColumnWidth;
        session.Track(range.Columns).AutoFit();
        double needed = (double)entireColumn.Width;
        entireColumn.ColumnWidth = columnWidth;
        return needed > widthBefore + 0.5;
    }

    /// <summary>Tên hàng (theo thứ tự dòng) có thể bị cắt khi in.</summary>
    private static List<ClippedName> FindClippedNames(ExcelSession session, dynamic workbook, dynamic sheet, DataBlock block, int column)
    {
        dynamic range = ColumnRange(session, (object)sheet, block, column);
        object?[] values = ReadColumnValues((object)range, block.Count);
        object wrapAll = range.WrapText; // true / false khi cả cột như nhau, DBNull khi lẫn lộn

        List<int> wrapped = new();
        List<(int Row, string Text)> unwrapped = new();
        for (int i = 0; i < block.Count; i++)
        {
            int row = block.First + i;
            string text = CellText(session, (object)sheet, row, column, values[i]);
            if (text.Length == 0)
                continue;

            bool wrap = wrapAll is bool all ? all : session.Track(sheet.Cells[row, column]).WrapText is true;
            if (wrap)
                wrapped.Add(row);
            else
                unwrapped.Add((row, text));
        }

        List<ClippedName> clipped = new();
        clipped.AddRange(FindClippedWrapped(session, (object)sheet, (object)range, wrapped));
        clipped.AddRange(FindClippedUnwrapped(session, (object)workbook, (object)sheet, (object)range, column, unwrapped));
        return clipped.OrderBy(c => c.Row).ToList();
    }

    /// <summary>
    /// Ô có xuống dòng: chiều cao dòng cần thiết do Excel tự giãn. Giãn MỘT lần
    /// cho cả khối (thay vì từng dòng) rồi so chiều cao trước / sau của các dòng
    /// cần đo. AutoFit một dòng không phụ thuộc dòng khác nên kết quả như giãn từng dòng.
    /// </summary>
    private static List<ClippedName> FindClippedWrapped(ExcelSession session, dynamic sheet, dynamic range, List<int> rows)
    {
        List<ClippedName> clipped = new();
        if (rows.Count == 0)
            return clipped;

        Dictionary<int, double> before = new();
        foreach (int row in rows)
            before[row] = (double)session.Track(sheet.Rows[row]).RowHeight;

        session.Track(range.EntireRow).AutoFit();

        foreach (int row in rows)
        {
            double needed = (double)session.Track(sheet.Rows[row]).RowHeight;
            if (needed > before[row] + 0.75)
                clipped.Add(new(row, $"cần cao {needed:0.#}pt, dòng chỉ cao {before[row]:0.#}pt"));
        }

        return clipped;
    }

    /// <summary>
    /// Ô KHÔNG xuống dòng: độ rộng chữ cần thiết do Excel tự giãn cột trên một
    /// sheet nháp, so với độ rộng ô (kể cả vùng gộp).
    /// </summary>
    private static List<ClippedName> FindClippedUnwrapped(ExcelSession session, dynamic workbook, dynamic sheet, dynamic range, int column,
        List<(int Row, string Text)> names)
    {
        List<ClippedName> clipped = new();
        if (names.Count == 0)
            return clipped;

        dynamic scratch = session.Track(session.Track(workbook.Worksheets).Add());
        SharedFont? font = ReadSharedFont(session, (object)range);
        double[] needed = font != null
            ? MeasureTextWidths(session, (object)scratch, names, font)
            : MeasureCellWidths(session, (object)sheet, (object)scratch, names, column);

        // Không ô nào gộp ⇒ mọi ô rộng bằng cột: đọc MỘT lần thay vì đọc vùng gộp của từng ô.
        object merged = range.MergeCells;
        double? columnWidth = merged is false ? (double)session.Track(sheet.Columns[column]).Width : null;

        for (int i = 0; i < names.Count; i++)
        {
            int row = names[i].Row;
            double available = columnWidth ?? (double)session.Track(session.Track(sheet.Cells[row, column]).MergeArea).Width;
            if (needed[i] > available + 1)
                clipped.Add(new(row, $"cần rộng {needed[i]:0.#}pt, ô chỉ rộng {available:0.#}pt, không bật xuống dòng"));
        }

        return clipped;
    }

    /// <summary>
    /// Font chung của cả cột tên hàng; null nếu các ô khác font / cỡ / kiểu chữ,
    /// có thụt lề, hoặc có ô định dạng từng phần chữ — khi đó phải đo từng ô.
    /// </summary>
    private static SharedFont? ReadSharedFont(ExcelSession session, dynamic range)
    {
        dynamic font = session.Track(range.Font);
        object name = font.Name, size = font.Size, bold = font.Bold, italic = font.Italic, indent = range.IndentLevel;
        bool noIndent = indent is int level ? level == 0 : indent is double d && d == 0;
        return name is string n && size is double s && bold is bool b && italic is bool it && noIndent
            ? new SharedFont(n, s, b, it)
            : null;
    }

    /// <summary>
    /// Cùng một font: ghi mọi tên vào MỘT hàng của sheet nháp (mỗi tên một cột),
    /// giãn tất cả cột MỘT lần rồi đọc độ rộng từng cột (point).
    /// </summary>
    private static double[] MeasureTextWidths(ExcelSession session, dynamic scratch, List<(int Row, string Text)> names, SharedFont font)
    {
        dynamic cells = session.Track(scratch.Range[session.Track(scratch.Cells[1, 1]), session.Track(scratch.Cells[1, names.Count])]);
        cells.NumberFormat = "@"; // giữ nguyên chữ — không để Excel đổi "36-39" thành ngày tháng
        cells.WrapText = false;
        dynamic cellFont = session.Track(cells.Font);
        cellFont.Name = font.Name;
        cellFont.Size = font.Size;
        cellFont.Bold = font.Bold;
        cellFont.Italic = font.Italic;

        object[,] values = new object[1, names.Count];
        for (int i = 0; i < names.Count; i++)
            values[0, i] = names[i].Text;
        cells.Value2 = values;
        session.Track(cells.Columns).AutoFit();

        double[] widths = new double[names.Count];
        for (int i = 0; i < names.Count; i++)
            widths[i] = (double)session.Track(scratch.Columns[i + 1]).Width;
        return widths;
    }

    /// <summary>
    /// Font khác nhau giữa các ô: chép TỪNG ô (kèm định dạng của chính ô đó)
    /// sang sheet nháp rồi giãn cột — chậm hơn nhưng đúng với mọi định dạng.
    /// </summary>
    private static double[] MeasureCellWidths(ExcelSession session, dynamic sheet, dynamic scratch, List<(int Row, string Text)> names,
        int column)
    {
        dynamic scratchCell = session.Track(scratch.Range["A1"]);
        dynamic scratchColumn = session.Track(scratch.Columns[1]);

        double[] widths = new double[names.Count];
        for (int i = 0; i < names.Count; i++)
        {
            session.Track(sheet.Cells[names[i].Row, column]).Copy(scratchCell);
            scratchCell.WrapText = false;
            scratchColumn.AutoFit();
            widths[i] = (double)scratchColumn.Width;
        }

        return widths;
    }

    /// <summary>Một cột của khối dữ liệu dưới dạng vùng Excel.</summary>
    private static dynamic ColumnRange(ExcelSession session, dynamic sheet, DataBlock block, int column) =>
        session.Track(sheet.Range[session.Track(sheet.Cells[block.First, column]), session.Track(sheet.Cells[block.Last, column])]);

    /// <summary>Giá trị (Value2) của một vùng một cột trong MỘT lời gọi COM; phần tử i = dòng thứ i của vùng.</summary>
    private static object?[] ReadColumnValues(dynamic range, int count)
    {
        object? value = range.Value2;
        object?[] result = new object?[count];
        if (value is object[,] grid)
        {
            int firstRow = grid.GetLowerBound(0);
            int firstColumn = grid.GetLowerBound(1);
            for (int i = 0; i < count; i++)
                result[i] = grid[firstRow + i, firstColumn];
        }
        else
        {
            result[0] = value; // vùng một ô: Excel trả thẳng giá trị, không phải mảng
        }

        return result;
    }

    /// <summary>Chữ Excel hiển thị trong ô: chữ lấy luôn từ Value2; số / ngày thì hỏi Excel (theo định dạng số của ô).</summary>
    private static string CellText(ExcelSession session, dynamic sheet, int row, int column, object? value) =>
        value switch
        {
            null => "",
            string text => text,
            _ => session.Track(sheet.Cells[row, column]).Text as string ?? ""
        };

    #endregion

    private static int? PageOf(int row, List<WarehousePreviewPage> pages) =>
        pages.FirstOrDefault(p => row >= p.FirstRow && row <= p.LastRow)?.Number;

    private static string ColumnName(int column) => CellReference.ConvertNumToColString(column - 1);

    #endregion

    /// <summary>Xoá các file PDF xem trước cũ còn sót (vd app bị tắt ngang) — giữ file của hôm nay.</summary>
    public static void CleanupOldPreviews(string folder) =>
        FileRetention.DeleteOlderThan(folder, "*.pdf", TimeSpan.FromDays(1));

    private static void DeletePreviewFile(string pdfPath)
    {
        try
        {
            if (File.Exists(pdfPath))
                File.Delete(pdfPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Còn khoá (Excel vừa bị dừng) — CleanupOldPreviews sẽ dọn ở lần xem trước sau.
            System.Diagnostics.Debug.WriteLine($"Không xoá được file xem trước {pdfPath}: {ex.Message}");
        }
    }

    public static string DefaultPreviewFolder =>
        Path.Combine(Path.GetTempPath(), "KiotVietLabelPrinter", "warehouse-preview");
}
