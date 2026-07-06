namespace KiotVietLabelPrinter.Models;

public class LabelDefinition
{
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
    public string HandlerType { get; set; } = "GENERIC";

    // Có cần nhập mã nhân viên không
    public bool RequiresEmployeeCode { get; set; }

    // Có parse mã từ "Tên hàng (thuộc tính)" hay không
    public bool UseBarcodeParser { get; set; }

    // Có nối mã nhân viên vào cuối mã hay không
    public bool AppendEmployeeCode { get; set; }

    // Cột đích trong file data để ghi mã đã parse
    // ví dụ cột F = 5
    public int TargetNameColumnIndex { get; set; } = 5;
}