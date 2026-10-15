using KiotVietLabelPrinter.Services.Glasses.Debug;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
    /// <summary>
    /// Tách tên hàng thành token đã phân loại (Code / Mk / K / Pcn / Color...).
    /// Start/End của token là vị trí trong chuỗi SAU khi Normalize (bỏ khoảng
    /// trắng hai đầu, xuống dòng/tab thành dấu cách).
    /// </summary>
    public static List<GlassesToken> Scan(string? text)
    {
        List<GlassesToken> tokens = [];

        if (string.IsNullOrWhiteSpace(text))
            return tokens;

        text = Normalize(text);

        // ScanCore() gán đúng Type cuối cùng cho mọi token lúc tạo (Flush()
        // gọi DetectType() cho token chữ/số, separator gán thẳng type).
        ScanCore(text, tokens);

        for (int i = 0; i < tokens.Count; i++)
            tokens[i].Index = i;

        // Chặn trước khi dựng chuỗi log: Scan chạy cho mọi dòng sản phẩm.
        if (GlassesDebug.Enabled)
        {
            GlassesDebug.Title("LEXER");
            GlassesDebug.Info($"INPUT : {text}");

            foreach (GlassesToken token in tokens)
                GlassesDebug.Info($"{token.Index,-2} {token.Type,-10} {token.Text}");

            GlassesDebug.Success($"Token Count : {tokens.Count}");
        }

        return tokens;
    }

    //-------------------------------------------------

    private static string Normalize(string text)
    {
        return text
            .Trim()
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ");
    }
}
