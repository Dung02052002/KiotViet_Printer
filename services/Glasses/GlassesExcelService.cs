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
        //
        // Khi in liên tiếp nhiều mã (mỗi mã ghi đè + gọi BarTender 1 lần),
        // BarTender có thể vẫn còn đang mở/đọc file data của lượt in trước
        // trong vài trăm ms → dùng OpenWithRetry để không bị lỗi "file đang
        // được sử dụng bởi tiến trình khác" làm gãy cả loạt in.
        using MemoryStream memoryStream = new();

        using (FileStream readStream = OpenWithRetry(
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

        using (FileStream writeStream = OpenWithRetry(
            dataFile,
            FileMode.Create,
            FileAccess.Write,
            FileShare.ReadWrite))
        {
            workbook.Write(writeStream);
        }

        workbook.Close();
    }

    /// <summary>
    /// Mở file với retry/backoff — dùng khi in nhiều mã liên tiếp, tránh
    /// lỗi "file đang được BarTender sử dụng" do tiến trình in trước chưa
    /// kịp giải phóng file data.
    /// </summary>
    private static FileStream OpenWithRetry(
        string path,
        FileMode mode,
        FileAccess access,
        FileShare share,
        int maxAttempts = 10,
        int delayMs = 250)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return new FileStream(path, mode, access, share);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
        }

        // Lần thử cuối cùng: để lỗi thật ném ra nếu vẫn thất bại
        return new FileStream(path, mode, access, share);
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