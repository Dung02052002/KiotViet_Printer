namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public class GlassesToken
{
    public int Index { get; set; }

    public TokenType Type { get; set; }

    public string Text { get; set; } = "";

    public int Start { get; set; }

    public int End { get; set; }

    //------------------------------------

    public override string ToString()
    {
        return
            $"[{Index}] {Type} : {Text}";
    }
}