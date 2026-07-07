using NPOI.SS.UserModel;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ExcelService
{
    private const int BarcodeColumnIndex = 5; // Cột F

    #region PUBLIC API CHO PROJECT MỚI

    public List<ProductRow> ReadProducts(string sourceFile)
    {
        using IWorkbook workbook = OpenWorkbook(sourceFile);
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

        return products;
    }

    /// <summary>
    /// Ghi file data tem FULL theo logic tool cũ:
    /// copy nguyên dữ liệu từ source sang target, không parse cột F
    /// </summary>
    public void WriteGenericLabelData(string sourceFile, string targetFile)
    {
        CopyToBarTenderData(sourceFile, targetFile, false, "");
    }

    /// <summary>
    /// Ghi file data tem BARCODE theo logic tool cũ:
    /// copy nguyên dữ liệu từ source sang target,
    /// riêng cột F parse mã + nối mã nhân viên
    /// </summary>
    public void WriteBarcodeLikeData(string sourceFile, string targetFile, string employeeCode)
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
        using IWorkbook sourceWorkbook = OpenWorkbook(sourceFile);
        using IWorkbook targetWorkbook = OpenWorkbook(targetFile);

        ISheet sourceSheet = sourceWorkbook.GetSheetAt(0);
        ISheet targetSheet = targetWorkbook.GetSheetAt(0);

        // Xóa dữ liệu cũ, giữ header
        ClearSheetData(targetSheet);

        // Copy nguyên từng hàng/cột từ source sang target
        for (int i = 1; i <= sourceSheet.LastRowNum; i++)
        {
            IRow? sourceRow = sourceSheet.GetRow(i);
            if (sourceRow == null)
                continue;

            // bỏ qua dòng hoàn toàn rỗng
            if (IsRowEmpty(sourceRow))
                continue;

            IRow targetRow = targetSheet.GetRow(i) ?? targetSheet.CreateRow(i);

            for (int j = 0; j < sourceRow.LastCellNum; j++)
            {
                ICell? sourceCell = sourceRow.GetCell(j);
                if (sourceCell == null)
                    continue;

                ICell targetCell = targetRow.GetCell(j) ?? targetRow.CreateCell(j);

                string value = GetCellText(sourceCell);

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
                    targetCell.SetCellValue(value);
                    continue;
                }

                CopyCellValue(sourceCell, targetCell);
            }
        }

        SaveWorkbook(targetWorkbook, targetFile);
    }

    #endregion

    #region HELPERS

    private static IWorkbook OpenWorkbook(string filePath)
    {
        if (!File.Exists(filePath))
            throw new Exception($"Không tìm thấy file Excel:\n{filePath}");

        FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        try
        {
            // Tự nhận diện xls / xlsx theo nội dung thật của file
            return WorkbookFactory.Create(fs);
        }
        catch (Exception ex)
        {
            fs.Dispose();
            throw new Exception(
                $"Không đọc được file Excel.\n" +
                $"File: {Path.GetFileName(filePath)}\n" +
                $"Đường dẫn: {filePath}\n" +
                $"Chi tiết: {ex.Message}");
        }
    }

    private static void SaveWorkbook(IWorkbook workbook, string targetFile)
    {
        string? folder = Path.GetDirectoryName(targetFile);
        if (!string.IsNullOrWhiteSpace(folder) && !Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        using FileStream output = new(targetFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        workbook.Write(output);
    }

    private static void ClearSheetData(ISheet sheet)
    {
        for (int i = sheet.LastRowNum; i >= 1; i--)
        {
            IRow? row = sheet.GetRow(i);
            if (row != null)
                sheet.RemoveRow(row);
        }
    }

    private static bool IsRowEmpty(IRow row)
    {
        for (int i = row.FirstCellNum; i < row.LastCellNum; i++)
        {
            if (i < 0) continue;

            ICell? cell = row.GetCell(i);
            if (cell != null && !string.IsNullOrWhiteSpace(GetCellText(cell)))
                return false;
        }

        return true;
    }

    private static void CopyCellValue(ICell sourceCell, ICell targetCell)
    {
        switch (sourceCell.CellType)
        {
            case CellType.Numeric:
                targetCell.SetCellValue(sourceCell.NumericCellValue);
                break;

            case CellType.Boolean:
                targetCell.SetCellValue(sourceCell.BooleanCellValue);
                break;

            case CellType.Formula:
                // Với file data BarTender, giữ nguyên kết quả text an toàn hơn
                targetCell.SetCellValue(GetCellText(sourceCell));
                break;

            case CellType.Blank:
                targetCell.SetCellValue(string.Empty);
                break;

            default:
                targetCell.SetCellValue(GetCellText(sourceCell));
                break;
        }
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

    private static string GetCellText(ICell cell)
    {
        return cell.ToString()?.Trim() ?? "";
    }

    #endregion
}