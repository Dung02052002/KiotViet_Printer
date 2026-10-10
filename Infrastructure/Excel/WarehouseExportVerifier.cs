using System.Globalization;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace KiotVietLabelPrinter.Infrastructure.Excel;

internal sealed record FontSig(string Name, double Size, bool Bold, bool Italic, int Underline);

internal sealed record StyleSig(
    string Borders, string Alignment, bool Wrap, string NumberFormat, string Fill, bool Locked);

internal sealed record CellSig(string Text, StyleSig Style, FontSig Font);

internal sealed record RowHeightSig(float HeightPt, bool Custom);

/// <summary>Lề làm tròn 3 chữ số (inch) để so sánh ổn định sau khi ghi / đọc lại.</summary>
internal sealed record MarginSig(double Left, double Right, double Top, double Bottom, double Header, double Footer)
{
    public static MarginSig Of(WarehouseMargins m) =>
        new(R(m.Left), R(m.Right), R(m.Top), R(m.Bottom), R(m.Header), R(m.Footer));

    private static double R(double v) => Math.Round(v, 3);
}

internal sealed record PageSig(
    bool Landscape, short PaperSize, short Scale, short FitWidth, short FitHeight, bool FitToPage,
    MarginSig Margins, string HeaderFooter, bool HCenter, bool VCenter)
{
    /// <summary>Thiết lập trang in mong đợi trên file kết quả: của mẫu + đúng các mục người dùng đã đổi.</summary>
    public PageSig With(WarehousePageSettings settings)
    {
        PageSig r = this with
        {
            PaperSize = settings.PaperSize ?? PaperSize,
            Landscape = settings.Landscape ?? Landscape
        };

        if (settings.Margins is { } m)
            r = r with { Margins = MarginSig.Of(m with { Header = Margins.Header, Footer = Margins.Footer }) };

        return settings.Scale?.Mode switch
        {
            WarehouseScaleMode.Percent => r with { FitToPage = false, Scale = settings.Scale.Percent },
            WarehouseScaleMode.FitWidth => r with { FitToPage = true, FitWidth = 1, FitHeight = 0 },
            WarehouseScaleMode.FitOnePage => r with { FitToPage = true, FitWidth = 1, FitHeight = 1 },
            _ => r
        };
    }
}

/// <summary>
/// Ảnh chụp những gì cần giữ nguyên của sheet phiếu trong file mẫu — để sau
/// khi ghi so lại từng ô, từng vùng gộp, từng thiết lập trang in.
/// </summary>
internal sealed class TemplateSnapshot
{
    public required WarehouseTemplateLayout Layout { get; init; }
    public required List<string> SheetNames { get; init; }
    public required Dictionary<(int Row, int Col), CellSig> Cells { get; init; }
    public required Dictionary<int, RowHeightSig> RowHeights { get; init; }
    public required List<CellRangeAddress> Merges { get; init; }
    public required Dictionary<int, double> ColumnWidths { get; init; }
    public required PageSig Page { get; init; }
    public required int[] RowBreaks { get; init; }

    public static TemplateSnapshot Capture(IWorkbook workbook, WarehouseTemplateLayout layout)
    {
        ISheet sheet = workbook.GetSheetAt(layout.SheetIndex);
        return new TemplateSnapshot
        {
            Layout = layout,
            SheetNames = SnapshotReader.SheetNames(workbook),
            Cells = SnapshotReader.Cells(workbook, sheet),
            RowHeights = SnapshotReader.RowHeights(sheet),
            Merges = SnapshotReader.Merges(sheet),
            ColumnWidths = SnapshotReader.ColumnWidths(sheet, layout.TableLastColumn),
            Page = SnapshotReader.Page(sheet),
            RowBreaks = sheet.RowBreaks ?? []
        };
    }
}

internal static class SnapshotReader
{
    private static readonly DataFormatter Formatter = new(CultureInfo.InvariantCulture);

    public static List<string> SheetNames(IWorkbook workbook) =>
        Enumerable.Range(0, workbook.NumberOfSheets).Select(workbook.GetSheetName).ToList();

    public static Dictionary<(int, int), CellSig> Cells(IWorkbook workbook, ISheet sheet)
    {
        Dictionary<(int, int), CellSig> cells = new();
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;
            foreach (ICell cell in row)
                cells[(r, cell.ColumnIndex)] = Cell(workbook, cell);
        }

