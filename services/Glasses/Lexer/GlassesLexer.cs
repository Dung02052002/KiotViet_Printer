using KiotVietLabelPrinter.Services.Glasses.Dictionaries;

using KiotVietLabelPrinter.Services.Glasses.Logging;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static class GlassesLexer
{
    public static List<GlassesToken> Scan(string text)
    {
        GlassesDebug.Title("LEXER");

        List<GlassesToken> tokens = [];

        if (string.IsNullOrWhiteSpace(text))
            return tokens;

        text = text.Trim();

        TokenBuilder builder = new();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            GlassesDebug.Info($"[{i}] '{c}'");

            //----------------------------------------
            // khoảng trắng
            //----------------------------------------

            if (IsWhiteSpace(c))
            {
                FlushToken(builder, tokens, i);
                continue;
            }

            //----------------------------------------
            // ký tự phân cách
            //----------------------------------------

            if (IsSeparator(c))
            {
                FlushToken(builder, tokens, i);

                tokens.Add(new GlassesToken
                {
                    Type = TokenType.Separator,
                    Text = c.ToString(),
                    Start = i,
                    End = i
                });

                GlassesDebug.Info($"SEP => {c}");

                continue;
            }

            //----------------------------------------
            // token
            //----------------------------------------

            if (!builder.HasValue)
                builder.Begin(i);

            builder.Append(c);
        }

        FlushToken(builder, tokens, text.Length);

        GlassesDebug.Success($"Token Count : {tokens.Count}");

        return tokens;
    }

    //---------------------------------------------------------
    // Đóng token
    //---------------------------------------------------------

    private static void FlushToken(
        TokenBuilder builder,
        List<GlassesToken> tokens,
        int endIndex)
    {
        if (!builder.HasValue)
            return;

        string value = builder.Build();

        TokenType type = DetectType(value);

        GlassesToken token = new()
        {
            Type = type,
            Text = value,
            Start = builder.StartIndex,
            End = endIndex - 1
        };

        tokens.Add(token);

        GlassesDebug.Info(
            $"TOKEN => {token.Type,-10} {token.Text}");

        builder.Clear();
    }

    //---------------------------------------------------------
    // Nhận diện loại token
    //---------------------------------------------------------

    private static TokenType DetectType(string value)
    {
        value = value.ToUpper();

        //----------------------------------------
        // MODEL
        //----------------------------------------

        if (value == "MODEL")
            return TokenType.Model;

        //----------------------------------------
        // KEYWORD
        //----------------------------------------

        if (value is "MÃ"
            or "MS"
            or "CODE")
            return TokenType.Keyword;

        //----------------------------------------
        // MKxxx
        //----------------------------------------

        if (value.StartsWith("MK"))
            return TokenType.Mk;

        //----------------------------------------
        // K020
        //----------------------------------------

        if (value.Length > 1 &&
            value[0] == 'K' &&
            char.IsDigit(value[1]))
            return TokenType.K;

        //----------------------------------------
        // PCN
        //----------------------------------------

        if (value.StartsWith("PCN"))
            return TokenType.Pcn;

        //----------------------------------------
        // COLOR
        //----------------------------------------

        if (ColorDictionary.Contains(value))
            return TokenType.Color;

        //----------------------------------------
        // CODE
        //----------------------------------------

        if (value.Any(char.IsDigit))
            return TokenType.Code;

        //----------------------------------------
        // WORD
        //----------------------------------------

        return TokenType.Word;
    }

    //---------------------------------------------------------

    private static bool IsSeparator(char c)
    {
        return c is
            '-'
            or '('
            or ')'
            or ','
            or ';';
    }

    //---------------------------------------------------------

    private static bool IsWhiteSpace(char c)
    {
        return char.IsWhiteSpace(c);
    }

    //---------------------------------------------------------

    private static bool IsTokenChar(char c)
    {
        return char.IsLetterOrDigit(c)
            || c == '/'
            || c == '-';
    }
}