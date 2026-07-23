using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesBarTenderService
{
    private readonly BarTenderService _barTenderService = new();

    public void Print(
        LabelDefinition label,
        GlassesDocument document)
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
        // - GLASSES_TITLE / GLASSES_INFO_LEFT / GLASSES_INFO_RIGHT: tách
        //   sẵn các phần để template tem kính có thể dàn đúng bố cục mẫu.
        // - GLASSES_INFO: block gộp để tương thích các template cũ đang
        //   chỉ dùng 1 object text duy nhất.
        Dictionary<string, string> namedSubStrings = new()
{
    ["GLASSES_TITLE"] = document.GlassesTitle ?? string.Empty,
    ["GLASSES_INFO_LEFT"] = document.GlassesInfoLeft ?? string.Empty,
    ["GLASSES_INFO_RIGHT"] = document.GlassesInfoRight ?? string.Empty,
    // Backward compatibility: many templates still bind a single text object
    // to GLASSES_INFO instead of using LEFT/RIGHT split fields.
    ["GLASSES_INFO"] = document.GlassesInfo ?? string.Empty,

    ["MA_HANG"] = document.BaseCode ?? string.Empty,
    ["MA_VACH"] = document.Barcode ?? string.Empty
};

        _barTenderService.Print(
            label.TemplatePath,
            namedSubStrings);
    }
}