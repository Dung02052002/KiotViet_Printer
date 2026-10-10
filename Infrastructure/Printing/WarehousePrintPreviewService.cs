using System.Globalization;
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

    /// <summary>Kiểm tra từng ô tên hàng tối đa bấy nhiêu dòng (mỗi dòng vài lời gọi COM).</summary>
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
        ExcelFacts excel = await ExcelAutomation.RunAsync(
            session => MeasureWithExcel(session, request, pdfPath),
            PreviewTimeout, "dựng bản xem trước in", WarehouseErrorKind.PreviewFailed);

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
        int PageCount, int? TemplatePageCount, string Printer, List<WarehousePreviewPage> Pages, List<WarehousePreviewIssue> Issues);

    /// <summary>Dòng/cột (tính từ 1) Excel sẽ in: vùng in nếu có, không thì vùng có dữ liệu.</summary>
    private sealed record PrintScope(int FirstRow, int LastRow, int FirstColumn, int LastColumn);

    private static ExcelFacts MeasureWithExcel(ExcelSession session, WarehousePreviewRequest request, string pdfPath)
    {
        List<WarehousePreviewIssue> issues = new();
        WarehouseTemplateLayout layout = request.Layout;

        dynamic workbook = session.OpenReadOnly(request.WorkbookPath);
        dynamic sheet = session.Worksheet(workbook, layout.SheetName);
        sheet.Activate();

        string printer = request.PrinterName != null
            ? session.SetActivePrinter(request.PrinterName)
            : (string)session.App.ActivePrinter;

        dynamic pageSetup = session.Track(sheet.PageSetup);
        int pageCount = (int)session.Track(pageSetup.Pages).Count;

        // 1) PDF trước, khi workbook còn nguyên như file (các bước đo sau có đổi chiều cao dòng trong bộ nhớ).
        sheet.ExportAsFixedFormat(0 /* xlTypePDF */, pdfPath, 0 /* xlQualityStandard */, false, false);

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

        // 3) Số trang so với mẫu (cùng máy in).
        int? templatePages = CountTemplatePages(session, request, issues);
        if (templatePages is int tp && pageCount > tp && layout.DataRowCount <= request.TemplateRowCount)
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Số trang tăng bất thường: phiếu {pageCount} trang trong khi mẫu {tp} trang dù số dòng hàng không nhiều hơn mẫu " +
                $"({layout.DataRowCount} ≤ {request.TemplateRowCount}). Kiểm tra font / cỡ chữ / thiết lập in đã đổi."));

        // 4) Đo từng ô (đổi chiều cao dòng trong bộ nhớ — làm SAU khi đã xuất PDF).
        MeasureCells(session, (object)workbook, (object)sheet, layout, pages, issues);

        return new ExcelFacts(pageCount, templatePages, printer, pages, issues);
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

    private static int? CountTemplatePages(ExcelSession session, WarehousePreviewRequest request, List<WarehousePreviewIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(request.TemplatePath) || !File.Exists(request.TemplatePath))
            return null;

        try
        {
            dynamic template = session.OpenReadOnly(request.TemplatePath);
            dynamic sheet = session.Worksheet(template, request.Layout.SheetName);
            return (int)session.Track(session.Track(sheet.PageSetup).Pages).Count;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            System.Diagnostics.Debug.WriteLine($"Không đếm được số trang của mẫu: {ex.Message}");
            issues.Add(new(WarehousePreviewIssueLevel.Info, "Không đếm được số trang của file mẫu để so sánh."));
            return null;
        }
    }

    /// <summary>
    /// Đo bằng chính Excel, theo đúng font / cỡ chữ / độ rộng ô / chế độ xuống
    /// dòng của từng ô (không đoán theo số ký tự):
    /// - Ô tên hàng CÓ xuống dòng: cho Excel tự giãn dòng; cần cao hơn dòng
    ///   đang có ⇒ chữ bị cắt (dòng đặt chiều cao cố định).
    /// - Ô tên hàng KHÔNG xuống dòng: chép ô sang sheet nháp, cho Excel tự
    ///   giãn cột; cần rộng hơn ô (kể cả vùng gộp) ⇒ chữ bị cắt / đè sang ô bên.
    /// - Ô số lượng hiện "###" ⇒ cột quá hẹp.
    /// </summary>
    private static void MeasureCells(ExcelSession session, dynamic workbook, dynamic sheet, WarehouseTemplateLayout layout,
        List<WarehousePreviewPage> pages, List<WarehousePreviewIssue> issues)
    {
        int first = layout.DataFirstRow + 1;
        int count = Math.Min(layout.DataRowCount, MaxRowsMeasured);
        int nameColumn = layout.NameColumn + 1;

        dynamic scratch = session.Track(session.Track(workbook.Worksheets).Add());
        dynamic scratchCell = session.Track(scratch.Range["A1"]);
        dynamic scratchColumn = session.Track(scratch.Columns[1]);

        List<(int Item, int Row, string Detail)> clipped = new();
        List<(int Item, int Row)> hashes = new();

        for (int k = 0; k < count; k++)
        {
            int r = first + k;
            dynamic cell = session.Track(sheet.Cells[r, nameColumn]);
            string text = cell.Text as string ?? "";
            if (text.Length > 0)
            {
                bool wrap = cell.WrapText is bool w && w;
                if (wrap)
                {
                    dynamic row = session.Track(sheet.Rows[r]);
                    double before = (double)row.RowHeight;
                    row.AutoFit();
                    double needed = (double)row.RowHeight;
                    if (needed > before + 0.75)
                        clipped.Add((k + 1, r, $"cần cao {needed:0.#}pt, dòng chỉ cao {before:0.#}pt"));
                }
                else
                {
                    double available = (double)session.Track(cell.MergeArea).Width;
                    cell.Copy(scratchCell);
                    scratchCell.WrapText = false;
                    scratchColumn.AutoFit();
                    double needed = (double)scratchColumn.Width;
                    if (needed > available + 1)
                        clipped.Add((k + 1, r, $"cần rộng {needed:0.#}pt, ô chỉ rộng {available:0.#}pt, không bật xuống dòng"));
                }
            }

            foreach (int column in new[] { layout.RequestedColumn + 1, layout.ActualColumn + 1 })
            {
                string shown = session.Track(sheet.Cells[r, column]).Text as string ?? "";
                if (shown.Length > 0 && shown.All(ch => ch == '#'))
                {
                    hashes.Add((k + 1, r));
                    break;
                }
            }
        }

        foreach ((int item, int row, string detail) in clipped.Take(MaxListedRows))
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Tên hàng dòng hàng {item} (dòng Excel {row}) có thể bị cắt khi in: {detail}.", PageOf(row, pages)));
        if (clipped.Count > MaxListedRows)
            issues.Add(new(WarehousePreviewIssueLevel.Warning, $"... và {clipped.Count - MaxListedRows} tên hàng khác có thể bị cắt."));

        if (hashes.Count > 0)
            issues.Add(new(WarehousePreviewIssueLevel.Warning,
                $"Cột số lượng quá hẹp, Excel in ra \"###\" ở dòng hàng {string.Join(", ", hashes.Take(MaxListedRows).Select(h => h.Item))}" +
                (hashes.Count > MaxListedRows ? "..." : "") + ".", PageOf(hashes[0].Row, pages)));

        if (layout.DataRowCount > MaxRowsMeasured)
            issues.Add(new(WarehousePreviewIssueLevel.Info,
                $"Chỉ đo {MaxRowsMeasured} dòng hàng đầu; các dòng sau hãy kiểm tra trực tiếp trên bản xem trước."));
    }

    private static int? PageOf(int row, List<WarehousePreviewPage> pages) =>
        pages.FirstOrDefault(p => row >= p.FirstRow && row <= p.LastRow)?.Number;

    private static string ColumnName(int column) => CellReference.ConvertNumToColString(column - 1);

    #endregion

    /// <summary>Xoá các file PDF xem trước cũ còn sót (vd app bị tắt ngang) — giữ file của hôm nay.</summary>
    public static void CleanupOldPreviews(string folder)
    {
        if (!Directory.Exists(folder))
            return;

        foreach (string file in Directory.EnumerateFiles(folder, "*.pdf"))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-1))
                    File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"Không xoá được file xem trước cũ {file}: {ex.Message}");
            }
        }
    }

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