        return cells;
    }

    public static CellSig Cell(IWorkbook workbook, ICell cell) =>
        new(WarehouseExcelReader.CellText(cell, Formatter), Style(cell.CellStyle), Font(workbook.GetFontAt(cell.CellStyle.FontIndex)));

    public static FontSig Font(IFont font) =>
        new(font.FontName, font.FontHeightInPoints, font.IsBold, font.IsItalic, (int)font.Underline);

    public static StyleSig Style(ICellStyle s) => new(
        $"{s.BorderLeft}/{s.BorderRight}/{s.BorderTop}/{s.BorderBottom}",
        $"{s.Alignment}/{s.VerticalAlignment}/{s.Indention}/{s.Rotation}",
        s.WrapText,
        s.GetDataFormatString() ?? "",
        $"{s.FillPattern}/{ColorKey(s)}",
        s.IsLocked);

    private static string ColorKey(ICellStyle s) =>
        s is XSSFCellStyle xs && xs.FillForegroundXSSFColor is { } c
            ? (c.ARGBHex ?? $"theme{c.Theme}")
            : s.FillForegroundColor.ToString(CultureInfo.InvariantCulture);

    public static Dictionary<int, RowHeightSig> RowHeights(ISheet sheet)
    {
        Dictionary<int, RowHeightSig> heights = new();
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row != null)
                heights[r] = RowHeight(row);
        }

        return heights;
    }

    public static RowHeightSig RowHeight(IRow row) =>
        row is XSSFRow x
            ? new(x.GetCTRow().IsSetHt() ? (float)x.GetCTRow().ht : -1, x.GetCTRow().customHeight)
            : new(row.HeightInPoints, true);

    public static List<CellRangeAddress> Merges(ISheet sheet) =>
        Enumerable.Range(0, sheet.NumMergedRegions).Select(sheet.GetMergedRegion).ToList();

    public static Dictionary<int, double> ColumnWidths(ISheet sheet, int minLastColumn)
    {
        int last = minLastColumn;
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            last = Math.Max(last, (sheet.GetRow(r)?.LastCellNum ?? 0) - 1);

        return Enumerable.Range(0, last + 1).ToDictionary(c => c, sheet.GetColumnWidth);
    }

    public static PageSig Page(ISheet sheet)
    {
        IPrintSetup p = sheet.PrintSetup;
        MarginSig margins = MarginSig.Of(new WarehouseMargins(
            sheet.GetMargin(MarginType.LeftMargin), sheet.GetMargin(MarginType.RightMargin),
            sheet.GetMargin(MarginType.TopMargin), sheet.GetMargin(MarginType.BottomMargin),
            sheet.GetMargin(MarginType.HeaderMargin), sheet.GetMargin(MarginType.FooterMargin)));
        string headerFooter = $"{sheet.Header.Left}|{sheet.Header.Center}|{sheet.Header.Right}|" +
                              $"{sheet.Footer.Left}|{sheet.Footer.Center}|{sheet.Footer.Right}";
        return new PageSig(p.Landscape, p.PaperSize, p.Scale, p.FitWidth, p.FitHeight, sheet.FitToPage,
            margins, headerFooter, sheet.HorizontallyCenter, sheet.VerticallyCenter);
    }
}

/// <summary>
/// Mở lại file vừa ghi và so với file mẫu. Bất kỳ sai khác nào ngoài những
/// thứ được phép đổi đều làm việc xuất thất bại (file tạm bị xoá).
/// </summary>
internal static class WarehouseExportVerifier
{
    private const int MaxReportedProblems = 12;

