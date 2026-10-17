using System.Globalization;
using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.SS.UserModel;
using NPOI.SS.Util;

namespace KiotVietLabelPrinter.Infrastructure.Excel;

/// <summary>
/// Đọc file phiếu xuất kho mẫu và file hàng hóa. Mọi vị trí đều được xác
/// định theo NỘI DUNG (chữ tiêu đề), không theo địa chỉ ô cố định. Không xác
/// định chắc chắn được thì báo lỗi rõ ràng thay vì đoán.
/// </summary>
public partial class WarehouseExcelReader
{
    // Chỉ dò tiêu đề trong phần đầu sheet — tiêu đề phiếu/bảng luôn ở trên cùng.
    private const int MaxHeaderScanRows = 200;
    private const int MaxGoodsHeaderScanRows = 20;

    // Tiêu đề cột (đã bỏ dấu, chữ thường) của bảng hàng trên phiếu mẫu.
    private static readonly string[] TemplateNameHeaders = ["ten hang hoa", "ten hang", "ten san pham", "ten hang hoa, dich vu"];
    private static readonly string[] TemplateUnitHeaders = ["don vi tinh", "dvt", "don vi"];
    private static readonly string[] TemplateRequestedHeaders = ["yeu cau", "so luong yeu cau", "sl yeu cau"];
    private static readonly string[] TemplateActualHeaders = ["thuc xuat", "so luong thuc xuat", "sl thuc xuat"];

    // Tiêu đề cột của file hàng hóa (file chi tiết chuyển hàng KiotViet:
    // Mã hàng | Tên hàng | Đơn vị tính | Số lượng chuyển | Số lượng nhận ...).
    private static readonly string[] GoodsCodeHeaders = ["ma hang", "ma hang hoa", "ma sp", "ma san pham"];
    private static readonly string[] GoodsNameHeaders = ["ten hang", "ten hang hoa", "ten san pham"];
    private static readonly string[] GoodsUnitHeaders = ["don vi tinh", "dvt", "don vi"];

    // File "Danh sách chi tiết chuyển hàng" (nhiều phiếu chuyển trong một file):
    // mỗi dòng hàng mang mã phiếu chuyển của nó.
    private static readonly string[] GoodsTransferCodeHeaders = ["ma chuyen hang", "ma phieu chuyen"];
    private static readonly string[] GoodsTransferNoteHeaders = ["ghi chu chuyen"];
    private static readonly string[] GoodsTransferDestinationHeaders = ["toi chi nhanh"];

    // Thứ tự ưu tiên: cột số lượng XUẤT/CHUYỂN đứng trước "Số lượng" chung.
    // So khớp nguyên văn — để "Số lượng nhận" không bao giờ bị lấy nhầm.
    private static readonly string[] GoodsQuantityHeaders =
        ["so luong chuyen", "so luong xuat", "sl xuat", "sl chuyen", "so luong yeu cau", "so luong", "sl"];

    #region Mở workbook

    /// <summary>
    /// Mở workbook CHỈ ĐỌC (FileShare.ReadWrite: vẫn đọc được khi file đang
    /// mở trong Excel). Nội dung được nạp hết vào bộ nhớ nên file gốc không
    /// bao giờ bị ghi.
    /// </summary>
    public static IWorkbook OpenWorkbook(string path, string role)
    {
        byte[] content = ReadAllBytesShared(path, role);
        return OpenWorkbook(content, path, role);
    }

    public static byte[] ReadAllBytesShared(string path, string role)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new WarehouseException(WarehouseErrorKind.FileNotFound,
                $"Không tìm thấy {role}:\n{path}\n\nHãy chọn lại file.");

