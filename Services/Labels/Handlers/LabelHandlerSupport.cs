using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Labels.Handlers;

/// <summary>
/// Phần dùng chung của các handler in qua file data (FULL / GENERIC / BARCODE).
/// </summary>
internal static class LabelHandlerSupport
{
    /// <summary>
    /// Thiếu file nguồn / data / template thì báo đúng file nào, thay vì lỗi
    /// khó hiểu từ NPOI/BarTender sau đó. <paramref name="dataFileRole"/> là tên
    /// file data hiển thị cho người dùng (vd "file data tem mã vạch").
    /// </summary>
    public static void EnsureFilesExist(LabelDefinition label, string dataFileRole)
    {
        if (string.IsNullOrWhiteSpace(label.SourceExcelFile) || !File.Exists(label.SourceExcelFile))
            throw new Exception($"Không tìm thấy file Excel nguồn:\n{label.SourceExcelFile}");

        if (string.IsNullOrWhiteSpace(label.DataFilePath) || !File.Exists(label.DataFilePath))
            throw new Exception($"Không tìm thấy {dataFileRole}:\n{label.DataFilePath}");

        if (string.IsNullOrWhiteSpace(label.TemplatePath) || !File.Exists(label.TemplatePath))
            throw new Exception($"Không tìm thấy file template BarTender:\n{label.TemplatePath}");
    }

    /// <summary>Xem trước tem in nguyên dữ liệu file nguồn: mã in = Mã hàng.</summary>
    public static List<PreviewRow> BuildPlainPreview(List<ProductRow> products)
    {
        return products
            .Select(item => new PreviewRow
            {
                ProductCode = item.ProductCode,
                ProductName = item.ProductName,
                ProductNameWithAttr = item.ProductNameWithAttr,
                ParsedBarcodeCode = item.ProductCode,
                FinalBarcodeCode = item.ProductCode,
                Quantity = item.Quantity,
                Price = item.Price,
                IsFullLabel = true,
                IsBarcodeLabel = false
            })
            .ToList();
    }
}
