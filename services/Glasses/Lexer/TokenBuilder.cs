using System.Text;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public class TokenBuilder
{
    private readonly StringBuilder _builder = new();

    public bool HasValue =>
        _builder.Length > 0;

    public int StartIndex { get; private set; }

    //------------------------------------

    public void Begin(int index)
    {
        if (_builder.Length == 0)
            StartIndex = index;
    }

    //------------------------------------

    public void Append(char c)
    {
        _builder.Append(c);
    }

    //------------------------------------

    public string Build()
    {
        return _builder.ToString();
    }

    //------------------------------------

    public void Clear()
    {
        _builder.Clear();

        StartIndex = 0;
    }
}