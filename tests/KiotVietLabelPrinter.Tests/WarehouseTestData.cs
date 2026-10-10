using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using HorizontalAlignment = NPOI.SS.UserModel.HorizontalAlignment;
using BorderStyle = NPOI.SS.UserModel.BorderStyle;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Dựng file phiếu mẫu + file hàng hóa giống cấu trúc file thật
/// (Desktop/Phiếu xuât Sài Gòn/*/Phiếu Xuất Kho.xlsx và file "Chi tiết
/// chuyển hàng" của KiotViet) để test không phụ thuộc file trên máy.
/// </summary>
internal static class WarehouseTestData
{
    public const string TemplateCode = "XK000001";

    // Đường dẫn file mẫu THẬT trên máy phát triển — test liên quan tự bỏ qua nếu không có.
    public static readonly string RealTemplatesFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Phiếu xuât Sài Gòn");

    /// <summary>
    /// Mẫu giống file thật: công ty (rich text) ở A1, "PHIẾU XUẤT KHO" B3,
    /// ngày B4, "Số : XK000001" B5, người nhận A7, địa chỉ A8:D8 (gộp), tiêu đề
    /// bảng dòng 12–13 (A12:A13, B12:B13, C12:D12 gộp), dòng hàng từ dòng 14,
    /// chữ ký dòng (16 + dataRows - 1), ghi chú gộp ở dưới cùng, vùng in A1:D(last).
    /// </summary>
    /// <param name="nameColumnChars">
    /// Độ rộng cột tên hàng (ký tự). Mặc định 30: cả bảng vừa chiều rộng A4 dọc khi Excel in; tăng lên
    /// (vd 60) để có phiếu rộng hơn trang giấy.
    /// </param>
    /// <param name="fixedRowHeight">
    /// &gt; 0: dòng hàng đặt chiều CAO CỐ ĐỊNH (pt) — tên hàng dài sẽ bị cắt khi in (để test cảnh báo bố cục).
    /// </param>
    public static string CreateTemplate(string dir, int dataRows = 2, string extension = ".xlsx", bool withPrintArea = true,
        bool includeActualColumn = true, string? fileName = null, float fixedRowHeight = 0, int nameColumnChars = 30)
    {
        IWorkbook wb = extension == ".xls" ? new HSSFWorkbook() : new XSSFWorkbook();
        ISheet sheet = wb.CreateSheet("Sheet1");
        wb.CreateSheet("Ghi chú").CreateRow(0).CreateCell(0).SetCellValue("Sheet phụ không được đụng tới");

        sheet.SetColumnWidth(0, nameColumnChars * 256);
        sheet.SetColumnWidth(1, 18 * 256);
        sheet.SetColumnWidth(2, 26 * 256);
        sheet.SetColumnWidth(3, 19 * 256);

        IFont times = Font(wb, "Times New Roman", 12, bold: false);
        IFont timesBold = Font(wb, "Times New Roman", 12, bold: true);
        IFont timesItalic = Font(wb, "Times New Roman", 12, bold: false, italic: true);
        IFont titleFont = Font(wb, "Times New Roman", 16, bold: true);

        ICellStyle plain = Style(wb, times);
        ICellStyle title = Style(wb, titleFont, HorizontalAlignment.Center);
        ICellStyle header = Style(wb, timesBold, HorizontalAlignment.Center, border: true);
        ICellStyle dataName = Style(wb, times, HorizontalAlignment.Left, border: true, wrap: true);
        ICellStyle dataCenter = Style(wb, times, HorizontalAlignment.Center, border: true);
        ICellStyle signature = Style(wb, times, HorizontalAlignment.Center, wrap: true);

        // A1: rich text 2 đoạn (đậm + thường), như ô tên công ty trong mẫu thật.
        ICell a1 = Cell(sheet, 0, 0, plain);
        IRichTextString company = wb.GetCreationHelper().CreateRichTextString("CÔNG TY TNHH PUCINI VIỆT NAM\nTầng 7, Số 113-115 Lê Duẩn");
        company.ApplyFont(0, 28, timesBold);
        company.ApplyFont(28, company.Length, times);
        a1.SetCellValue(company);
        Cell(sheet, 0, 2, plain).SetCellValue("Mẫu số : 02-VT");

        Cell(sheet, 2, 1, title).SetCellValue("PHIẾU XUẤT KHO");
        Cell(sheet, 3, 1, plain).SetCellValue("Ngày  22  tháng   9     năm 2026");
        Cell(sheet, 3, 3, plain).SetCellValue("Nợ: 632");
        Cell(sheet, 4, 1, plain).SetCellValue("Số : " + TemplateCode);
        Cell(sheet, 4, 3, plain).SetCellValue("Có: 156");
        Cell(sheet, 6, 0, plain).SetCellValue("Họ tên người nhận hàng: Kho hàng-Địa điểm kinh doanh công ty TNHH Pucini Việt Nam");
        Cell(sheet, 7, 0, plain).SetCellValue("Địa chỉ (bộ phận): Số 6 Đường 6C, TP Hồ Chí Minh");
        for (int c = 1; c <= 3; c++) Cell(sheet, 7, c, plain);
        sheet.AddMergedRegion(new CellRangeAddress(7, 7, 0, 3));
        Cell(sheet, 8, 0, plain).SetCellValue("Lý do xuất kho: Điều chuyển hàng hóa");

        // Tiêu đề bảng 2 dòng.
        for (int c = 0; c <= 3; c++) { Cell(sheet, 11, c, header); Cell(sheet, 12, c, header); }
        sheet.GetRow(11).GetCell(0).SetCellValue("Tên hàng hóa");
        sheet.GetRow(11).GetCell(1).SetCellValue("Đơn vị tính");
        sheet.GetRow(11).GetCell(2).SetCellValue("Số lượng");
        sheet.GetRow(12).GetCell(2).SetCellValue("Yêu cầu");
        if (includeActualColumn)
            sheet.GetRow(12).GetCell(3).SetCellValue("Thực xuất");
        sheet.AddMergedRegion(new CellRangeAddress(11, 12, 0, 0));
        sheet.AddMergedRegion(new CellRangeAddress(11, 12, 1, 1));
        sheet.AddMergedRegion(new CellRangeAddress(11, 11, 2, 3));

        for (int i = 0; i < dataRows; i++)
        {
            int r = 13 + i;
            Cell(sheet, r, 0, dataName).SetCellValue($"Hàng mẫu số {i + 1}, mô tả dài để xuống dòng trong ô tên hàng");
            Cell(sheet, r, 1, dataCenter).SetCellValue("Cái");
            Cell(sheet, r, 2, dataCenter).SetCellValue(10 + i);
            Cell(sheet, r, 3, dataCenter).SetCellValue(10 + i);
            if (fixedRowHeight > 0)
                sheet.GetRow(r).HeightInPoints = fixedRowHeight;
            else
                SetAutoHeight(sheet.GetRow(r), 47.25f);
        }

        int sigRow = 13 + dataRows + 1;
        string[] sigs = ["Người lập phiếu", "Người nhận hàng", "Kế toán", "Giám Đốc"];
        for (int c = 0; c <= 3; c++)
        {
            ICell cell = Cell(sheet, sigRow, c, signature);
            IRichTextString text = wb.GetCreationHelper().CreateRichTextString(sigs[c] + "\n(Ký, họ tên)");
            text.ApplyFont(0, sigs[c].Length, timesBold);
            text.ApplyFont(sigs[c].Length, text.Length, timesItalic);
            cell.SetCellValue(text);
        }
        sheet.GetRow(sigRow).HeightInPoints = 30.75f;

        int noteRow = sigRow + 2;
        Cell(sheet, noteRow, 0, plain).SetCellValue("Ghi chú cuối phiếu (ô gộp dưới bảng)");
        Cell(sheet, noteRow, 1, plain);
        sheet.AddMergedRegion(new CellRangeAddress(noteRow, noteRow, 0, 1));

        if (withPrintArea)
            wb.SetPrintArea(0, 0, 3, 0, noteRow);

        // Mã khổ giấy theo Excel/OOXML: 9 = A4. (Enum PaperSize.A4 của NPOI ghi ra mã 8 — Excel hiểu là A3.)
        sheet.PrintSetup.PaperSize = 9;
        sheet.PrintSetup.Landscape = false;
        sheet.SetMargin(MarginType.LeftMargin, 0.25);
        sheet.SetMargin(MarginType.RightMargin, 0.25);
        sheet.SetMargin(MarginType.TopMargin, 0.75);
        sheet.SetMargin(MarginType.BottomMargin, 0.75);

        string path = Path.Combine(dir, fileName ?? "Phiếu Xuất Kho" + extension);
        using FileStream fs = File.Create(path);
        wb.Write(fs);
        return path;
    }

