namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
    //---------------------------------------------------------
    // Peek
    //---------------------------------------------------------

    private static char? Peek(
        string text,
        int index)
    {
        if (index < 0)
            return null;

        if (index >= text.Length)
            return null;

        return text[index];
    }

    //---------------------------------------------------------
    // WhiteSpace
    //---------------------------------------------------------

    private static bool IsWhiteSpace(char c)
    {
        return char.IsWhiteSpace(c);
    }

    //---------------------------------------------------------
    // Digit
    //---------------------------------------------------------

    private static bool IsDigit(char? c)
    {
        return
            c != null &&
            char.IsDigit(c.Value);
    }

    //---------------------------------------------------------
    // Letter
    //---------------------------------------------------------

    private static bool IsLetter(char? c)
    {
        return
            c != null &&
            char.IsLetter(c.Value);
    }

    //---------------------------------------------------------
    // Token Char
    //---------------------------------------------------------

    private static bool IsTokenChar(char c)
    {
        return
            char.IsLetterOrDigit(c)
            || c == '/'
            || c == '-'
            || c == '_';
    }

    //---------------------------------------------------------
    // Separator
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
    // Hyphen có phải separator không
    //---------------------------------------------------------

    private static bool IsHyphenSeparator(
        string text,
        int index)
    {
        char? left = Peek(text, index - 1);
        char? right = Peek(text, index + 1);

        //------------------------------------
        // 9805-01
        //------------------------------------

        if (IsDigit(left) && IsDigit(right))
            return false;

        //------------------------------------
        // B305/147-MK220
        //------------------------------------

        if (IsDigit(left)
            && right != null
            && char.ToUpper(right.Value) == 'M')
            return true;

        //------------------------------------
        // MK109-P8315
        //------------------------------------

        if (left != null
            && char.ToUpper(left.Value) == '9'
            && IsLetter(right))
            return true;

        //------------------------------------
        // mặc định
        //------------------------------------

        return true;
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

        string value = builder.Build();

        GlassesToken token = new()
        {
            Text = value,
            Type = DetectType(value),
            Start = builder.StartIndex,
            End = endIndex - 1
        };

        tokens.Add(token);

        builder.Clear();
    }
}