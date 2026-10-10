namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>Phạm vi áp dụng font/cỡ chữ người dùng chọn lên phiếu.</summary>
public enum WarehouseFontApplyScope
{
    /// <summary>Không đổi font hay cỡ chữ của bất kỳ ô nào (mặc định).</summary>
    KeepTemplate,

    /// <summary>Chỉ các ô tên hàng ở các dòng hàng hóa.</summary>
    ProductNameOnly,

    /// <summary>Toàn bộ bảng hàng hóa: dòng tiêu đề bảng + các dòng hàng.</summary>
    ProductTable,

    /// <summary>Mọi ô trên sheet phiếu.</summary>
    WholeDocument
}

public sealed class WarehouseFontSettings
{
    public WarehouseFontApplyScope Scope { get; set; } = WarehouseFontApplyScope.KeepTemplate;
    public string FontFamily { get; set; } = "";
    public double FontSize { get; set; }

    public bool PreserveTemplateFormatting => Scope == WarehouseFontApplyScope.KeepTemplate;

    public const double MinFontSize = 6;
    public const double MaxFontSize = 36;

    public static WarehouseFontSettings KeepTemplate() => new();
}
