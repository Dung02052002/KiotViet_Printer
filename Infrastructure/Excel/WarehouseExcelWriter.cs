using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace KiotVietLabelPrinter.Infrastructure.Excel;

/// <summary>
/// Ghi phiếu xuất kho bằng cách SỬA MỘT BẢN SAO của chính workbook mẫu (không
/// dựng workbook mới): chỉ đổi giá trị ô số phiếu + 4 cột của các dòng hàng,
/// thêm/bớt dòng hàng khi số sản phẩm khác mẫu, và (chỉ khi người dùng chọn)
/// đổi font/cỡ chữ trong phạm vi đã chọn, đổi các mục thiết lập trang in đã
/// chọn. Mọi thứ khác — tiêu đề, người nhận, ngày, chữ ký, ô gộp, độ rộng
/// cột, các mục thiết lập trang in không đổi — giữ nguyên.
///
/// Quy trình an toàn: file mẫu chỉ được ĐỌC → sửa trong bộ nhớ → ghi ra file
/// tạm cạnh file đích → MỞ LẠI file tạm và so với mẫu → chỉ khi khớp mới đổi
/// tên thành file kết quả. Lỗi ở bất kỳ bước nào thì xoá file tạm, không để
/// lại file hỏng mang tên kết quả.
/// </summary>
public class WarehouseExcelWriter
{
    public WarehouseExportResult Export(WarehouseExportRequest request)
    {
        if (request.Rows.Count == 0)
            throw new WarehouseException(WarehouseErrorKind.InvalidData, "Không có dòng hàng nào để ghi lên phiếu.");

        string outputPath = Path.GetFullPath(request.OutputPath);
        if (string.Equals(outputPath, Path.GetFullPath(request.TemplatePath), StringComparison.OrdinalIgnoreCase))
            throw new WarehouseException(WarehouseErrorKind.OutputNotWritable,
                "File kết quả trùng với file phiếu mẫu — chọn thư mục output khác để không ghi đè file mẫu.");

        string folder = Path.GetDirectoryName(outputPath)!;
        EnsureFolderWritable(folder);
        EnsureCanReplace(outputPath, request.OverwriteExisting);

        byte[] templateBytes = WarehouseExcelReader.ReadAllBytesShared(request.TemplatePath, "file phiếu mẫu");
        string extension = Path.GetExtension(request.TemplatePath).ToLowerInvariant();

        // Bản chụp của mẫu (mở riêng, không sửa) để đối chiếu sau khi ghi.
        TemplateSnapshot expected;
        using (IWorkbook original = WarehouseExcelReader.OpenWorkbook(templateBytes, request.TemplatePath, "file phiếu mẫu"))
        {
            WarehouseTemplateLayout originalLayout = WarehouseExcelReader.DetectLayout(original, extension);
            expected = TemplateSnapshot.Capture(original, originalLayout);
        }

        string tempPath = Path.Combine(folder, $"~{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.tmp{extension}");
        try
        {
            int rowDelta;
            using (IWorkbook workbook = WarehouseExcelReader.OpenWorkbook(templateBytes, request.TemplatePath, "file phiếu mẫu"))
            {
                WarehouseTemplateLayout layout = WarehouseExcelReader.DetectLayout(workbook, extension);
                ISheet sheet = workbook.GetSheetAt(layout.SheetIndex);

                rowDelta = ResizeDataRegion(workbook, sheet, layout, request.Rows.Count);
                WriteTicketCode(sheet, layout, request.TicketCode);
                WriteRows(sheet, layout, request.Rows);
                WarehouseFontApplier.Apply(workbook, sheet, layout, request.Rows.Count, request.Font);
                WarehousePageSetupAccess.Apply(sheet, request.Page);

                SaveTo(workbook, tempPath);
            }

            (List<string> checks, WarehouseTemplateLayout outputLayout) = WarehouseExportVerifier.Verify(tempPath, expected, request, rowDelta);
            MoveIntoPlace(tempPath, outputPath, request.OverwriteExisting);

            return new WarehouseExportResult
            {
                OutputPath = outputPath,
                RowCount = request.Rows.Count,
                RowDelta = rowDelta,
                VerifiedChecks = checks,
                SheetName = outputLayout.SheetName,
                Layout = outputLayout,
                Stamp = WarehouseFileStamp.Capture(outputPath)
                        ?? throw new WarehouseException(WarehouseErrorKind.SaveFailed, $"File kết quả biến mất ngay sau khi ghi:\n{outputPath}")
            };
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    #region Thêm / bớt dòng hàng

    /// <summary>
    /// Đưa số dòng hàng của bảng về đúng số sản phẩm. Trả về số dòng đã thêm
    /// (dương) hoặc bớt (âm).
    ///
    /// Thêm: đẩy dòng hàng CUỐI cùng mọi thứ bên dưới xuống, rồi tạo các dòng
    /// mới vào khoảng trống, định dạng chép từ dòng hàng cuối. Chèn VÀO TRONG
    /// bảng (không phải sau bảng) để công thức/vùng tham chiếu tới cả bảng tự
    /// giãn theo, như khi chèn dòng trong Excel.
    /// Bớt: xoá các dòng hàng thừa ở cuối bảng rồi kéo phần bên dưới lên.
    /// Ô gộp / vùng in / ngắt trang phía dưới bảng được dời theo.
    /// </summary>
    private static int ResizeDataRegion(IWorkbook workbook, ISheet sheet, WarehouseTemplateLayout layout, int rowCount)
    {
        int delta = rowCount - layout.DataRowCount;
        if (delta == 0)
            return 0;

        int lastData = layout.DataLastRow;
        CellRangeAddress? printArea = ParsePrintArea(layout.PrintArea);
        int[] rowBreaks = sheet.RowBreaks ?? [];

        if (delta > 0)
        {
            sheet.ShiftRows(lastData, sheet.LastRowNum, delta, true, false);
            IRow styleRow = sheet.GetRow(lastData + delta);
            for (int r = lastData; r < lastData + delta; r++)
                CopyRowFormat(styleRow, sheet.CreateRow(r));
        }
        else
        {
            int firstRemoved = layout.DataFirstRow + rowCount;
            for (int r = firstRemoved; r <= lastData; r++)
            {
                IRow? row = sheet.GetRow(r);
                if (row != null)
                    sheet.RemoveRow(row);
            }

            if (lastData < sheet.LastRowNum)
                sheet.ShiftRows(lastData + 1, sheet.LastRowNum, delta, true, false);
        }

        // Vùng in và ngắt trang: tự đặt lại theo đúng quy tắc dời dòng, không
        // phụ thuộc thư viện có tự cập nhật hay không.
        if (printArea != null)
        {
            int lastRow = ShiftRowIndex(printArea.LastRow, lastData, delta);
            workbook.SetPrintArea(layout.SheetIndex, printArea.FirstColumn, printArea.LastColumn, printArea.FirstRow, lastRow);
        }

        foreach (int b in rowBreaks)
            sheet.RemoveRowBreak(b);
        foreach (int b in rowBreaks.Select(b => ShiftRowIndex(b, lastData, delta)).Distinct())
            sheet.SetRowBreak(b);

        return delta;
    }

    /// <summary>
    /// Vị trí mới (0-based) của một mốc dòng (cuối vùng in, ngắt trang) sau
    /// khi thêm/bớt dòng hàng: mốc ở dòng hàng cuối hoặc dưới bảng dời theo;
    /// mốc rơi vào các dòng hàng bị xoá thì về dòng hàng cuối mới.
    /// </summary>
    public static int ShiftRowIndex(int row, int templateLastDataRow, int delta)
    {
        if (row >= templateLastDataRow)
            return row + delta;

        int newLastDataRow = templateLastDataRow + delta;
        return delta < 0 && row > newLastDataRow ? newLastDataRow : row;
    }

    public static CellRangeAddress? ParsePrintArea(string? printArea)
    {
        if (string.IsNullOrWhiteSpace(printArea))
            return null;

        // "Sheet1!$A$1:$D$25" — nhiều vùng in (có dấu phẩy) thì chỉ lấy vùng
        // đầu không an toàn → bỏ qua, phần kiểm tra sau khi ghi sẽ báo lại.
        string area = printArea.Contains('!') ? printArea[(printArea.LastIndexOf('!') + 1)..] : printArea;
        if (area.Contains(','))
            return null;

        try
        {
            return CellRangeAddress.ValueOf(area.Replace("$", ""));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Chép định dạng một dòng hàng sang dòng mới: style từng ô, style dòng và
    /// CHẾ ĐỘ chiều cao. Với .xlsx chép thẳng thuộc tính chiều cao của dòng
    /// mẫu: dòng mẫu thường không đặt "chiều cao cố định" nên Excel tự giãn
    /// dòng theo tên hàng dài khi mở file — gán Height qua thư viện sẽ biến nó
    /// thành chiều cao cố định và tên dài bị che mất.
    /// </summary>
    private static void CopyRowFormat(IRow source, IRow target)
    {
        if (source is XSSFRow xs && target is XSSFRow xt)
        {
            var src = xs.GetCTRow();
            var dst = xt.GetCTRow();
            if (src.IsSetHt()) dst.ht = src.ht; else dst.UnsetHt();
            if (src.IsSetCustomHeight()) dst.customHeight = src.customHeight; else dst.UnsetCustomHeight();
            if (src.IsSetS()) dst.s = src.s; else dst.UnsetS();
            if (src.customFormat) dst.customFormat = true; else dst.UnsetCustomFormat();
        }
        else
        {
            target.Height = source.Height;
            if (source.RowStyle != null)
                target.RowStyle = source.RowStyle;
        }

        foreach (ICell cell in source)
            target.CreateCell(cell.ColumnIndex).CellStyle = cell.CellStyle;
    }

    #endregion

    #region Ghi giá trị

    private static void WriteTicketCode(ISheet sheet, WarehouseTemplateLayout layout, string ticketCode)
    {
        ICell cell = sheet.GetRow(layout.TicketCodeRow).GetCell(layout.TicketCodeColumn);
        cell.SetCellValue(layout.TicketCodeLabel + ticketCode);
    }

    private static void WriteRows(ISheet sheet, WarehouseTemplateLayout layout, IReadOnlyList<WarehouseProductRow> rows)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            WarehouseProductRow item = rows[i];
            IRow row = sheet.GetRow(layout.DataFirstRow + i) ?? sheet.CreateRow(layout.DataFirstRow + i);

            SetCell(row, layout.NameColumn, item.TicketName.Trim());
            SetCell(row, layout.UnitColumn, item.Unit.Trim());
            SetCell(row, layout.RequestedColumn, item.RequestedQuantity!.Value);
            SetCell(row, layout.ActualColumn, item.ActualQuantity!.Value);
        }
    }

    // Chỉ đổi GIÁ TRỊ: ô đã có thì giữ nguyên style. Ô chưa tồn tại (mẫu để
    // trống) mới tạo — lấy style của cột/dòng nếu có.
    private static ICell GetOrCreate(IRow row, int column)
    {
        ICell? cell = row.GetCell(column);
        if (cell != null)
            return cell;

        cell = row.CreateCell(column);
        ICellStyle? style = row.RowStyle ?? row.Sheet.GetColumnStyle(column);
        if (style != null)
            cell.CellStyle = style;
        return cell;
    }

    private static void SetCell(IRow row, int column, string value) => GetOrCreate(row, column).SetCellValue(value);

    private static void SetCell(IRow row, int column, double value) => GetOrCreate(row, column).SetCellValue(value);

    #endregion

    #region Lưu file an toàn

    private static void EnsureFolderWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            string probe = Path.Combine(folder, $"~kv-write-test-{Guid.NewGuid():N}.tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or NotSupportedException)
        {
            throw new WarehouseException(WarehouseErrorKind.OutputNotWritable,
                $"Không ghi được vào thư mục output:\n{folder}\n\nHãy chọn thư mục khác (ví dụ trong Documents hoặc Desktop).", ex);
        }
    }

    private static void EnsureCanReplace(string outputPath, bool overwrite)
    {
        if (!File.Exists(outputPath))
            return;

        if (!overwrite)
            throw new WarehouseException(WarehouseErrorKind.OutputExists,
                $"File kết quả đã tồn tại:\n{outputPath}");

        try
        {
            using (new FileStream(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WarehouseException(WarehouseErrorKind.OutputLocked,
                $"File kết quả đang được mở (có thể trong Excel) hoặc chỉ đọc nên không ghi đè được:\n{outputPath}\n\n" +
                "Hãy đóng file trong Excel rồi bấm xuất lại.", ex);
        }
    }

    private static void SaveTo(IWorkbook workbook, string path)
    {
        try
        {
            using FileStream fs = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            workbook.Write(fs, leaveOpen: true);
            fs.Flush(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WarehouseException(WarehouseErrorKind.SaveFailed,
                $"Không lưu được file phiếu vào thư mục output (ổ đầy, mất kết nối hoặc không có quyền ghi).\n{Path.GetDirectoryName(path)}", ex);
        }
    }

    private static void MoveIntoPlace(string tempPath, string outputPath, bool overwrite)
    {
        try
        {
            File.Move(tempPath, outputPath, overwrite);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WarehouseException(WarehouseErrorKind.OutputLocked,
                $"Không đặt được file kết quả vào vị trí (file cùng tên đang mở hoặc vừa được tạo):\n{outputPath}\n\n" +
                "Hãy đóng file trong Excel rồi xuất lại.", ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File tạm (tên bắt đầu bằng "~", đuôi .tmp.xlsx) không xoá được
            // thì để lại — không bao giờ mang tên file kết quả nên không gây nhầm.
            System.Diagnostics.Debug.WriteLine($"Không xoá được file tạm {path}: {ex.Message}");
        }
    }

    #endregion
}

/// <summary>
/// Áp dụng font/cỡ chữ người dùng chọn — CHỈ trong phạm vi đã chọn, và chỉ
/// đổi tên font + cỡ chữ: đậm/nghiêng/gạch chân/màu, viền, căn lề, nền, định
/// dạng số giữ nguyên. Ô dùng chung style với ô ngoài phạm vi được gán một
/// style SAO CHÉP (không sửa style dùng chung).
/// </summary>
internal static class WarehouseFontApplier
{
    public static void Apply(IWorkbook workbook, ISheet sheet, WarehouseTemplateLayout layout, int rowCount, WarehouseFontSettings font)
    {
        if (font.Scope == WarehouseFontApplyScope.KeepTemplate)
            return;

        Dictionary<short, ICellStyle> styleCache = new();
        Dictionary<short, IFont> fontCache = new();

        foreach (ICell cell in CellsInScope(sheet, layout, rowCount, font.Scope))
        {
            cell.CellStyle = RestyledFont(workbook, cell.CellStyle, font, styleCache, fontCache);
            ApplyToRichTextRuns(workbook, cell, font, fontCache);
        }
    }

    public static IEnumerable<ICell> CellsInScope(ISheet sheet, WarehouseTemplateLayout layout, int rowCount, WarehouseFontApplyScope scope)
    {
        int dataLast = layout.DataFirstRow + rowCount - 1;
        switch (scope)
        {
            case WarehouseFontApplyScope.ProductNameOnly:
                for (int r = layout.DataFirstRow; r <= dataLast; r++)
                {
                    ICell? cell = sheet.GetRow(r)?.GetCell(layout.NameColumn);
                    if (cell != null)
                        yield return cell;
                }
                break;

            case WarehouseFontApplyScope.ProductTable:
                for (int r = layout.HeaderFirstRow; r <= dataLast; r++)
                {
                    IRow? row = sheet.GetRow(r);
                    if (row == null)
                        continue;
                    for (int c = layout.TableFirstColumn; c <= layout.TableLastColumn; c++)
                    {
                        ICell? cell = row.GetCell(c);
                        if (cell != null)
                            yield return cell;
                    }
                }
                break;

            case WarehouseFontApplyScope.WholeDocument:
                for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
                {
                    IRow? row = sheet.GetRow(r);
                    if (row == null)
                        continue;
                    foreach (ICell cell in row)
                        yield return cell;
                }
                break;
        }
    }

    private static ICellStyle RestyledFont(IWorkbook workbook, ICellStyle style, WarehouseFontSettings settings,
        Dictionary<short, ICellStyle> styleCache, Dictionary<short, IFont> fontCache)
    {
        if (styleCache.TryGetValue(style.Index, out ICellStyle? cached))
            return cached;

        ICellStyle clone = workbook.CreateCellStyle();
        clone.CloneStyleFrom(style);
        clone.SetFont(DerivedFont(workbook, style.FontIndex, settings, fontCache));
        styleCache[style.Index] = clone;
        // Ô đã đổi rồi (style mới) gặp lại thì giữ nguyên.
        styleCache[clone.Index] = clone;
        return clone;
    }

    private static IFont DerivedFont(IWorkbook workbook, short fontIndex, WarehouseFontSettings settings, Dictionary<short, IFont> fontCache)
    {
        if (fontCache.TryGetValue(fontIndex, out IFont? cached))
            return cached;

        IFont source = workbook.GetFontAt(fontIndex);
        IFont font = workbook.CreateFont();
        font.IsBold = source.IsBold;
        font.IsItalic = source.IsItalic;
        font.IsStrikeout = source.IsStrikeout;
        font.Underline = source.Underline;
        font.TypeOffset = source.TypeOffset;
        font.Charset = source.Charset;
        font.Color = source.Color;
        if (source is XSSFFont xSource && font is XSSFFont xFont)
        {
            XSSFColor? color = xSource.GetXSSFColor();
            if (color != null)
                xFont.SetColor(color);
            xFont.Family = xSource.Family;
        }

        // Tên font + cỡ chữ là hai thứ DUY NHẤT thay đổi. Font mới không gắn
        // "scheme" của theme nên Excel dùng đúng tên font đã chọn.
        font.FontName = settings.FontFamily;
        font.FontHeightInPoints = settings.FontSize;

        fontCache[fontIndex] = font;
        fontCache[font.Index] = font;
        return font;
    }

    /// <summary>
    /// Ô chữ có nhiều định dạng trong cùng ô (vd "Người lập phiếu" đậm +
    /// "(Ký, họ tên)" nghiêng): font của từng đoạn đè lên font của ô, nên phải
    /// đổi tên font/cỡ chữ trên từng đoạn, giữ đậm/nghiêng của đoạn đó.
    /// </summary>
    private static void ApplyToRichTextRuns(IWorkbook workbook, ICell cell, WarehouseFontSettings settings, Dictionary<short, IFont> fontCache)
    {
        if (cell.CellType != CellType.String)
            return;

        if (cell.RichStringCellValue is XSSFRichTextString xRich)
        {
            var runs = xRich.GetCTRst().r;
            if (runs == null || runs.Count == 0)
                return;

            foreach (var run in runs)
            {
                run.rPr ??= new NPOI.OpenXmlFormats.Spreadsheet.CT_RPrElt();
                run.rPr.rFont = new NPOI.OpenXmlFormats.Spreadsheet.CT_FontName { val = settings.FontFamily };
                run.rPr.sz = new NPOI.OpenXmlFormats.Spreadsheet.CT_FontSize { val = settings.FontSize };
                // Bỏ gắn scheme (font theo theme) để tên font mới có hiệu lực.
                run.rPr.scheme = null;
            }
        }
        else if (cell.RichStringCellValue is HSSFRichTextString hRich && hRich.NumFormattingRuns > 0)
        {
            HSSFRichTextString rebuilt = new(hRich.String);
            for (int i = 0; i < hRich.NumFormattingRuns; i++)
            {
                int start = hRich.GetIndexOfFormattingRun(i);
                int end = i + 1 < hRich.NumFormattingRuns ? hRich.GetIndexOfFormattingRun(i + 1) : hRich.Length;
                short runFont = hRich.GetFontOfFormattingRun(i);
                rebuilt.ApplyFont(start, end, DerivedFont(workbook, runFont, settings, fontCache));
            }
            cell.SetCellValue(rebuilt);
        }
    }
}
