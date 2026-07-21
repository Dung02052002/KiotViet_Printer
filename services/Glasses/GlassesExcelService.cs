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

        using FileStream stream = new(
            dataFile,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        // Tự nhận diện xls / xlsx theo nội dung thật của file
        // (file data tem kính thường là .xls định dạng cũ, không phải .xlsx)
        IWorkbook workbook = WorkbookFactory.Create(stream);
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

        stream.SetLength(0);
        stream.Position = 0;

        workbook.Write(stream);

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