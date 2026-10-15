using Newtonsoft.Json;

namespace KiotVietLabelPrinter.Models;

public class LabelDefinition
{
    // File Excel nguồn của MỘT lần xem trước/in — chỉ gán trên bản sao từ
    // ForSource(), không thuộc cấu hình nên không lưu vào config.json.
    [JsonIgnore]
    public string SourceExcelFile { get; set; } = "";
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string IconText { get; set; } = "";

    public bool IsEnabled { get; set; } = true;

    public string TemplatePath { get; set; } = "";
    public string DataFilePath { get; set; } = "";

    // Tên handler xử lý tem này
    // Ví dụ: FULL, BARCODE, GENERIC
    // Luôn chuẩn hoá về chữ HOA: factory tra không phân biệt hoa/thường, còn
    // giao diện so sánh == "FULL"... — config sửa tay ghi "full" vẫn in được
    // nhưng mất ô sửa giá/tên nếu không chuẩn hoá.
    private string _handlerType = "GENERIC";
    public string HandlerType
    {
        get => _handlerType;
        set => _handlerType = (value ?? "").Trim().ToUpperInvariant();
    }

    // Có cần nhập mã nhân viên không
    public bool RequiresEmployeeCode { get; set; }

    // Có parse mã từ "Tên hàng (thuộc tính)" hay không
    public bool UseBarcodeParser { get; set; }

    // Có nối mã nhân viên vào cuối mã hay không
    public bool AppendEmployeeCode { get; set; }

    // Cột đích trong file data để ghi mã đã parse
    // ví dụ cột F = 5
    public int TargetNameColumnIndex { get; set; } = 5;

    /// <summary>
    /// Bản sao dùng riêng cho một lần xem trước/in với file nguồn
    /// <paramref name="sourceExcelFile"/>. Đối tượng gốc nằm trong cấu hình
    /// dùng chung (ConfigService) nên không được sửa từ luồng in.
    /// </summary>
    public LabelDefinition ForSource(string sourceExcelFile)
    {
        LabelDefinition copy = (LabelDefinition)MemberwiseClone();
        copy.SourceExcelFile = sourceExcelFile;
        return copy;
    }
}