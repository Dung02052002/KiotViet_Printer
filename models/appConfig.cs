namespace KiotVietLabelPrinter.Models;

public class AppConfig
{
    /// <summary>
    /// Đường dẫn bartend.exe
    /// </summary>
    public string BarTenderExe { get; set; } = "";

    /// <summary>
    /// Tem đầy đủ
    /// </summary>
    public LabelConfig FullLabel { get; set; } = new();

    /// <summary>
    /// Tem mã vạch
    /// </summary>
    public LabelConfig BarcodeLabel { get; set; } = new();

    /// <summary>
    /// Thư mục Excel lần cuối
    /// </summary>
    public string LastFolder { get; set; } = "";

    public bool AutoOpenLastFolder { get; set; } = true;

    /// <summary>
    /// Nhớ mã nhân viên
    /// </summary>
    public bool RememberEmployee { get; set; } = true;

    public string DefaultEmployee { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Company { get; set; } = "Dũng Store";
}