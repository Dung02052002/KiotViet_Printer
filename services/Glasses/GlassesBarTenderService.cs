using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesBarTenderService
{
    private readonly BarTenderService _barTenderService = new();

    public void Print(
        LabelDefinition label,
        string maHang,
        string maVach)
    {
        if (string.IsNullOrWhiteSpace(label.TemplatePath))
            throw new Exception("Chưa cấu hình file BarTender.");

        if (!File.Exists(label.TemplatePath))
            throw new Exception(
                $"Không tìm thấy file:\n{label.TemplatePath}");

        // CHỈ set 2 mã đã parse (MA_HANG / MA_VACH) — đây là 2 Named
        // Sub-String NHỎ chèn ngay tại vị trí số trong khối text tĩnh
        // (KÍNH MÁT / Nhập từ / Đ/c / Thông số kỹ thuật...), KHÔNG ghi đè
        // nguyên khối GLASSES_INFO nữa để giữ nguyên toàn bộ thông tin có
        // sẵn trong template BarTender.
        Dictionary<string, string> namedSubStrings = new()
        {
            ["MA_HANG"] = maHang ?? "",
            ["MA_VACH"] = maVach ?? ""
        };

        _barTenderService.Print(
            label.TemplatePath,
            namedSubStrings);
    }
}