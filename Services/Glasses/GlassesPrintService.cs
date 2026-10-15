using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Models.Labels;
using KiotVietLabelPrinter.Services.Labels;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesPrintService
{
    private static readonly SemaphoreSlim PrintSequenceGate = new(1, 1);

    private readonly GlassesExcelService _excelService = new();
    private readonly GlassesBarTenderService _barTenderService = new();

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

        // Tem kính LUÔN cần NamedSubString (tiêu đề/thông tin đã format
        // động) qua /XMLScript= — không có cách nào thay thế bằng command
        // line chuẩn. Nếu máy đã xác nhận không hỗ trợ, báo lỗi rõ ràng 1
        // lần duy nhất ngay từ đầu thay vì để từng mã trong danh sách lần
        // lượt fail với cùng 1 lỗi lặp lại.
        BarTenderCapabilityService.Instance.EnsureProbed(
            ConfigService.Instance.Config.BarTenderExe);

        if (BarTenderCapabilityService.Instance.XmlScriptSupported == false)
            throw BarTenderCapabilityService.BuildEnterpriseRequiredException();

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

        // KHÔNG được để 1 mã lỗi giữa chừng (kẹt máy in, file data đang
        // khoá quá lâu, BarTender báo lỗi tạm thời...) làm dừng luôn cả
        // những mã còn lại phía sau — đó chính là nguyên nhân "in thiếu
        // mã" khi in nhiều mã, mỗi mã số lượng lớn. Vẫn in tiếp các mã còn
        // lại rồi báo cáo đầy đủ mã nào bị lỗi để người dùng in bù.
        List<string> failedProducts = new();
        List<ProductRow> printedProducts = new();

        foreach (ProductRow product in products)
        {
            try
            {
                PrintSingle(
                    product,
                    label,
                    colorCode);

                printedProducts.Add(product);
            }
            catch (Exception ex)
            {
                failedProducts.Add($"{product.ProductCode}: {ex.Message}");
            }
        }

        if (failedProducts.Count > 0)
        {
            string message =
                $"In thiếu {failedProducts.Count}/{products.Count} mã do lỗi:\n\n" +
                string.Join("\n", failedProducts);

            // Còn mã đã in được → báo để LabelService ghi lịch sử cho chúng.
            if (printedProducts.Count > 0)
                throw new PartialPrintException(message, printedProducts);

            throw new Exception(message);
        }
    }

    // Ghi file data + gọi BarTender cho MỘT mã là một khối không được xen
    // ngang. LabelService.PrintLock đã chặn hai lệnh in chồng nhau; cổng này
    // giữ đúng bất biến đó cả khi service được gọi từ nơi khác.
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

            // File data chỉ nhận dữ liệu GỐC của mã này từ Excel import (mã
            // hàng, mã vạch, tên, thuộc tính, số lượng, giá) — không ghi mã đã
            // parse vào, và không đụng cột F (Tên hàng thuộc tính: vừa là chữ
            // hiển thị trên tem vừa là nguồn để parser cắt mã). Mã đã parse,
            // mã vạch và khối thông tin kính đi riêng qua Named Sub-String
            // (GlassesBarTenderService).
            //
            // KHÔNG được in tiếp khi ghi file data thất bại: file data lúc đó vẫn
            // chứa số lượng / tên / giá của MÃ TRƯỚC, BarTender sẽ in tem lẫn dữ
            // liệu (barcode mã này + số lượng/tên mã khác) mà không báo lỗi gì.
            // Để lỗi ném ra → vòng in nhiều mã đưa mã này vào danh sách "in thiếu".
            try
            {
                _excelService.WriteSingleProduct(
                    document.Product,
                    label.DataFilePath);
            }
            catch (IOException ex)
            {
                throw new Exception(
                    "Không ghi được file data tem kính (file đang bị chương trình " +
                    "khác mở/khoá) — đã bỏ qua mã này để tránh in sai dữ liệu.\n" +
                    $"File: {label.DataFilePath}\nChi tiết: {ex.Message}",
                    ex);
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
