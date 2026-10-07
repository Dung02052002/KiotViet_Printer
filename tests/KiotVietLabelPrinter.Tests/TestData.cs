using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Glasses.Tests;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KiotVietLabelPrinter.Tests;

internal static class TestData
{
    public static List<ParserTestCase> AllParserCases() =>
        ParserSamples.Get().Concat(BeltSamples.Get()).ToList();

    public static List<ProductRow> ProductsFromSamples() =>
        AllParserCases()
            .Select((t, i) => new ProductRow
            {
                ProductCode = $"SP{i:000}",
                ProductName = string.IsNullOrEmpty(t.ProductName) ? t.Input : t.ProductName,
                ProductNameWithAttr = t.Input,
                Quantity = i % 3 + 1,
                Price = 10000 * (i + 1)
            })
            .ToList();

    public static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "kv-label-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    // File xuất từ KiotViet: A Cửa hàng, B Nhóm, C Mã hàng, D Mã vạch, E Tên
    // hàng, F Tên hàng (thuộc tính), G ĐVT, H Số lượng, I Giá bán, J Mô tả.
    public static string CreateKiotVietSource(string dir)
    {
        string path = Path.Combine(dir, "kiotviet.xlsx");
        using IWorkbook wb = new XSSFWorkbook();
        ISheet sheet = wb.CreateSheet("Sheet1");

        string[] header = ["Cửa hàng", "Nhóm hàng", "Mã hàng", "Mã vạch", "Tên hàng", "Tên hàng (thuộc tính)", "ĐVT", "Số lượng", "Giá bán", "Mô tả"];
        Fill(sheet.CreateRow(0), header);

        object?[][] rows =
        [
            ["CH1", "Kính", "SP001", "8930001", "Kính mát nam", "Kính mát nam MK1234 C1 - Đen", "Chiếc", 2d, 150000d, "mô tả 1"],
            ["CH1", "Kính", "00123", "0012300", "Gọng kính", "Gọng kính model 6282 C3", "Chiếc", "3", "250000", ""],
            ["CH1", "TL", "TL001", "", "THẮT LƯNG NAM TL10 Mẫu01 - Da bò", "THẮT LƯNG NAM TL10 Mẫu01 - Da bò", "Chiếc", null, 99000d, ""],
            [null, null, null, null, null, null, null, null, null, null],
            ["CH1", "TL", "TL002", "", "THẮT LƯNG PUTL01 - Da A", "THẮT LƯNG PUTL01 - Da A", "Chiếc", 0d, "=1000*2", "dòng công thức"],
            ["CH1", "Khác", "SP999", "", "Sản phẩm không dấu", "San pham 12.5 ABC", "Cái", -1d, "1.234.000", ""],
        ];

        for (int i = 0; i < rows.Length; i++)
            Fill(sheet.CreateRow(i + 1), rows[i]);

        using FileStream fs = File.Create(path);
        wb.Write(fs);
        return path;
    }

    // File data của BarTender: chỉ có dòng tiêu đề, app ghi dữ liệu vào.
    public static string CreateBarTenderDataTemplate(string dir, string name)
    {
        string path = Path.Combine(dir, name);
        using IWorkbook wb = new HSSFWorkbook();
        ISheet sheet = wb.CreateSheet("Sheet1");
        Fill(sheet.CreateRow(0), ["Cửa hàng", "Nhóm hàng", "Mã hàng", "Mã vạch", "Tên hàng", "Tên hàng (thuộc tính)", "ĐVT", "Số lượng", "Giá bán", "Mô tả"]);
        using FileStream fs = File.Create(path);
        wb.Write(fs);
        return path;
    }

    public static List<List<string>> ReadAllCells(string path)
    {
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using IWorkbook wb = WorkbookFactory.Create(fs);
        ISheet sheet = wb.GetSheetAt(0);
        DataFormatter formatter = new(System.Globalization.CultureInfo.InvariantCulture);
        List<List<string>> result = [];
        for (int r = 0; r <= sheet.LastRowNum; r++)
        {
            IRow? row = sheet.GetRow(r);
            List<string> cells = [];
            if (row != null)
            {
                for (int c = 0; c < Math.Max((int)row.LastCellNum, 0); c++)
                {
                    ICell? cell = row.GetCell(c);
                    cells.Add(cell == null ? "" : $"{cell.CellType}:{formatter.FormatCellValue(cell)}");
                }
            }
            result.Add(cells);
        }
        return result;
    }

    private static void Fill(IRow row, object?[] values)
    {
        for (int c = 0; c < values.Length; c++)
        {
            switch (values[c])
            {
                case null:
                    break;
                case double d:
                    row.CreateCell(c).SetCellValue(d);
                    break;
                case string s when s.StartsWith('='):
                    row.CreateCell(c).SetCellFormula(s[1..]);
                    break;
                case string s:
                    row.CreateCell(c).SetCellValue(s);
                    break;
            }
        }
    }
}
