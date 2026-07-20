using KiotVietLabelPrinter.Services.Glasses.Logging;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
    public static List<GlassesToken> Scan(string? text)
    {
        GlassesDebug.Title("LEXER");

        List<GlassesToken> tokens = [];

        if (string.IsNullOrWhiteSpace(text))
            return tokens;

        //-------------------------------------------------
        // Normalize
        //-------------------------------------------------

        text = Normalize(text);

        GlassesDebug.Info($"INPUT : {text}");

        //-------------------------------------------------
        // Scan
        //-------------------------------------------------

        ScanCore(text, tokens);

        //-------------------------------------------------
        // Detect Type
        //-------------------------------------------------

        foreach (GlassesToken token in tokens)
        {
            token.Type = DetectType(token.Text);

            GlassesDebug.Info(
                $"{token.Index,-2} {token.Type,-10} {token.Text}");
        }

        //-------------------------------------------------
        // Index
        //-------------------------------------------------

        for (int i = 0; i < tokens.Count; i++)
        {
            tokens[i].Index = i;
        }

        GlassesDebug.Success(
            $"Token Count : {tokens.Count}");

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