        try
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using MemoryStream ms = new();
            fs.CopyTo(ms);
            return ms.ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new WarehouseException(WarehouseErrorKind.WorkbookUnreadable,
                $"Không đọc được {role} (file đang bị khóa hoặc không có quyền đọc):\n{path}", ex);
        }
    }

    public static IWorkbook OpenWorkbook(byte[] content, string path, string role)
    {
        try
        {
            // Tự nhận diện xls / xlsx theo nội dung thật của file.
            return WorkbookFactory.Create(new MemoryStream(content, writable: false));
        }
        catch (Exception ex)
        {
            throw new WarehouseException(WarehouseErrorKind.WorkbookUnreadable,
                $"{char.ToUpper(role[0])}{role[1..]} không phải file Excel hợp lệ hoặc đã bị hỏng:\n{Path.GetFileName(path)}\n\n" +
                "Hãy mở thử file bằng Excel, lưu lại dạng .xlsx rồi chọn lại.", ex);
        }
    }

    #endregion

    #region File phiếu mẫu

    public WarehouseTemplateLayout ReadTemplateLayout(string templatePath)
    {
        using IWorkbook workbook = OpenWorkbook(templatePath, "file phiếu mẫu");
        return DetectLayout(workbook, Path.GetExtension(templatePath));
    }

    /// <summary>
    /// Xác định cấu trúc phiếu trên một workbook đã mở. Dùng chung cho lúc
    /// đọc mẫu và lúc ghi (ghi luôn dò lại trên chính bản sao đang sửa).
    /// </summary>
    public static WarehouseTemplateLayout DetectLayout(IWorkbook workbook, string fileExtension)
    {
        for (int s = 0; s < workbook.NumberOfSheets; s++)
        {
            ISheet sheet = workbook.GetSheetAt(s);
            (int Row, int Col)? nameHeader = FindCell(sheet, 0, Math.Min(sheet.LastRowNum, MaxHeaderScanRows), TemplateNameHeaders);
            if (nameHeader == null)
                continue;

            return DetectLayoutOnSheet(workbook, s, nameHeader.Value, fileExtension);
        }

        throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
            "Không tìm thấy bảng hàng hóa (ô tiêu đề \"Tên hàng hóa\") trong bất kỳ sheet nào của file mẫu.\n\n" +
            "Hãy chọn đúng file phiếu xuất kho mẫu (có bảng Tên hàng hóa | Đơn vị tính | Số lượng: Yêu cầu / Thực xuất).");
    }

    private static WarehouseTemplateLayout DetectLayoutOnSheet(IWorkbook workbook, int sheetIndex, (int Row, int Col) nameHeader, string fileExtension)
    {
        ISheet sheet = workbook.GetSheetAt(sheetIndex);
        int headerFirst = nameHeader.Row;

        // Tiêu đề bảng chiếm 2 dòng (Số lượng gộp trên Yêu cầu / Thực xuất) —
        // dò thêm 2 dòng dưới ô "Tên hàng hóa".
        int scanLast = Math.Min(sheet.LastRowNum, headerFirst + 2);
        (int Row, int Col)? unit = FindCell(sheet, headerFirst, scanLast, TemplateUnitHeaders);
        (int Row, int Col)? requested = FindCell(sheet, headerFirst, scanLast, TemplateRequestedHeaders);
        (int Row, int Col)? actual = FindCell(sheet, headerFirst, scanLast, TemplateActualHeaders);

        List<string> missing = new();
        if (unit == null) missing.Add("\"Đơn vị tính\"");
        if (requested == null) missing.Add("\"Yêu cầu\" (số lượng)");
        if (actual == null) missing.Add("\"Thực xuất\" (số lượng)");
        if (missing.Count > 0)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                $"Sheet \"{sheet.SheetName}\" của file mẫu có cột \"Tên hàng hóa\" nhưng thiếu cột: {string.Join(", ", missing)}.\n\n" +
                "Hãy kiểm tra lại tiêu đề bảng trong file mẫu.");

        int[] columns = [nameHeader.Col, unit!.Value.Col, requested!.Value.Col, actual!.Value.Col];
        if (columns.Distinct().Count() != columns.Length)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                "Các cột Tên hàng hóa / Đơn vị tính / Yêu cầu / Thực xuất trong file mẫu bị trùng vị trí — không xác định được bảng.");

        // Dòng tiêu đề cuối = dòng thấp nhất của các ô tiêu đề, tính cả vùng
        // gộp (ô "Tên hàng hóa" thường gộp 2 dòng).
        int headerLast = new[] { nameHeader.Row, unit.Value.Row, requested.Value.Row, actual.Value.Row }.Max();
        foreach ((int r, int c) in new[] { nameHeader, unit.Value, requested.Value, actual.Value })
        {
            CellRangeAddress? merged = FindMergedRegion(sheet, r, c);
            if (merged != null)
                headerLast = Math.Max(headerLast, merged.LastRow);
        }

        int dataFirst = headerLast + 1;
        int dataCount = 0;
        while (dataFirst + dataCount <= sheet.LastRowNum &&
               RowHasData(sheet.GetRow(dataFirst + dataCount), columns))
        {
            dataCount++;
        }

        if (dataCount == 0)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                $"File mẫu không có dòng hàng hóa nào ngay dưới tiêu đề bảng (dòng {dataFirst + 1}).\n\n" +
                "Phiếu mẫu cần ít nhất 1 dòng hàng làm mẫu định dạng (viền, căn lề, font) cho các dòng sẽ ghi.");

        int dataLast = dataFirst + dataCount - 1;
        EnsureDataRegionIsSimple(sheet, dataFirst, dataLast, columns);

        (int codeRow, int codeCol, string label, string currentCode) = FindTicketCodeCell(sheet, headerFirst);
        (int dateRow, int dateCol, string dateText) = FindDocumentDateCell(sheet, headerFirst);

        ICell? nameCell = sheet.GetRow(dataFirst)?.GetCell(nameHeader.Col);
        IFont font = workbook.GetFontAt(nameCell?.CellStyle?.FontIndex ?? 0);

        return new WarehouseTemplateLayout
        {
            SheetIndex = sheetIndex,
            SheetName = sheet.SheetName,
            TicketCodeRow = codeRow,
            TicketCodeColumn = codeCol,
            TicketCodeLabel = label,
            TemplateTicketCode = currentCode,
            DocumentDateRow = dateRow,
            DocumentDateColumn = dateCol,
            DocumentDateText = dateText,
            HeaderFirstRow = headerFirst,
            HeaderLastRow = headerLast,
            NameColumn = nameHeader.Col,
            UnitColumn = unit.Value.Col,
            RequestedColumn = requested.Value.Col,
            ActualColumn = actual.Value.Col,
            DataFirstRow = dataFirst,
            DataRowCount = dataCount,
            NameFontName = font.FontName,
            NameFontSize = font.FontHeightInPoints,
            PrintArea = workbook.GetPrintArea(sheetIndex),
            Page = WarehousePageSetupAccess.Read(workbook, sheetIndex),
            FileExtension = string.IsNullOrEmpty(fileExtension) ? ".xlsx" : fileExtension.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Vùng dòng hàng phải "đơn giản" để thêm/bớt dòng an toàn: không có ô
    /// gộp, không có công thức trong 4 cột của bảng. Gặp cấu trúc phức tạp
    /// hơn thì dừng và báo, không thử sửa có nguy cơ làm hỏng mẫu.
    /// </summary>
    private static void EnsureDataRegionIsSimple(ISheet sheet, int dataFirst, int dataLast, int[] columns)
    {
        for (int i = 0; i < sheet.NumMergedRegions; i++)
        {
            CellRangeAddress region = sheet.GetMergedRegion(i);
            if (region.FirstRow <= dataLast && region.LastRow >= dataFirst)
                throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                    $"Các dòng hàng hóa mẫu (dòng {dataFirst + 1}–{dataLast + 1}) có ô gộp {region.FormatAsString()}.\n\n" +
                    "Chức năng chưa hỗ trợ thêm/bớt dòng hàng có ô gộp vì dễ làm lệch bố cục phiếu. " +
                    "Hãy bỏ gộp ô ở các dòng hàng trong file mẫu rồi chọn lại.");
        }

        for (int r = dataFirst; r <= dataLast; r++)
        {
            IRow? row = sheet.GetRow(r);
            foreach (int c in columns)
            {
                if (row?.GetCell(c)?.CellType == CellType.Formula)
                    throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                        $"Ô {new CellReference(r, c).FormatAsString()} trong dòng hàng mẫu là công thức.\n\n" +
                        "Chức năng chỉ ghi giá trị vào các cột Tên hàng / Đơn vị / Yêu cầu / Thực xuất và sẽ không ghi đè công thức. " +
                        "Hãy thay công thức bằng giá trị trong file mẫu (hoặc liên hệ để hỗ trợ mẫu này).");
            }
        }
    }

    /// <summary>
    /// Ô số phiếu: chữ dạng "Số : XK023056" hoặc "Số phiếu: ..." nằm phía trên
    /// bảng hàng. Phải có đúng một ô như vậy.
    /// </summary>
    private static (int Row, int Col, string Label, string Code) FindTicketCodeCell(ISheet sheet, int headerFirstRow)
    {
        List<(int Row, int Col, string Label, string Code)> found = new();
        for (int r = 0; r < headerFirstRow; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;

            foreach (ICell cell in row)
            {
                if (cell.CellType != CellType.String)
                    continue;

                Match m = TicketCodePattern().Match(cell.StringCellValue ?? "");
                if (m.Success)
                    found.Add((r, cell.ColumnIndex, m.Groups["label"].Value, m.Groups["code"].Value.Trim()));
            }
        }

        if (found.Count == 0)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                "Không tìm thấy ô số phiếu (dạng \"Số : XK...\") phía trên bảng hàng hóa trong file mẫu.\n\n" +
                "Hãy kiểm tra file mẫu có dòng \"Số : ...\" dưới tiêu đề PHIẾU XUẤT KHO.");

        if (found.Count > 1)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                "File mẫu có nhiều ô dạng \"Số : ...\" (" +
                string.Join(", ", found.Select(f => new CellReference(f.Row, f.Col).FormatAsString())) +
                ") — không xác định được ô nào là số phiếu.");

        return found[0];
    }

    /// <summary>
    /// Ô ngày lập phiếu: chữ dạng "Ngày 22 tháng 9 năm 2026" nằm phía trên
    /// bảng hàng. Phải có đúng một ô như vậy — không đoán địa chỉ ô.
    /// </summary>
    private static (int Row, int Col, string Text) FindDocumentDateCell(ISheet sheet, int headerFirstRow)
    {
        List<(int Row, int Col, string Text)> found = new();
        for (int r = 0; r < headerFirstRow; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;

            foreach (ICell cell in row)
            {
                if (cell.CellType == CellType.String && WarehouseDocumentDateService.IsDateText(cell.StringCellValue))
                    found.Add((r, cell.ColumnIndex, cell.StringCellValue));
            }
        }

        if (found.Count == 0)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                "Không tìm thấy ô ngày lập phiếu (dạng \"Ngày ... tháng ... năm ...\") phía trên bảng hàng hóa trong file mẫu.\n\n" +
                "Hãy kiểm tra file mẫu có dòng \"Ngày ... tháng ... năm ...\" dưới tiêu đề PHIẾU XUẤT KHO (ô chữ, không phải công thức).");

        if (found.Count > 1)
            throw new WarehouseException(WarehouseErrorKind.TemplateStructure,
                "File mẫu có nhiều ô dạng \"Ngày ... tháng ... năm ...\" (" +
                string.Join(", ", found.Select(f => new CellReference(f.Row, f.Col).FormatAsString())) +
                ") — không xác định được ô nào là ngày lập phiếu.");

        return found[0];
    }

    // "Số : XK023056" / "Số phiếu: 123" / "SỐ:" — nhãn (giữ nguyên khi ghi) + mã.
    [GeneratedRegex(@"^(?<label>\s*Số(?:\s+phiếu)?\s*[:：]\s*)(?<code>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TicketCodePattern();

    #endregion

    #region File hàng hóa

    public WarehouseGoodsFile ReadGoods(string goodsPath)
    {
        using IWorkbook workbook = OpenWorkbook(goodsPath, "file hàng hóa");
        string? lastProblem = null;

        for (int s = 0; s < workbook.NumberOfSheets; s++)
        {
            ISheet sheet = workbook.GetSheetAt(s);
            for (int r = sheet.FirstRowNum; r <= Math.Min(sheet.LastRowNum, sheet.FirstRowNum + MaxGoodsHeaderScanRows); r++)
            {
                IRow? row = sheet.GetRow(r);
                if (row == null)
                    continue;

                int code = FindColumn(row, GoodsCodeHeaders);
                int name = FindColumn(row, GoodsNameHeaders);
                (int quantity, string quantityHeader) = FindQuantityColumn(row);
                if (code < 0 && name < 0)
                    continue;

                if (quantity < 0)
                {
                    lastProblem = $"Sheet \"{sheet.SheetName}\" có cột Mã hàng/Tên hàng ở dòng {r + 1} nhưng không có cột số lượng " +
                                  "(\"Số lượng chuyển\", \"Số lượng xuất\" hoặc \"Số lượng\").";
                    continue;
                }

                if (name < 0)
                {
                    lastProblem = $"Sheet \"{sheet.SheetName}\" không có cột \"Tên hàng\" — không có nguồn tên hàng để ghi lên phiếu.";
                    continue;
                }

                GoodsColumns columns = new(code, name, FindColumn(row, GoodsUnitHeaders), quantity,
                    FindColumn(row, GoodsTransferCodeHeaders), FindColumn(row, GoodsTransferNoteHeaders),
                    FindColumn(row, GoodsTransferDestinationHeaders));
                return new WarehouseGoodsFile
                {
                    SheetName = sheet.SheetName,
                    HeaderRowNumber = r + 1,
                    HasCodeColumn = code >= 0,
                    HasTransferColumn = columns.TransferCode >= 0,
                    QuantityColumnHeader = quantityHeader,
                    Lines = ReadGoodsLines(sheet, r + 1, columns)
                };
            }
        }

        throw new WarehouseException(WarehouseErrorKind.GoodsStructure,
            (lastProblem ?? "Không tìm thấy dòng tiêu đề có cột \"Mã hàng\" / \"Tên hàng\" và cột số lượng trong file hàng hóa.") +
            "\n\nFile hàng hóa cần có các cột: Mã hàng, Tên hàng, Số lượng chuyển (hoặc Số lượng) — " +
            "ví dụ file \"Chi tiết chuyển hàng\" xuất từ KiotViet.");
    }

    /// <summary>Vị trí các cột của file hàng hóa (-1 = không có cột đó).</summary>
    private readonly record struct GoodsColumns(
        int Code, int Name, int Unit, int Quantity, int TransferCode, int TransferNote, int TransferDestination);

    private static List<WarehouseGoodsLine> ReadGoodsLines(ISheet sheet, int firstRow, GoodsColumns columns)
    {
        DataFormatter formatter = new(CultureInfo.InvariantCulture);
        List<WarehouseGoodsLine> lines = new();

        string Text(IRow row, int col) => col >= 0 ? CellText(row.GetCell(col), formatter) : "";

        for (int r = firstRow; r <= sheet.LastRowNum; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;

            string code = Text(row, columns.Code);
            string name = Text(row, columns.Name);
            string qtyText = Text(row, columns.Quantity);

            // Dòng trống (hoặc chỉ có đơn vị/giá) không phải một dòng hàng.
            if (code.Length == 0 && name.Length == 0 && qtyText.Length == 0)
                continue;

            lines.Add(new WarehouseGoodsLine
            {
                SourceRowNumber = r + 1,
                ProductCode = code,
                ProductName = name,
                SourceUnit = Text(row, columns.Unit),
                QuantityText = qtyText,
                Quantity = ReadQuantity(row.GetCell(columns.Quantity), qtyText),
                TransferCode = Text(row, columns.TransferCode),
                TransferNote = Text(row, columns.TransferNote),
                TransferDestination = Text(row, columns.TransferDestination)
            });
        }

        return lines;
    }

    /// <summary>
    /// Số lượng: ô số lấy đúng giá trị; ô chữ chỉ nhận khi toàn là chữ số và
    /// dấu ngăn cách ("12", "2,5", "1.000"). Còn lại trả null (= không hợp lệ)
    /// để báo lỗi, KHÔNG tự hiểu thành 0 hay 1.
    /// </summary>
    private static double? ReadQuantity(ICell? cell, string text)
    {
        if (cell == null)
            return null;

        if (cell.CellType == CellType.Numeric ||
            (cell.CellType == CellType.Formula && cell.CachedFormulaResultType == CellType.Numeric))
            return cell.NumericCellValue;

        string trimmed = text.Replace(" ", "");
        if (!NumberText().IsMatch(trimmed))
            return null;

        return ExcelService.ParseNumberText(trimmed);
    }

    [GeneratedRegex(@"^-?\d[\d.,]*$")]
    private static partial Regex NumberText();

    private static (int Column, string Header) FindQuantityColumn(IRow row)
    {
        foreach (string wanted in GoodsQuantityHeaders)
        {
            foreach (ICell cell in row)
            {
                if (cell.CellType == CellType.String && VietnameseText.Fold(cell.StringCellValue) == wanted)
                    return (cell.ColumnIndex, VietnameseText.CollapseSpaces(cell.StringCellValue));
            }
        }

        return (-1, "");
    }

    #endregion

    #region Helpers

    private static int FindColumn(IRow row, string[] headers)
    {
        foreach (ICell cell in row)
        {
            if (cell.CellType == CellType.String && headers.Contains(VietnameseText.Fold(cell.StringCellValue)))
                return cell.ColumnIndex;
        }

        return -1;
    }

    private static (int Row, int Col)? FindCell(ISheet sheet, int firstRow, int lastRow, string[] texts)
    {
        for (int r = Math.Max(0, firstRow); r <= lastRow; r++)
        {
            IRow? row = sheet.GetRow(r);
            if (row == null)
                continue;

            foreach (ICell cell in row)
            {
                if (cell.CellType == CellType.String && texts.Contains(VietnameseText.Fold(cell.StringCellValue)))
                    return (r, cell.ColumnIndex);
            }
        }

        return null;
    }

    public static CellRangeAddress? FindMergedRegion(ISheet sheet, int row, int col)
    {
        for (int i = 0; i < sheet.NumMergedRegions; i++)
        {
            CellRangeAddress region = sheet.GetMergedRegion(i);
            if (region.IsInRange(row, col))
                return region;
        }

        return null;
    }

    private static bool RowHasData(IRow? row, int[] columns) =>
        row != null && columns.Any(c => !IsBlank(row.GetCell(c)));

    private static bool IsBlank(ICell? cell) =>
        cell == null || cell.CellType == CellType.Blank ||
        (cell.CellType == CellType.String && string.IsNullOrWhiteSpace(cell.StringCellValue));

    /// <summary>
    /// Chữ của ô như Excel hiển thị. Ô số định dạng General là số nguyên thì
    /// ghi đủ chữ số (mã hàng dạng số dài không bị thành "8.9E+12").
    /// </summary>
    public static string CellText(ICell? cell, DataFormatter formatter)
    {
        if (cell == null)
            return "";

        CellType type = cell.CellType == CellType.Formula ? cell.CachedFormulaResultType : cell.CellType;
        switch (type)
        {
            case CellType.String:
                return VietnameseText.CollapseSpaces(cell.StringCellValue);
            case CellType.Numeric:
                double value = cell.NumericCellValue;
                string? format = cell.CellStyle?.GetDataFormatString();
                bool general = string.IsNullOrEmpty(format) || format.Equals("General", StringComparison.OrdinalIgnoreCase);
                if (general && value == Math.Floor(value) && Math.Abs(value) < 1e15)
                    return value.ToString("0", CultureInfo.InvariantCulture);
                return cell.CellType == CellType.Formula
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : formatter.FormatCellValue(cell).Trim();
            case CellType.Boolean:
                return cell.BooleanCellValue ? "TRUE" : "FALSE";
            default:
                return "";
        }
    }

    #endregion
}