    /// <summary>Trả về các mục đã kiểm tra + cấu trúc phiếu dò lại trên file kết quả.</summary>
    public static (List<string> Checks, WarehouseTemplateLayout Layout) Verify(
        string path, TemplateSnapshot expected, WarehouseExportRequest request, int delta)
    {
        List<string> problems = new();
        List<string> checks = new();
        WarehouseTemplateLayout t = expected.Layout;
        int n = request.Rows.Count;

        IWorkbook workbook;
        try
        {
            workbook = WarehouseExcelReader.OpenWorkbook(path, "file kết quả vừa ghi");
        }
        catch (WarehouseException ex)
        {
            throw new WarehouseException(WarehouseErrorKind.VerificationFailed,
                "File vừa ghi không mở lại được — đã huỷ, không tạo file kết quả.", ex);
        }

        WarehouseTemplateLayout? o = null;
        using (workbook)
        {
            // 1. Sheet + cấu trúc phiếu dò lại được trên file kết quả.
            List<string> names = SnapshotReader.SheetNames(workbook);
            if (!names.SequenceEqual(expected.SheetNames))
                problems.Add($"Danh sách sheet khác mẫu: [{string.Join(", ", names)}] ≠ [{string.Join(", ", expected.SheetNames)}].");
            else
                checks.Add($"Mở lại được file; {names.Count} sheet, đúng tên và thứ tự như mẫu.");

            try
            {
                o = WarehouseExcelReader.DetectLayout(workbook, t.FileExtension);
            }
            catch (WarehouseException ex)
            {
                problems.Add("Không dò lại được bảng hàng trên file kết quả: " + ex.Message);
            }

            if (o != null)
            {
                ISheet sheet = workbook.GetSheetAt(o.SheetIndex);
                VerifyLayout(o, t, n, request, problems, checks);
                VerifyDocumentDate(sheet, o, t, request.DocumentDate, problems, checks);
                VerifyRows(sheet, o, request.Rows, problems, checks);
                VerifyUntouchedCells(workbook, sheet, expected, request, delta, problems, checks);
                VerifyDataFormatting(workbook, sheet, expected, request, problems, checks);
                VerifyFonts(workbook, sheet, o, n, request.Font, problems, checks);
                VerifySheetSettings(workbook, sheet, expected, request.Page, delta, problems, checks);
            }
        }

        if (problems.Count > 0 || o == null)
            throw new WarehouseException(WarehouseErrorKind.VerificationFailed,
                "File vừa ghi KHÔNG khớp với file mẫu / dữ liệu nên đã bị huỷ (không tạo file kết quả):\n- " +
                string.Join("\n- ", problems.Take(MaxReportedProblems)) +
                (problems.Count > MaxReportedProblems ? $"\n... và {problems.Count - MaxReportedProblems} sai khác khác." : ""));

        return (checks, o);
    }

    private static void VerifyLayout(WarehouseTemplateLayout o, WarehouseTemplateLayout t, int n,
        WarehouseExportRequest request, List<string> problems, List<string> checks)
    {
        if (o.SheetIndex != t.SheetIndex || o.TicketCodeRow != t.TicketCodeRow || o.TicketCodeColumn != t.TicketCodeColumn)
            problems.Add("Ô số phiếu không còn ở đúng vị trí như mẫu.");
        else if (o.TemplateTicketCode != request.TicketCode || o.TicketCodeLabel != t.TicketCodeLabel)
            problems.Add($"Ô số phiếu là \"{o.TicketCodeLabel}{o.TemplateTicketCode}\", mong đợi \"{t.TicketCodeLabel}{request.TicketCode}\".");
        else
            checks.Add($"Số phiếu: \"{o.TicketCodeLabel}{o.TemplateTicketCode}\" tại {new CellReference(o.TicketCodeRow, o.TicketCodeColumn).FormatAsString()}.");

        if (o.HeaderFirstRow != t.HeaderFirstRow || o.DataFirstRow != t.DataFirstRow ||
            o.NameColumn != t.NameColumn || o.UnitColumn != t.UnitColumn ||
            o.RequestedColumn != t.RequestedColumn || o.ActualColumn != t.ActualColumn)
            problems.Add("Tiêu đề bảng hàng hóa bị lệch so với mẫu.");

        if (o.DataRowCount != n)
            problems.Add($"Bảng có {o.DataRowCount} dòng hàng, mong đợi {n}.");
        else
            checks.Add($"Bảng hàng có đúng {n} dòng (mẫu có {t.DataRowCount} dòng), bắt đầu từ dòng {o.DataFirstRow + 1}.");
    }

