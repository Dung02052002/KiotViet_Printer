using KiotVietLabelPrinter.Models;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KiotVietLabelPrinter.Services;

public class ExcelService
{
    public List<ProductRow> ReadProducts(string sourceFile)
    {
        using FileStream fs = new(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        IWorkbook workbook = CreateWorkbook(fs, sourceFile);
        ISheet sheet = workbook.GetSheetAt(0);

        List<ProductRow> products = new();

        for (int i = 1; i <= sheet.LastRowNum; i++)
        {
            IRow? row = sheet.GetRow(i);
            if (row == null) continue;

            ProductRow item = new()
            {
                StoreName = GetCellString(row, 0),
                Category = GetCellString(row, 1),
                ProductCode = GetCellString(row, 2),
                Barcode = GetCellString(row, 3),
                ProductName = GetCellString(row, 4),
                ProductNameWithAttr = GetCellString(row, 5),
                Unit = GetCellString(row, 6),
                Quantity = GetCellDouble(row, 7),
                Price = GetCellDouble(row, 8),
                Description = GetCellString(row, 9),
                Attribute = GetCellString(row, 10),
                Position = GetCellString(row, 11)
            };

            if (string.IsNullOrWhiteSpace(item.ProductCode) &&
                string.IsNullOrWhiteSpace(item.ProductNameWithAttr))
            {
                continue;
            }

            products.Add(item);
        }

        workbook.Close();
        return products;
    }

    public void WriteFullLabelData(List<ProductRow> products, string targetFile)
    {
        using FileStream fs = new(targetFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        IWorkbook workbook = CreateWorkbook(fs, targetFile);
        ISheet sheet = workbook.GetSheetAt(0);

        ClearDataRows(sheet);

        int rowIndex = 1;
        foreach (var item in products)
        {
            IRow row = sheet.CreateRow(rowIndex++);

            SetCell(row, 0, item.StoreName);
            SetCell(row, 1, item.Category);
            SetCell(row, 2, item.ProductCode);
            SetCell(row, 3, item.Barcode);
            SetCell(row, 4, item.ProductName);
            SetCell(row, 5, item.ProductNameWithAttr);
            SetCell(row, 6, item.Unit);
            SetCell(row, 7, item.Quantity);
            SetCell(row, 8, item.Price);
            SetCell(row, 9, item.Description);
            SetCell(row, 10, item.Attribute);
            SetCell(row, 11, item.Position);
        }

        SaveWorkbook(workbook, targetFile);
    }

    public void WriteBarcodeLabelData(
        List<ProductRow> products,
        string targetFile,
        string employeeCode)
    {
        using FileStream fs = new(targetFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        IWorkbook workbook = CreateWorkbook(fs, targetFile);
        ISheet sheet = workbook.GetSheetAt(0);

        ClearDataRows(sheet);

        int rowIndex = 1;
        foreach (var item in products)
        {
            string parsedCode = BarcodeParser.Parse(
                item.ProductNameWithAttr,
                item.ProductCode);

            if (!string.IsNullOrWhiteSpace(employeeCode))
            {
                parsedCode = $"{parsedCode}-{employeeCode.Trim()}";
            }

            IRow row = sheet.CreateRow(rowIndex++);

            SetCell(row, 0, item.StoreName);
            SetCell(row, 1, item.Category);
            SetCell(row, 2, item.ProductCode);
            SetCell(row, 3, item.Barcode);
            SetCell(row, 4, item.ProductName);
            SetCell(row, 5, parsedCode); // tem mã vạch sẽ dùng cột F làm nội dung in mã
            SetCell(row, 6, item.Unit);
            SetCell(row, 7, item.Quantity);
            SetCell(row, 8, item.Price);
            SetCell(row, 9, item.Description);
            SetCell(row, 10, item.Attribute);
            SetCell(row, 11, item.Position);
        }

        SaveWorkbook(workbook, targetFile);
    }

    private static IWorkbook CreateWorkbook(Stream stream, string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();

        return ext switch
        {
            ".xls" => new HSSFWorkbook(stream),
            ".xlsx" => new XSSFWorkbook(stream),
            _ => throw new Exception("Định dạng Excel không hỗ trợ: " + ext)
        };
    }

    private static string GetCellString(IRow row, int cellIndex)
    {
        ICell? cell = row.GetCell(cellIndex);
        if (cell == null) return "";

        return cell.ToString()?.Trim() ?? "";
    }

    private static double GetCellDouble(IRow row, int cellIndex)
    {
        ICell? cell = row.GetCell(cellIndex);
        if (cell == null) return 0;

        if (cell.CellType == CellType.Numeric)
            return cell.NumericCellValue;

        if (double.TryParse(cell.ToString(), out double value))
            return value;

        return 0;
    }

    private static void SetCell(IRow row, int index, string value)
    {
        row.CreateCell(index).SetCellValue(value ?? "");
    }

    private static void SetCell(IRow row, int index, double value)
    {
        row.CreateCell(index).SetCellValue(value);
    }

    private static void ClearDataRows(ISheet sheet)
    {
        for (int i = sheet.LastRowNum; i >= 1; i--)
        {
            IRow? row = sheet.GetRow(i);
            if (row != null)
                sheet.RemoveRow(row);
        }
    }

  private static void SaveWorkbook(IWorkbook workbook, string targetFile)
{
    using FileStream output = new(targetFile, FileMode.Create, FileAccess.Write);
    workbook.Write(output);
    workbook.Close();
}
}