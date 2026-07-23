using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesPrintService
{
    private static readonly SemaphoreSlim PrintSequenceGate = new(1, 1);

    private readonly GlassesExcelService _excelService = new();
    private readonly GlassesBarTenderService  _barTenderService = new();

    public void Print(
        List<ProductRow> products,
        LabelDefinition label,
        string colorCode)
    {
        if (products == null || products.Count == 0)
            throw new Exception("Không có sản phẩm để in.");

        if (string.IsNullOrWhiteSpace(label.DataFilePath))
            throw new Exception("Chưa cấu hình file Data của tem kính.");

        if (string.IsNullOrWhiteSpace(label.TemplatePath))
            throw new Exception("Chưa cấu hình file BarTender.");

        if (!File.Exists(label.DataFilePath))
            throw new Exception($"Không tìm thấy file:\n{label.DataFilePath}");

        if (!File.Exists(label.TemplatePath))
            throw new Exception($"Không tìm thấy file:\n{label.TemplatePath}");

        //============================
        // 1 sản phẩm
        //============================

        if (products.Count == 1)
        {
            PrintSingle(
                products[0],
                label,
                colorCode);

            return;
        }

        //============================
        // nhiều sản phẩm
        //============================

        foreach (ProductRow product in products)
        {
            PrintSingle(
                product,
                label,
                colorCode);
        }
    }

    private void PrintSingle(
    ProductRow product,
    LabelDefinition label,
    string colorCode)
{
    PrintSequenceGate.Wait();

    try
    {
    GlassesDocument document =
        GlassesDocumentBuilder.Build(
            product,
            colorCode);

    // File data (.xls) GIỮ NGUYÊN dữ liệu gốc từ Excel import — KHÔNG
    // ghi đè Mã hàng / Mã vạch bằng mã đã parse. Mã đã parse
    // (BaseCode/BarcodeCode) chỉ được dùng để encode hình mã vạch và
    // gửi qua GLASSES_INFO cho BarTender, không đụng vào cột dữ liệu
    // gốc trong file data.

    // KHÔNG đụng vào ProductNameWithAttr (cột F) — đây là text thuộc
    // tính hiển thị riêng bên phải tem VÀ cũng là nguồn để parser cắt
    // mã, ghi đè nó sẽ vừa phá parse vừa phá nội dung hiển thị đó.
    //
    // File data (.xls) GIỮ NGUYÊN 100% — không ghi thêm cột nào khác,
    // không đụng Description. Khối GLASSES_INFO (KÍNH MÁT / Mã hàng /
    // Nhập từ / Đ/c / Thông số kỹ thuật / Mã vạch...) được gửi thẳng
    // qua Print XML Script dưới dạng Named Sub-String "GLASSES_INFO"
    // (đã quét parser và thay sẵn 2 dòng "Mã hàng"/"Mã vạch" bằng mã
    // ĐÃ PARSE) — không cần ghi ra file data nữa.
    try
    {
        _excelService.WriteSingleProduct(
            document.Product,
            label.DataFilePath);
    }
    catch (IOException)
    {
        // Khi file data đang bị app khác giữ lock, vẫn cho phép in bằng
        // dữ liệu Named Sub-String để không làm gián đoạn thao tác in.
    }

    _barTenderService.Print(
        label,
        document);
    }
    finally
    {
        PrintSequenceGate.Release();
    }
}
}