    /// <summary>
    /// Ô ngày lập phiếu: đúng vị trí như mẫu, chữ đúng nguyên văn (kể cả khoảng
    /// trắng căn lề của mẫu) và đọc ngược lại ra đúng ngày đã chọn — tránh mọi
    /// kiểu đảo ngày / tháng hay ghi nhầm ngày hôm nay.
    /// </summary>
    private static void VerifyDocumentDate(ISheet sheet, WarehouseTemplateLayout o, WarehouseTemplateLayout t, DateTime date,
        List<string> problems, List<string> checks)
    {
        string address = new CellReference(t.DocumentDateRow, t.DocumentDateColumn).FormatAsString();
        if (o.DocumentDateRow != t.DocumentDateRow || o.DocumentDateColumn != t.DocumentDateColumn)
        {
            problems.Add($"Ô ngày lập phiếu không còn ở đúng vị trí như mẫu ({address}).");
            return;
        }

        ICell cell = sheet.GetRow(o.DocumentDateRow).GetCell(o.DocumentDateColumn);
        string want = WarehouseDocumentDateService.Compose(t.DocumentDateText, date);
        if (cell.StringCellValue != want || WarehouseDocumentDateService.TryRead(cell.StringCellValue) != date.Date)
            problems.Add($"Ô ngày lập phiếu {address} là \"{cell.StringCellValue}\", mong đợi \"{want}\" ({date:dd/MM/yyyy}).");
        else
            checks.Add($"Ngày lập phiếu: \"{VietnameseText.CollapseSpaces(want)}\" ({date:dd/MM/yyyy}) tại {address}.");
    }

    private static void VerifyRows(ISheet sheet, WarehouseTemplateLayout o, IReadOnlyList<WarehouseProductRow> rows,
        List<string> problems, List<string> checks)
    {
        int bad = 0;
        for (int i = 0; i < rows.Count; i++)
        {
            IRow? row = sheet.GetRow(o.DataFirstRow + i);
            WarehouseProductRow item = rows[i];
            string at = $"dòng {o.DataFirstRow + i + 1}";

            if (Text(row?.GetCell(o.NameColumn)) != VietnameseText.CollapseSpaces(item.TicketName))
                Fail($"Tên hàng ở {at} không khớp.");
            if (Text(row?.GetCell(o.UnitColumn)) != VietnameseText.CollapseSpaces(item.Unit))
                Fail($"Đơn vị ở {at} không khớp (\"{Text(row?.GetCell(o.UnitColumn))}\" ≠ \"{item.Unit}\").");
            if (Number(row?.GetCell(o.RequestedColumn)) != item.RequestedQuantity)
                Fail($"Số lượng yêu cầu ở {at} không khớp.");
            if (Number(row?.GetCell(o.ActualColumn)) != item.ActualQuantity)
                Fail($"Số lượng thực xuất ở {at} không khớp.");
        }

        if (bad == 0)
            checks.Add($"{rows.Count} dòng hàng: tên hàng, đơn vị tính, SL yêu cầu, SL thực xuất khớp từng dòng với dữ liệu xem trước.");

        void Fail(string message)
        {
            bad++;
            problems.Add(message);
        }
    }

