using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace KiotVietLabelPrinter.Services;

public class ExcelService
{
    private const int BarcodeColumnIndex = 5; // Cột F trong file Excel

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

    /// <summary>
    /// Copy dữ liệu từ file nguồn sang file data BarTender.
    /// Logic giữ nguyên như tool cũ:
    /// - Copy toàn bộ ô theo đúng vị trí cột.
    /// - Nếu là tem mã vạch thì chỉ xử lý riêng cột F:
    ///     + Parse mã barcode từ tên hàng
    ///     + Nối mã nhân viên nếu có
    /// </summary>
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

        // Xóa dữ liệu cũ ở file target nhưng giữ lại header dòng 0
        for (int i = targetSheet.LastRowNum; i >= 1; i--)
        {
            IRow? oldRow = targetSheet.GetRow(i);
            if (oldRow != null)
                targetSheet.RemoveRow(oldRow);
        }

        // Copy dữ liệu từ source sang target
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

                // ===== GIÁ TRỊ GỐC CỦA Ô =====
                string stringValue = sourceCell.ToString() ?? "";

                // ===== TEM MÃ VẠCH: CHỈ XỬ LÝ CỘT F =====
                if (isBarcode && j == BarcodeColumnIndex)
                {
                    string parsedCode = BarcodeParser.Parse(stringValue);

                    // Nếu parser không ra gì thì fallback về mã hàng cột C
                    if (string.IsNullOrWhiteSpace(parsedCode))
                    {
                        parsedCode = sourceRow.GetCell(2)?.ToString()?.Trim() ?? "";
                    }

                    // Nếu có mã nhân viên thì nối vào đuôi
                    if (!string.IsNullOrWhiteSpace(employeeCode))
                    {
                        parsedCode = $"{parsedCode}-{employeeCode.Trim()}";
                    }

                    stringValue = parsedCode;
                }

                // ===== GHI DỮ LIỆU =====
                switch (sourceCell.CellType)
                {
                    case CellType.Numeric:
                        // Riêng barcode cột F thì luôn ghi string
                        if (isBarcode && j == BarcodeColumnIndex)
                            targetCell.SetCellValue(stringValue);
                        else
                            targetCell.SetCellValue(sourceCell.NumericCellValue);
                        break;

                    case CellType.Boolean:
                        // Barcode cột F vẫn ghi string
                        if (isBarcode && j == BarcodeColumnIndex)
                            targetCell.SetCellValue(stringValue);
                        else
                            targetCell.SetCellValue(sourceCell.BooleanCellValue);
                        break;

                    case CellType.Formula:
                        // Barcode cột F ưu tiên ghi text đã parse
                        if (isBarcode && j == BarcodeColumnIndex)
                            targetCell.SetCellValue(stringValue);
                        else
                            targetCell.SetCellFormula(sourceCell.CellFormula);
                        break;

                    default:
                        targetCell.SetCellValue(stringValue);
                        break;
                }
            }
        }

        using FileStream output = new(targetFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        targetWorkbook.Write(output);

        targetWorkbook.Close();
        sourceWorkbook.Close();
    }
}