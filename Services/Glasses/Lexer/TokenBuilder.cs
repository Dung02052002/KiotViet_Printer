using System.Text;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public class TokenBuilder
{
    private readonly StringBuilder _buffer = new();

    public int StartIndex { get; private set; }

    public bool HasValue => _buffer.Length > 0;

    //---------------------------------------------------------

    public void Begin(int startIndex)
    {
        if (HasValue)
            return;

        StartIndex = startIndex;
    }

    //---------------------------------------------------------

    public void Append(char c)
    {
        _buffer.Append(c);
    }

    //---------------------------------------------------------

    public string Build()
    {
        return _buffer.ToString();
    }

    //---------------------------------------------------------

    public void Clear()
    {
        _buffer.Clear();
        StartIndex = 0;
    }
}