    /// <summary>
    /// Mọi ô NGOÀI vùng dòng hàng (tiêu đề, người nhận, chữ ký...) phải
    /// giữ nguyên chữ + định dạng (ô số phiếu, ô ngày lập phiếu: chỉ chữ được
    /// đổi đúng giá trị mới); phần dưới bảng so tại vị trí đã dời. Không
    /// được có ô nào ngoài vùng hàng bị ghi thêm.
    /// </summary>
    private static void VerifyUntouchedCells(IWorkbook workbook, ISheet sheet, TemplateSnapshot expected,
        WarehouseExportRequest request, int delta, List<string> problems, List<string> checks)
    {
        WarehouseTemplateLayout t = expected.Layout;
        HashSet<(int, int)> fontScope = FontScopeCells(sheet, t, request.Rows.Count, request.Font);
        int compared = 0;
        int before = problems.Count;

        foreach (((int r, int c), CellSig want) in expected.Cells)
        {
            if (r >= t.DataFirstRow && r <= t.DataLastRow)
                continue;

            int outRow = r < t.DataFirstRow ? r : r + delta;
            ICell? cell = sheet.GetRow(outRow)?.GetCell(c);
            string address = new CellReference(outRow, c).FormatAsString();
            if (cell == null)
            {
                if (want.Text.Length > 0)
                    problems.Add($"Ô {address} của mẫu bị mất (\"{Short(want.Text)}\").");
                continue;
            }

            CellSig got = SnapshotReader.Cell(workbook, cell);
            string wantText = r == t.TicketCodeRow && c == t.TicketCodeColumn
                ? VietnameseText.CollapseSpaces(t.TicketCodeLabel + request.TicketCode)
                : r == t.DocumentDateRow && c == t.DocumentDateColumn
                    ? VietnameseText.CollapseSpaces(WarehouseDocumentDateService.Compose(t.DocumentDateText, request.DocumentDate))
                    : want.Text;

            if (got.Text != wantText)
                problems.Add($"Ô {address} bị đổi nội dung: \"{Short(got.Text)}\" (mẫu: \"{Short(wantText)}\").");
            if (got.Style != want.Style)
                problems.Add($"Ô {address} bị đổi định dạng (viền/căn lề/nền/định dạng số).");
            if (!fontScope.Contains((outRow, c)) && got.Font != want.Font)
                problems.Add($"Ô {address} bị đổi font ({got.Font.Name} {got.Font.Size}pt, mẫu {want.Font.Name} {want.Font.Size}pt).");
            compared++;
        }

        // Ô có chữ mới xuất hiện ngoài vùng hàng = ghi nhầm chỗ.
        int dataLast = t.DataFirstRow + request.Rows.Count - 1;
        for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
        {
            if (r >= t.DataFirstRow && r <= dataLast)
                continue;
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;
            int templateRow = r < t.DataFirstRow ? r : r - delta;
            foreach (ICell cell in row)
            {
                if (Text(cell).Length > 0 && !expected.Cells.ContainsKey((templateRow, cell.ColumnIndex)))
                    problems.Add($"Ô {new CellReference(r, cell.ColumnIndex).FormatAsString()} có dữ liệu mới ngoài vùng hàng hóa.");
            }
        }

        if (problems.Count == before)
            checks.Add($"{compared} ô ngoài bảng hàng (tiêu đề, người nhận, chữ ký...) giữ nguyên nội dung và định dạng; " +
                       "chỉ ô số phiếu và ô ngày lập phiếu đổi giá trị.");
    }

    /// <summary>Định dạng (viền, căn lề, xuống dòng, định dạng số, nền) của các dòng hàng phải giống dòng hàng mẫu.</summary>
    private static void VerifyDataFormatting(IWorkbook workbook, ISheet sheet, TemplateSnapshot expected,
        WarehouseExportRequest request, List<string> problems, List<string> checks)
    {
        WarehouseTemplateLayout t = expected.Layout;
        HashSet<(int, int)> fontScope = FontScopeCells(sheet, t, request.Rows.Count, request.Font);
        int before = problems.Count;

        for (int k = 0; k < request.Rows.Count; k++)
        {
            int templateRow = t.DataFirstRow + Math.Min(k, t.DataRowCount - 1);
            int outRow = t.DataFirstRow + k;
            IRow? row = sheet.GetRow(outRow);

            foreach (((int r, int c), CellSig want) in expected.Cells.Where(e => e.Key.Row == templateRow))
            {
                ICell? cell = row?.GetCell(c);
                string address = new CellReference(outRow, c).FormatAsString();
                if (cell == null)
                {
                    problems.Add($"Ô {address} của dòng hàng bị thiếu định dạng.");
                    continue;
                }

                CellSig got = SnapshotReader.Cell(workbook, cell);
                if (got.Style != want.Style)
                    problems.Add($"Ô {address} khác định dạng dòng hàng mẫu (viền/căn lề/xuống dòng).");
                if (!fontScope.Contains((outRow, c)) && got.Font != want.Font)
                    problems.Add($"Ô {address} bị đổi font ngoài phạm vi đã chọn.");
            }

            if (row != null && expected.RowHeights.TryGetValue(templateRow, out RowHeightSig? h))
            {
                RowHeightSig got = SnapshotReader.RowHeight(row);
                if (got.Custom != h.Custom || (h.Custom && Math.Abs(got.HeightPt - h.HeightPt) > 0.01))
                    problems.Add($"Dòng {outRow + 1}: chế độ chiều cao dòng khác dòng hàng mẫu.");
            }
        }

        if (problems.Count == before)
            checks.Add("Các dòng hàng giữ đúng định dạng dòng hàng mẫu: viền, căn lề, xuống dòng, định dạng số, chế độ chiều cao dòng" +
                       (expected.RowHeights.TryGetValue(t.DataFirstRow, out RowHeightSig? first) && !first.Custom
                           ? " (tự giãn theo tên hàng khi mở bằng Excel)."
                           : "."));
    }

