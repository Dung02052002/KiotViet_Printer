using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ExcelService
{
    #region Read products from KiotViet Excel
    public List<ProductRow> ReadProducts(string sourceFile)
    {
        IWorkbook workbook = OpenWorkbook(sourceFile);
        ISheet sheet = workbook.GetSheetAt(0);

        List<ProductRow> products = new();

        // Bỏ dòng tiêu đề, bắt đầu từ dòng 1
        for (int i = 1; i <= sheet.LastRowNum; i++)
        {
            IRow? row = sheet.GetRow(i);
            if (row == null) continue;

            string productCode = GetCellString(row, 2);           // C
            string productName = GetCellString(row, 3);           // D
            string productNameWithAttr = GetCellString(row, 5);   // F
            double quantity = GetCellDouble(row, 7);              // H
            double price = GetCellDouble(row, 8);                 // I

            if (string.IsNullOrWhiteSpace(productCode) &&
                string.IsNullOrWhiteSpace(productName) &&
                string.IsNullOrWhiteSpace(productNameWithAttr))
            {
                continue;
            }

            products.Add(new ProductRow
            {
                ProductCode = productCode,
                ProductName = productName,
                ProductNameWithAttr = productNameWithAttr,
                Quantity = quantity,
                Price = price
            });
        }

        workbook.Close();
        return products;
    }
    #endregion

    #region Generic label write
    public void WriteGenericLabelData(
        List<ProductRow> products,
        LabelDefinition label)
    {
        IWorkbook workbook = OpenWorkbook(label.DataFilePath);
        ISheet sheet = workbook.GetSheetAt(0);

        ClearDataRows(sheet);

        for (int i = 0; i < products.Count; i++)
        {
            ProductRow item = products[i];
            IRow row = sheet.CreateRow(i + 1);

            WriteDefaultProductRow(row, item);
        }

        SaveWorkbook(workbook, label.DataFilePath);
    }
    #endregion

    #region Barcode-like label write
    public void WriteBarcodeLikeData(
        List<ProductRow> products,
        LabelDefinition label,
        string employeeCode)
    {
        IWorkbook workbook = OpenWorkbook(label.DataFilePath);
        ISheet sheet = workbook.GetSheetAt(0);

        ClearDataRows(sheet);

        for (int i = 0; i < products.Count; i++)
        {
            ProductRow item = products[i];
            IRow row = sheet.CreateRow(i + 1);

            // ghi mặc định trước
            WriteDefaultProductRow(row, item);

            string finalText = item.ProductNameWithAttr;

            if (label.UseBarcodeParser)
            {
                finalText = BarcodeParser.Parse(
                    item.ProductNameWithAttr,
                    item.ProductCode);
            }

            if (label.AppendEmployeeCode &&
                !string.IsNullOrWhiteSpace(employeeCode))
            {
                finalText = $"{finalText}-{employeeCode.Trim()}";
            }

            int targetCol = label.TargetNameColumnIndex;
            ICell cell = row.GetCell(targetCol) ?? row.CreateCell(targetCol);
            cell.SetCellValue(finalText);
        }

        SaveWorkbook(workbook, label.DataFilePath);
    }
    #endregion

    #region Helpers
    private static void WriteDefaultProductRow(IRow row, ProductRow item)
    {
        // Cột theo layout file KiotViet / file data hiện tại của bạn
        // Có thể điều chỉnh sau nếu cần map động hơn
        row.CreateCell(2).SetCellValue(item.ProductCode);          // C
        row.CreateCell(3).SetCellValue(item.ProductName);          // D
        row.CreateCell(5).SetCellValue(item.ProductNameWithAttr);  // F
        row.CreateCell(7).SetCellValue(item.Quantity);             // H
        row.CreateCell(8).SetCellValue(item.Price);                // I
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

    private static IWorkbook OpenWorkbook(string filePath)
    {
        FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        string ext = Path.GetExtension(filePath).ToLower();

        if (ext == ".xls")
            return new HSSFWorkbook(fs);

        if (ext == ".xlsx")
            return new XSSFWorkbook(fs);

        throw new Exception("Định dạng Excel không hỗ trợ.");
    }

    private static string GetCellString(IRow row, int index)
    {
        return row.GetCell(index)?.ToString()?.Trim() ?? "";
    }

    private static double GetCellDouble(IRow row, int index)
    {
        ICell? cell = row.GetCell(index);
        if (cell == null) return 0;

        if (cell.CellType == CellType.Numeric)
            return cell.NumericCellValue;

        if (double.TryParse(cell.ToString(), out double value))
            return value;

        return 0;
    }
    #endregion
}