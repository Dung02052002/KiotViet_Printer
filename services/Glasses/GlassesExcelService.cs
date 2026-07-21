using KiotVietLabelPrinter.Models;
using NPOI.SS.UserModel;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesExcelService
{
    public void WriteSingleProduct(
        ProductRow product,
        string dataFile)
    {
        if (!File.Exists(dataFile))
            throw new FileNotFoundException(dataFile);

        // Đọc toàn bộ file vào MemoryStream trước, KHÔNG dùng chung FileStream
        // để vừa đọc vừa ghi (NPOI vẫn cần đọc lazy các phần chưa parse của
        // workbook gốc khi Write(), nếu dùng chung stream mà đã SetLength(0)
        // trước đó sẽ gây lỗi "Cannot access a closed file").
        using MemoryStream memoryStream = new();

        using (FileStream readStream = new(
            dataFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite))
        {
            readStream.CopyTo(memoryStream);
        }

        memoryStream.Position = 0;

        // Tự nhận diện xls / xlsx theo nội dung thật của file
        // (file data tem kính thường là .xls định dạng cũ, không phải .xlsx)
        IWorkbook workbook = WorkbookFactory.Create(memoryStream);
        ISheet sheet = workbook.GetSheetAt(0);

        //=========================================
        // Xóa dữ liệu cũ (giữ header)
        //=========================================

        for (int i = sheet.LastRowNum; i >= 1; i--)
        {
            IRow? oldRow  = sheet.GetRow(i);

            if (oldRow  != null)
                sheet.RemoveRow(oldRow );
        }

        //=========================================
        // Tạo dòng mới
        //=========================================

        IRow row = sheet.CreateRow(1);

        // Copy style từ header nếu có
        IRow? header = sheet.GetRow(0);

        if (header != null)
        {
            for (int i = 0; i < header.LastCellNum; i++)
            {
                ICell newCell = row.CreateCell(i);

                ICell? headerCell = header.GetCell(i);

                if (headerCell != null)
                    newCell.CellStyle = headerCell.CellStyle;
            }
        }

        //=========================================
        // Ghi dữ liệu
        //=========================================

        SetCell(row, 2, product.ProductCode);
        SetCell(row, 3, product.Barcode);
        SetCell(row, 4, product.ProductName);
        SetCell(row, 5, product.ProductNameWithAttr);
        SetCell(row, 7, product.Quantity);
        SetCell(row, 8, product.Price);

        //=========================================
        // Save
        //=========================================

        using (FileStream writeStream = new(
            dataFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite))
        {
            workbook.Write(writeStream);
        }

        workbook.Close();
    }

    private static void SetCell(
        IRow row,
        int column,
        object? value)
    {
        ICell cell = row.GetCell(column)
            ?? row.CreateCell(column);

        switch (value)
        {
            case null:
                cell.SetCellValue("");
                break;

            case double d:
                cell.SetCellValue(d);
                break;

            case int i:
                cell.SetCellValue(i);
                break;

            default:
                cell.SetCellValue(value.ToString());
                break;
        }
    }
}