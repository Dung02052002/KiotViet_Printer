namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public class GlassesToken
{
    public TokenType Type { get; set; }

    public string Text { get; set; } = "";

    public int Start { get; set; }

    public int End { get; set; }

    public int Index { get; set; }

    public override string ToString()
    {
        return $"{Type,-12} {Text}";
    }
}