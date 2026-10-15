using KiotVietLabelPrinter.Models.Labels;

namespace KiotVietLabelPrinter.Models.Glasses;

/// <summary>
/// Dữ liệu một tem kính đã dựng sẵn: mã đã parse, mã vạch (mã + mã màu) và
/// các khối chữ gửi cho BarTender qua Named Sub-String.
/// </summary>
public class GlassesDocument
{
    public ProductRow Product { get; set; } = null!;

    public string BaseCode { get; set; } = "";

    public string Barcode { get; set; } = "";

    public string AttributeText { get; set; } = "";

    public string GlassesInfo { get; set; } = "";

    public string GlassesTitle { get; set; } = "";

    public string GlassesInfoLeft { get; set; } = "";

    public string GlassesInfoRight { get; set; } = "";
}