    private static void VerifyFonts(IWorkbook workbook, ISheet sheet, WarehouseTemplateLayout o, int n,
        WarehouseFontSettings font, List<string> problems, List<string> checks)
    {
        if (font.Scope == WarehouseFontApplyScope.KeepTemplate)
        {
            checks.Add("Font và cỡ chữ: giữ nguyên toàn bộ như mẫu (không chọn đổi font).");
            return;
        }

        int count = 0;
        int before = problems.Count;
        foreach (ICell cell in WarehouseFontApplier.CellsInScope(sheet, o, n, font.Scope))
        {
            FontSig got = SnapshotReader.Font(workbook.GetFontAt(cell.CellStyle.FontIndex));
            if (got.Name != font.FontFamily || Math.Abs(got.Size - font.FontSize) > 0.01)
                problems.Add($"Ô {new CellReference(cell).FormatAsString()} chưa đổi sang {font.FontFamily} {font.FontSize}pt.");

            if (cell.CellType == CellType.String && cell.RichStringCellValue is XSSFRichTextString rich &&
                rich.GetCTRst().r is { Count: > 0 } runs &&
                runs.Any(run => run.rPr?.rFont?.val != font.FontFamily || Math.Abs((run.rPr?.sz?.val ?? 0) - font.FontSize) > 0.01))
                problems.Add($"Ô {new CellReference(cell).FormatAsString()}: một phần chữ chưa đổi font.");
            count++;
        }

        if (problems.Count == before)
            checks.Add($"Font {font.FontFamily} {font.FontSize:0.#}pt đã áp dụng cho {count} ô trong phạm vi \"{ScopeName(font.Scope)}\"; " +
                       "các ô ngoài phạm vi giữ font của mẫu.");
    }