    /// <summary>File "Chi tiết chuyển hàng" KiotViet: Mã hàng | Tên hàng | ĐVT | SL chuyển | SL nhận.</summary>
    public static string CreateGoods(string dir, params object?[][] rows) =>
        CreateGoods(dir, ["Mã hàng", "Tên hàng", "Đơn vị tính", "Số lượng chuyển", "Số lượng nhận"], rows);

    public static string CreateGoods(string dir, string[] header, object?[][] rows, string fileName = "goods.xlsx")
    {
        using XSSFWorkbook wb = new();
        ISheet sheet = wb.CreateSheet("ChiTietChuyenHang_TRF022042");
        IRow h = sheet.CreateRow(0);
        for (int c = 0; c < header.Length; c++)
            h.CreateCell(c).SetCellValue(header[c]);

        for (int i = 0; i < rows.Length; i++)
        {
            IRow row = sheet.CreateRow(i + 1);
            for (int c = 0; c < rows[i].Length; c++)
            {
                switch (rows[i][c])
                {
                    case null: break;
                    case double d: row.CreateCell(c).SetCellValue(d); break;
                    case int n: row.CreateCell(c).SetCellValue(n); break;
                    case string s: row.CreateCell(c).SetCellValue(s); break;
                }
            }
        }

        string path = Path.Combine(dir, fileName);
        using FileStream fs = File.Create(path);
        wb.Write(fs);
        return path;
    }

