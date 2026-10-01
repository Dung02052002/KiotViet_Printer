using NPOI.SS.UserModel;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ExcelService
{
    private const int ProductNameColumnIndex = 4;  // Cột E - Tên hàng
    private const int BarcodeColumnIndex = 5; // Cột F - cũng là "Tên hàng (thuộc tính)"
    private const int PriceColumnIndex = 8;   // Cột I - Giá bán
    private const int ProductCodeColumnIndex = 2; // Cột C - Mã hàng
    private const int QuantityColumnIndex = 7; // Cột H - Số lượng

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

            // Cùng 1 quy tắc với CopyToBarTenderData — Xem trước / số sản phẩm /
            // tổng tem / lịch sử phải khớp đúng với dữ liệu gửi cho BarTender.
            if (!IsProductRow(row))
                continue;

            string productCode = GetCellString(row, 2);         // C - Mã hàng
            string barcode = GetCellString(row, 3);             // D - Mã vạch
            string productName = GetCellString(row, 4);         // E - Tên hàng
            string productNameWithAttr = GetCellString(row, 5); // F - Tên hàng (thuộc tính)
            double price = GetCellDouble(row, 8);               // I - Giá bán
            string description = GetCellString(row, 9);         // J - Mô tả

            products.Add(new ProductRow
            {
                ProductCode = productCode,
                Barcode = barcode,
                ProductName = productName,
                ProductNameWithAttr = productNameWithAttr,
                Quantity = ReadQuantity(row),
                Price = price,
                Description = description
            });
        }

        return products;
    }

    /// <summary>
    /// Ghi file data tem FULL theo logic tool cũ:
    /// copy nguyên dữ liệu từ source sang target, không parse cột F.
    /// priceOverride (tuỳ chọn) cho phép ghi đè cột giá bán (I) theo chế độ đã
    /// chọn ở màn hình Tem đầy đủ, mà không đụng tới file Excel nguồn.
    /// nameOverrides (tuỳ chọn) cho phép ghi đè tên hàng (cột E/F) theo mã sản
    /// phẩm đã sửa ở modal "Sửa tên hàng" - cũng không đụng tới file Excel nguồn.
    /// </summary>
    public void WriteGenericLabelData(
        string sourceFile,
        string targetFile,
        PriceOverride? priceOverride = null,
        Dictionary<string, string>? nameOverrides = null)
    {
        CopyToBarTenderData(sourceFile, targetFile, false, "", priceOverride, nameOverrides);
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
        string employeeCode = "",
        PriceOverride? priceOverride = null,
        Dictionary<string, string>? nameOverrides = null)
    {
        using IWorkbook sourceWorkbook = OpenWorkbook(sourceFile);
        using IWorkbook targetWorkbook = OpenWorkbook(targetFile);

        ISheet sourceSheet = sourceWorkbook.GetSheetAt(0);
        ISheet targetSheet = targetWorkbook.GetSheetAt(0);

        // Xóa dữ liệu cũ, giữ header
        ClearSheetData(targetSheet);

        // Ghi các dòng sản phẩm LIỀN NHAU từ dòng 1 (không để lỗ hổng: dòng
        // trống ở giữa file data bị BarTender đọc thành tem trắng), và chỉ lấy
        // đúng những dòng ReadProducts coi là sản phẩm — để số dòng/số lượng
        // gửi cho BarTender khớp với Xem trước.
        int targetIndex = 1;

        for (int i = 1; i <= sourceSheet.LastRowNum; i++)
        {
            IRow? sourceRow = sourceSheet.GetRow(i);
            if (sourceRow == null || !IsProductRow(sourceRow))
                continue;

            IRow targetRow = targetSheet.CreateRow(targetIndex++);

            string productCode = GetCellString(sourceRow, ProductCodeColumnIndex);

            // Copy nguyên từng cột từ source sang target.
            for (int j = 0; j < sourceRow.LastCellNum; j++)
            {
                ICell? sourceCell = sourceRow.GetCell(j);
                if (sourceCell == null)
                    continue;

                CopyCellValue(sourceCell, targetRow.CreateCell(j));
            }

            // Các cột dưới đây ghi SAU khi copy và luôn ghi (kể cả khi dòng
            // nguồn ngắn, không có tới cột đó) — trước đây nằm trong vòng lặp
            // cột nên dòng thiếu cột cuối bị bỏ qua giá/tên đã sửa.

            // SỐ LƯỢNG: đúng con số Xem trước hiển thị (trống / <= 0 → 1).
            SetNumber(targetRow, QuantityColumnIndex, ReadQuantity(sourceRow));

            // TEM BARCODE: cột F = mã parse (fallback mã hàng cột C) + mã nhân
            // viên. Luôn ghi, kể cả khi cột F nguồn trống (trước đây ô trống bị
            // bỏ qua → tem in mã rỗng trong khi Xem trước hiện mã hàng).
            if (isBarcode)
            {
                string parsedCode = BarcodeParser.Parse(
                    GetCellString(sourceRow, BarcodeColumnIndex),
                    "",
                    GetCellString(sourceRow, ProductNameColumnIndex));

                if (string.IsNullOrWhiteSpace(parsedCode))
                    parsedCode = productCode;

                if (!string.IsNullOrWhiteSpace(employeeCode))
                    parsedCode = $"{parsedCode}-{employeeCode.Trim()}";

                SetText(targetRow, BarcodeColumnIndex, parsedCode);
            }

            // GIÁ BÁN: ghi đè theo chế độ đã chọn ở màn hình Tem đầy đủ (Sửa
            // tất cả cùng 1 giá / Sửa giá một vài sản phẩm). Không đụng tới
            // file Excel nguồn - chỉ áp dụng lên file data vừa ghi ra đây.
            if (priceOverride != null && priceOverride.TryResolve(productCode, out double overriddenPrice))
                SetNumber(targetRow, PriceColumnIndex, overriddenPrice);

            // TÊN HÀNG: ghi đè cột E/F theo tên đã sửa ở modal "Sửa tên hàng"
            // (Tem đầy đủ). Không áp dụng cho tem mã vạch (cột F ở đó dùng để
            // parse mã, không phải tên hiển thị). Không đụng tới file Excel
            // nguồn - chỉ áp dụng lên file data vừa ghi ra đây.
            if (!isBarcode &&
                nameOverrides != null && !string.IsNullOrWhiteSpace(productCode) &&
                nameOverrides.TryGetValue(productCode, out string? overriddenName) &&
                !string.IsNullOrWhiteSpace(overriddenName))
            {
                SetText(targetRow, ProductNameColumnIndex, overriddenName);
                SetText(targetRow, BarcodeColumnIndex, overriddenName);
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

        // Dựng nội dung trong bộ nhớ trước: FileMode.Create cắt file về rỗng
        // ngay khi mở, nếu Write() lỗi giữa chừng thì file data bị hỏng.
        byte[] content;

        using (MemoryStream buffer = new())
        {
            workbook.Write(buffer, leaveOpen: true);
            content = buffer.ToArray();
        }

        using FileStream output = new(targetFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        output.Write(content, 0, content.Length);
        output.Flush(true);
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

    /// <summary>
    /// Dòng có phải là một sản phẩm cần in không — quy tắc DUY NHẤT dùng cho
    /// cả ReadProducts (Xem trước, đếm, lịch sử) lẫn ghi file data BarTender.
    /// </summary>
    private static bool IsProductRow(IRow row)
    {
        return !string.IsNullOrWhiteSpace(GetCellString(row, ProductCodeColumnIndex)) ||
               !string.IsNullOrWhiteSpace(GetCellString(row, ProductNameColumnIndex)) ||
               !string.IsNullOrWhiteSpace(GetCellString(row, BarcodeColumnIndex));
    }

    /// <summary>
    /// Số lượng tem của dòng — quy tắc DUY NHẤT: trống / không phải số /
    /// &lt;= 0 → 1 tem.
    /// </summary>
    private static double ReadQuantity(IRow row)
    {
        double quantity = GetCellDouble(row, QuantityColumnIndex);
        return quantity <= 0 ? 1 : quantity;
    }

    private static void SetNumber(IRow row, int column, double value)
    {
        (row.GetCell(column) ?? row.CreateCell(column)).SetCellValue(value);
    }

    private static void SetText(IRow row, int column, string value)
    {
        (row.GetCell(column) ?? row.CreateCell(column)).SetCellValue(value);
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