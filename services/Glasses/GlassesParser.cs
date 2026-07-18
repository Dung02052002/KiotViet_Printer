using KiotVietLabelPrinter.Models;


namespace KiotVietLabelPrinter.Services.Glasses;

/// <summary>
/// Adapter cho Parser V3.
/// Giữ lại API cũ để không phải sửa toàn bộ project.
/// </summary>
public static class GlassesParser
{
    private static readonly GlassesParserEngine _engine = new();

    //---------------------------------------------------------
    // Product
    //---------------------------------------------------------

    public static string Parse(ProductRow product)
    {
        GlassesParserResult result =
            _engine.Parse(product);

        return result.BaseCode;
    }

    //---------------------------------------------------------
    // Text
    //---------------------------------------------------------

    public static string Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        GlassesParserResult result =
            _engine.Parse(text);

        return result.BaseCode;
    }

    //---------------------------------------------------------
    // Parser đầy đủ
    //---------------------------------------------------------

    public static GlassesParserResult ParseFull(ProductRow product)
    {
        return _engine.Parse(product);
    }

    public static GlassesParserResult ParseFull(string text)
    {
        return _engine.Parse(text);
    }
}