    public static object?[] Line(string code, string name, object? qty, string unit = "") => [code, name, unit, qty, 0];

    private static IFont Font(IWorkbook wb, string name, double size, bool bold, bool italic = false)
    {
        IFont font = wb.CreateFont();
        font.FontName = name;
        font.FontHeightInPoints = size;
        font.IsBold = bold;
        font.IsItalic = italic;
        return font;
    }

    private static ICellStyle Style(IWorkbook wb, IFont font, HorizontalAlignment align = HorizontalAlignment.General,
        bool border = false, bool wrap = false)
    {
        ICellStyle style = wb.CreateCellStyle();
        style.SetFont(font);
        style.Alignment = align;
        style.VerticalAlignment = VerticalAlignment.Center;
        style.WrapText = wrap;
        if (border)
        {
            style.BorderLeft = style.BorderRight = style.BorderTop = style.BorderBottom = BorderStyle.Thin;
        }
        return style;
    }

    private static ICell Cell(ISheet sheet, int r, int c, ICellStyle style)
    {
        IRow row = sheet.GetRow(r) ?? sheet.CreateRow(r);
        ICell cell = row.GetCell(c) ?? row.CreateCell(c);
        cell.CellStyle = style;
        return cell;
    }

    // Như file thật: dòng hàng có chiều cao nhưng KHÔNG cố định (Excel tự giãn khi mở).
    private static void SetAutoHeight(IRow row, float height)
    {
        if (row is XSSFRow x)
        {
            x.GetCTRow().ht = height;
            x.GetCTRow().UnsetCustomHeight();
        }
        else
        {
            row.HeightInPoints = height;
        }
    }

    public static string Sha256(string path) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
}
