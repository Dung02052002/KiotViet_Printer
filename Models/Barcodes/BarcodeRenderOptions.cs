namespace KiotVietLabelPrinter.Models.Barcodes;

// Thông số vẽ tem mã vạch Code 128 cho tính năng "Tạo mã vạch" (CÔNG CỤ HÌNH
// ẢNH). Bố cục tem cố định từ trên xuống: TÊN HÀNG → VẠCH → MÃ HÀNG. Mọi kích
// thước tính theo mm để khớp với tem thật; service quy đổi sang pixel theo Dpi
// và làm tròn độ rộng vạch về số nguyên pixel (vạch lệch nửa pixel là nguyên
// nhân chính khiến máy quét đọc sai).
public class BarcodeRenderOptions
{
    // Độ rộng vạch hẹp nhất (X-dimension). 0.33 mm là cỡ chuẩn của tem nhãn
    // bán lẻ, máy quét laser/CCD thông thường đều đọc được.
    public double ModuleWidthMm { get; set; } = 0.33;

    // Chiều rộng phần vạch mong muốn. 0 = tự động theo ModuleWidthMm; > 0 thì
    // độ rộng vạch được tính lại để vừa chiều rộng này (làm tròn xuống pixel).
    public double TargetWidthMm { get; set; }

    public double BarHeightMm { get; set; } = 12;

    public string FontFamily { get; set; } = "Arial";

    // Cỡ chữ mã hàng (dòng dưới vạch).
    public float FontSizePt { get; set; } = 9;

    // Cỡ chữ tên hàng (dòng trên vạch). Tên dài tự xuống dòng theo chiều rộng
    // tem; tem có chiều cao cố định mà không đủ chỗ thì cỡ chữ được giảm dần
    // (không thấp hơn MinNameFontSizePt) — không bao giờ cắt bớt chữ.
    public float NameFontSizePt { get; set; } = 9;
    public const float MinNameFontSizePt = 5;

    // In đậm cả tên hàng và mã hàng.
    public bool Bold { get; set; } = true;

    // Khoảng cách từ đáy tên hàng tới đỉnh vạch.
    public double NameGapMm { get; set; } = 1;

    // Khoảng cách từ đáy vạch tới dòng chữ mã hàng.
    public double TextGapMm { get; set; } = 1;

    // Kích thước tem. 0 = tự động theo nội dung (vạch + lề); > 0 thì ảnh đúng
    // bằng kích thước này và nội dung được căn giữa / co lại cho vừa.
    public double LabelWidthMm { get; set; }
    public double LabelHeightMm { get; set; }

    // Lề trái/phải — phần vạch luôn được chừa tối thiểu 10 lần độ rộng vạch
    // (quiet zone bắt buộc của Code 128) dù người dùng nhập nhỏ hơn.
    public double MarginHorizontalMm { get; set; } = 4;
    public double MarginVerticalMm { get; set; } = 2;

    public int Dpi { get; set; } = 300;

    public BarcodeRenderOptions Clone() => (BarcodeRenderOptions)MemberwiseClone();
}
