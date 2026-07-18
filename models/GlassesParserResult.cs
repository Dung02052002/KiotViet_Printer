namespace KiotVietLabelPrinter.Models.Glasses;

public class GlassesParserResult
{
    /// <summary>
    /// Chuỗi gốc
    /// </summary>
    public string OriginalText { get; set; } = "";

    /// <summary>
    /// Chuỗi sau Normalize
    /// </summary>
    public string NormalizedText { get; set; } = "";

    /// <summary>
    /// Mã kính cuối cùng
    /// </summary>
    public string BaseCode { get; set; } = "";

    /// <summary>
    /// Rule đã match
    /// </summary>
    public string RuleName { get; set; } = "";

    /// <summary>
    /// Thời gian parse
    /// </summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>
    /// Log debug
    /// </summary>
    public List<string> Logs { get; } = [];

    public bool Success =>
        !string.IsNullOrWhiteSpace(BaseCode);

    public void AddLog(string log)
    {
        Logs.Add(log);
    }
}