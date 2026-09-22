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

            if (char.IsWhiteSpace(c))
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
                    Text = c.ToString(),
                    // DetectType() của một ký tự phân cách đơn lẻ như "(",",",
                    // ";",":" không rơi vào nhánh riêng nào nên luôn trả về Word
                    // mặc định — gán đúng type cuối cùng này ngay từ lúc tạo để
                    // GlassesLexer.Scan khỏi phải gọi lại DetectType cho toàn bộ
                    // token (xem vòng lặp DetectType ở đó).
                    Type = TokenType.Word,
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
                string current =
                    builder.HasValue
                        ? builder.Build()
                        : "";

                if (IsHyphenSeparator(
                    text,
                    i,
                    current))
                {
                    Flush(builder, tokens, i);

                    tokens.Add(new GlassesToken
                    {
                        Text = "-",
                        Type = TokenType.Separator,
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

        //-------------------------------------------------
        // Debug
        //-------------------------------------------------

        foreach (GlassesToken token in tokens)
        {
            GlassesDebug.Info(
                $"{token.Type,-10} {token.Text}");
        }
    }
}