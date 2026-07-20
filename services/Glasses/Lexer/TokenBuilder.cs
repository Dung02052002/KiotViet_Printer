namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public class TokenBuilder
{
    private readonly List<char> _buffer = [];

    public int StartIndex { get; private set; }

    public bool HasValue => _buffer.Count > 0;

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
        _buffer.Add(c);
    }

    //---------------------------------------------------------

    public string Build()
    {
        return new string(_buffer.ToArray());
    }

    //---------------------------------------------------------

    public void Clear()
    {
        _buffer.Clear();
        StartIndex = 0;
    }
}