    private static void VerifySheetSettings(IWorkbook workbook, ISheet sheet, TemplateSnapshot expected,
        WarehousePageSettings pageSettings, int delta, List<string> problems, List<string> checks)
    {
        WarehouseTemplateLayout t = expected.Layout;
        int before = problems.Count;

        HashSet<string> wantMerges = expected.Merges.Select(m => Shift(m, t.DataLastRow, delta).FormatAsString()).ToHashSet();
        HashSet<string> gotMerges = SnapshotReader.Merges(sheet).Select(m => m.FormatAsString()).ToHashSet();
        if (!wantMerges.SetEquals(gotMerges))
            problems.Add($"Ô gộp khác mẫu: [{string.Join(", ", gotMerges.Order())}], mong đợi [{string.Join(", ", wantMerges.Order())}].");
        else
            checks.Add($"{gotMerges.Count} vùng ô gộp giữ nguyên ({string.Join(", ", gotMerges.Order())}).");

        Dictionary<int, double> widths = SnapshotReader.ColumnWidths(sheet, t.TableLastColumn);
        if (expected.ColumnWidths.Any(w => !widths.TryGetValue(w.Key, out double got) || Math.Abs(got - w.Value) > 0.001))
            problems.Add("Độ rộng cột khác mẫu.");
        else
            checks.Add("Độ rộng cột giữ nguyên.");

        foreach ((int r, RowHeightSig want) in expected.RowHeights.Where(h => h.Key < t.DataFirstRow || h.Key > t.DataLastRow))
        {
            IRow? row = sheet.GetRow(r < t.DataFirstRow ? r : r + delta);
            if (row == null || SnapshotReader.RowHeight(row) != want)
                problems.Add($"Chiều cao dòng {r + 1} của mẫu bị thay đổi.");
        }

        if (SnapshotReader.Page(sheet) != expected.Page.With(pageSettings))
            problems.Add(pageSettings.KeepsTemplate
                ? "Thiết lập trang in (khổ giấy / hướng / lề / co giãn / header-footer) khác mẫu."
                : "Thiết lập trang in không đúng như đã chọn (" + pageSettings.Describe() + ") hoặc các mục không chọn đổi bị thay đổi.");
        else if (pageSettings.KeepsTemplate)
            checks.Add("Thiết lập trang in giữ nguyên: khổ giấy, hướng giấy, lề, tỷ lệ/co giãn, header/footer.");
        else
            checks.Add($"Thiết lập trang in: đã đổi {pageSettings.Describe()}; các mục còn lại (header/footer, căn giữa...) giữ như mẫu.");

        CellRangeAddress? area = WarehouseExcelWriter.ParsePrintArea(t.PrintArea);
        string? gotArea = workbook.GetPrintArea(t.SheetIndex);
        if (t.PrintArea == null)
        {
            if (gotArea != null)
                problems.Add($"File kết quả có vùng in ({gotArea}) trong khi mẫu không đặt vùng in.");
            else
                checks.Add("Vùng in: mẫu không đặt vùng in — kết quả cũng không (Excel in toàn bộ vùng có dữ liệu).");
        }
        else if (area != null)
        {
            CellRangeAddress want = new(area.FirstRow, WarehouseExcelWriter.ShiftRowIndex(area.LastRow, t.DataLastRow, delta),
                area.FirstColumn, area.LastColumn);
            CellRangeAddress? got = WarehouseExcelWriter.ParsePrintArea(gotArea);
            if (got == null || got.FormatAsString() != want.FormatAsString())
                problems.Add($"Vùng in là {gotArea ?? "(trống)"}, mong đợi {want.FormatAsString()}.");
            else
                checks.Add($"Vùng in: {want.FormatAsString()}" + (delta != 0 ? $" (mẫu {area.FormatAsString()}, dời theo số dòng hàng)." : "."));
        }
        else
        {
            checks.Add($"Vùng in giữ nguyên chuỗi gốc của mẫu ({t.PrintArea}) — mẫu có nhiều vùng in nên không dời theo số dòng; hãy kiểm tra khi in.");
        }

        int[] wantBreaks = expected.RowBreaks.Select(b => WarehouseExcelWriter.ShiftRowIndex(b, t.DataLastRow, delta)).Distinct().Order().ToArray();
        int[] gotBreaks = (sheet.RowBreaks ?? []).Order().ToArray();
        if (!wantBreaks.SequenceEqual(gotBreaks))
            problems.Add("Ngắt trang thủ công khác mẫu.");

        if (problems.Count == before && delta != 0)
            checks.Add($"Phần dưới bảng (chữ ký...) đã dời {(delta > 0 ? "xuống" : "lên")} {Math.Abs(delta)} dòng, giữ nguyên nội dung và chiều cao dòng.");
    }

    private static CellRangeAddress Shift(CellRangeAddress m, int lastData, int delta) =>
        m.FirstRow > lastData
            ? new CellRangeAddress(m.FirstRow + delta, m.LastRow + delta, m.FirstColumn, m.LastColumn)
            : m;

    private static HashSet<(int, int)> FontScopeCells(ISheet sheet, WarehouseTemplateLayout layout, int n, WarehouseFontSettings font) =>
        WarehouseFontApplier.CellsInScope(sheet, layout, n, font.Scope)
            .Select(c => (c.RowIndex, c.ColumnIndex))
            .ToHashSet();

    public static string ScopeName(WarehouseFontApplyScope scope) => scope switch
    {
        WarehouseFontApplyScope.ProductNameOnly => "Chỉ tên hàng hóa",
        WarehouseFontApplyScope.ProductTable => "Toàn bộ bảng hàng hóa",
        WarehouseFontApplyScope.WholeDocument => "Toàn bộ nội dung phiếu",
        _ => "Giữ nguyên định dạng mẫu"
    };

    private static string Text(ICell? cell) =>
        WarehouseExcelReader.CellText(cell, new DataFormatter(CultureInfo.InvariantCulture));

    private static double? Number(ICell? cell) =>
        cell?.CellType == CellType.Numeric ? cell.NumericCellValue : null;

    private static string Short(string text) => text.Length > 40 ? text[..40] + "…" : text;
}
