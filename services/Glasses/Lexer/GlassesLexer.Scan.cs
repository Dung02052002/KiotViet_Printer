using KiotVietLabelPrinter.Services.Glasses.Logging;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
    //---------------------------------------------------------
    // Scan
    //---------------------------------------------------------

    private static void ScanCore(
        string text,
        List<GlassesToken> tokens)
    {
        TokenBuilder builder = new();

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            //-------------------------------------------------
            // WhiteSpace
            //-------------------------------------------------

            if (IsWhiteSpace(c))
            {
                Flush(builder, tokens, i);
                continue;
            }

            //-------------------------------------------------
            // Separator
            //-------------------------------------------------

            if (IsSeparator(c))
            {
                Flush(builder, tokens, i);

                tokens.Add(new GlassesToken
                {
                    Type = TokenType.Separator,
                    Text = c.ToString(),
                    Start = i,
                    End = i
                });

                continue;
            }

            //-------------------------------------------------
            // Hyphen
            //-------------------------------------------------

            if (c == '-')
            {
                if (IsHyphenSeparator(text, i))
                {
                    Flush(builder, tokens, i);

                    tokens.Add(new GlassesToken
                    {
                        Type = TokenType.Separator,
                        Text = "-",
                        Start = i,
                        End = i
                    });

                    continue;
                }
            }

            //-------------------------------------------------
            // Token
            //-------------------------------------------------

            if (!builder.HasValue)
                builder.Begin(i);

            builder.Append(c);
        }

        Flush(builder, tokens, text.Length);

        //--------------------------------------------
        // Debug
        //--------------------------------------------

        foreach (GlassesToken token in tokens)
        {
            GlassesDebug.Info(
                $"[{token.Index}] {token.Type,-10} {token.Text}");
        }
    }
}