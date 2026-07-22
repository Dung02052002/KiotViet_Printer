using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesBarTenderService
{
    private readonly BarTenderService _barTenderService = new();

    public void Print(
        LabelDefinition label,
        string maHang,
        string maVach,
        string glassesInfo)
    {
        if (string.IsNullOrWhiteSpace(label.TemplatePath))
            throw new Exception("Chưa cấu hình file BarTender.");

        if (!File.Exists(label.TemplatePath))
            throw new Exception(
                $"Không tìm thấy file:\n{label.TemplatePath}");

        // Gửi qua Print XML Script bằng Named Sub-String — KHÔNG đụng gì
        // tới file data (.xls), file data giữ nguyên 100% dữ liệu gốc.
        //
        // - MA_HANG / MA_VACH: mã đã parse, dùng cho các object nhỏ (nếu
        //   template có object riêng lẻ đang trỏ 2 tên này).
        // - GLASSES_INFO: nguyên khối text tĩnh (KÍNH MÁT / Mã hàng /
        //   Nhập từ / Đ/c / Thông số kỹ thuật / Mã vạch...) đã được
        //   GlassesInfoBuilder quét parser và thay sẵn 2 dòng "Mã hàng"/
        //   "Mã vạch" bằng mã ĐÃ PARSE — bên BarTender chỉ cần đổi Type
        //   của object GLASSES_INFO thành "Named Sub-String", Name =
        //   GLASSES_INFO là nhận đúng toàn bộ nội dung này, không cần
        //   tách nhỏ nhiều Data Source nữa.
        Dictionary<string, string> namedSubStrings = new()
        {
            ["MA_HANG"] = maHang ?? "",
            ["MA_VACH"] = maVach ?? "",
            ["GLASSES_INFO"] = glassesInfo ?? ""
        };

        _barTenderService.Print(
            label.TemplatePath,
            namedSubStrings);
    }
}