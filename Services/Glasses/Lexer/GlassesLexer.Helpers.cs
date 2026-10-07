namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
    //---------------------------------------------------------
    // WhiteSpace
    //---------------------------------------------------------

    private static bool IsWhiteSpace(char c)
    {
        return char.IsWhiteSpace(c);
    }

    //---------------------------------------------------------
    // Separator
    // Có tạo Token
    //---------------------------------------------------------

    private static bool IsSeparator(char c)
    {
        return c is
            '('
            or ')'
            or ','
            or ';'
            or ':';
    }

    //---------------------------------------------------------
    // Delimiter
    // Chỉ cắt token
    //---------------------------------------------------------

    private static bool IsDelimiter(char c)
    {
        return c is
            '-'
            or '+';
    }

    //---------------------------------------------------------
    // Hyphen handling
    //---------------------------------------------------------

    private static bool IsHyphenSeparator(
        string text,
        int hyphenIndex,
        string current)
    {
        if (hyphenIndex + 1 >= text.Length)
            return true;

        char next = text[hyphenIndex + 1];

        if (char.IsWhiteSpace(next) || IsSeparator(next))
            return true;

        if (string.IsNullOrEmpty(current))
            return true;

        char prev = current[^1];

        bool prevToken = char.IsLetterOrDigit(prev) || prev == '/' || prev == '_';
        bool nextToken = char.IsLetterOrDigit(next) || next == '/' || next == '_';

        // Extract next meaningful segment (until whitespace or separator)
        int j = hyphenIndex + 1;
        Span<char> buf = stackalloc char[32];
        int bi = 0;

        while (j < text.Length && !char.IsWhiteSpace(text[j]) && !IsSeparator(text[j]))
        {
            if (bi < buf.Length) buf[bi++] = text[j];
            j++;
        }

        string nextSeg = bi == 0 ? "" : new string(buf.Slice(0, bi));

        // If next segment starts with MK (e.g., MK221), treat hyphen as separator
        if (nextSeg.Length >= 2 && (nextSeg.StartsWith("MK", StringComparison.InvariantCultureIgnoreCase)))
            return true;

        // If current starts with MK (e.g., MK109-P8315), treat hyphen as separator
        if (current.Length >= 2 && current.StartsWith("MK", StringComparison.InvariantCultureIgnoreCase))
            return true;

        // If both sides are alnum (or / or _) keep hyphen inside token (not a separator)
        if (prevToken && nextToken)
            return false;

        return true;
    }

    //---------------------------------------------------------
    // Token Char
    //---------------------------------------------------------

    private static bool IsTokenChar(char c)
    {
        return char.IsLetterOrDigit(c)
            || c == '/'
            || c == '_';
    }

    //---------------------------------------------------------
    // Flush
    //---------------------------------------------------------

    private static void Flush(
        TokenBuilder builder,
        List<GlassesToken> tokens,
        int endIndex)
    {
        if (!builder.HasValue)
            return;

        string value = builder.Build().Trim();

        if (value.Length == 0)
        {
            builder.Clear();
            return;
        }

        // "1806#", "6982#"... -> dấu # cuối chỉ là ký hiệu đánh dấu "còn thay
        // đổi theo màu" trong tên hàng, không thuộc về mã sản phẩm thật. Nếu
        // phần trước dấu # đã là một mã hợp lệ thì bỏ hẳn dấu # đi.
        if (value.Length > 1 && value[^1] == '#')
        {
            string withoutHash = value[..^1];

            if (DetectType(withoutHash) == TokenType.Code)
            {
                tokens.Add(new GlassesToken
                {
                    Text = withoutHash,
                    Type = TokenType.Code,
                    Start = builder.StartIndex,
                    End = endIndex - 1
                });

                builder.Clear();
                return;
            }
        }

        tokens.Add(new GlassesToken
        {
            Text = value,
            Type = DetectType(value),
            Start = builder.StartIndex,
            End = endIndex - 1
        });

        builder.Clear();
    }
}