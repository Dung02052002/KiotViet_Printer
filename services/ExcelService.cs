using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ExcelService
{
    private const int BarcodeColumnIndex = 5; // Cột F

    #region PUBLIC API CHO PROJECT MỚI

    public List<ProductRow> ReadProducts(string sourceFile)
    {
        IWorkbook workbook = OpenWorkbook(sourceFile);
        ISheet sheet = workbook.GetSheetAt(0);

        List<ProductRow> products = new();

        for (int i = 1; i <= sheet.LastRowNum; i++)
        {
            IRow? row = sheet.GetRow(i);
            if (row == null)
                continue;

            string productCode = GetCellString(row, 2);         // C - Mã hàng
            string barcode = GetCellString(row, 3);             // D - Mã vạch
            string productName = GetCellString(row, 4);         // E - Tên hàng
            string productNameWithAttr = GetCellString(row, 5); // F - Tên hàng (thuộc tính)
            double quantity = GetCellDouble(row, 7);            // H - Số lượng
            double price = GetCellDouble(row, 8);               // I - Giá bán

            if (string.IsNullOrWhiteSpace(productCode) &&
                string.IsNullOrWhiteSpace(productName) &&
                string.IsNullOrWhiteSpace(productNameWithAttr))
            {
                continue;
            }

            products.Add(new ProductRow
            {
                ProductCode = productCode,
                Barcode = barcode,
                ProductName = productName,
                ProductNameWithAttr = productNameWithAttr,
                Quantity = quantity <= 0 ? 1 : quantity,
                Price = price
            });
        }

        workbook.Close();
        return products;
    }

    /// <summary>
    /// Ghi file data tem FULL theo logic tool cũ:
    /// copy nguyên dữ liệu từ source sang target, không parse cột F
    /// </summary>
    public void WriteGenericLabelData(
        string sourceFile,
        string targetFile)
    {
        CopyToBarTenderData(sourceFile, targetFile, false, "");
    }

    /// <summary>
    /// Ghi file data tem BARCODE theo logic tool cũ:
    /// copy nguyên dữ liệu từ source sang target,
    /// riêng cột F parse mã + nối mã nhân viên
    /// </summary>
    public void WriteBarcodeLikeData(
        string sourceFile,
        string targetFile,
        string employeeCode)
    {
        CopyToBarTenderData(sourceFile, targetFile, true, employeeCode);
    }

    #endregion

    #region CORE LOGIC - GIỮ THEO TOOL CŨ

    public void CopyToBarTenderData(
        string sourceFile,
        string targetFile,
        bool isBarcode,
        string employeeCode = "")
    {
        IWorkbook sourceWorkbook = OpenWorkbook(sourceFile);
        IWorkbook targetWorkbook = OpenWorkbook(targetFile);

        ISheet sourceSheet = sourceWorkbook.GetSheetAt(0);
        ISheet targetSheet = targetWorkbook.GetSheetAt(0);

        // Xóa dữ liệu cũ, giữ header
        for (int i = targetSheet.LastRowNum; i >= 1; i--)
        {
            IRow? oldRow = targetSheet.GetRow(i);
            if (oldRow != null)
                targetSheet.RemoveRow(oldRow);
        }

        // Copy nguyên từng hàng/cột từ source sang target
        for (int i = 1; i <= sourceSheet.LastRowNum; i++)
        {
            IRow? sourceRow = sourceSheet.GetRow(i);
            if (sourceRow == null)
                continue;

            IRow targetRow = targetSheet.GetRow(i) ?? targetSheet.CreateRow(i);

            for (int j = 0; j < sourceRow.LastCellNum; j++)
            {
                ICell? sourceCell = sourceRow.GetCell(j);
                if (sourceCell == null)
                    continue;

                ICell targetCell = targetRow.GetCell(j) ?? targetRow.CreateCell(j);

                string value = sourceCell.ToString() ?? "";

                // TEM BARCODE: chỉ xử lý riêng cột F
                if (isBarcode && j == BarcodeColumnIndex)
                {
                    string parsedCode = BarcodeParser.Parse(value);

                    if (string.IsNullOrWhiteSpace(parsedCode))
                    {
                        // fallback về mã hàng cột C
                        parsedCode = sourceRow.GetCell(2)?.ToString()?.Trim() ?? "";
                    }

                    if (!string.IsNullOrWhiteSpace(employeeCode))
                    {
                        parsedCode = $"{parsedCode}-{employeeCode.Trim()}";
                    }

                    value = parsedCode;
                }

                switch (sourceCell.CellType)
                {
                    case CellType.Numeric:
                        if (isBarcode && j == BarcodeColumnIndex)
                            targetCell.SetCellValue(value);
                        else
                            targetCell.SetCellValue(sourceCell.NumericCellValue);
                        break;

                    case CellType.Boolean:
                        if (isBarcode && j == BarcodeColumnIndex)
                            targetCell.SetCellValue(value);
                        else
                            targetCell.SetCellValue(sourceCell.BooleanCellValue);
                        break;

                    case CellType.Formula:
                        if (isBarcode && j == BarcodeColumnIndex)
                            targetCell.SetCellValue(value);
                        else
                            targetCell.SetCellFormula(sourceCell.CellFormula);
                        break;

                    default:
                        targetCell.SetCellValue(value);
                        break;
                }
            }
        }

        using FileStream output = new(targetFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        targetWorkbook.Write(output);

        targetWorkbook.Close();
        sourceWorkbook.Close();
    }

    #endregion

    #region HELPERS

    private IWorkbook OpenWorkbook(string filePath)
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