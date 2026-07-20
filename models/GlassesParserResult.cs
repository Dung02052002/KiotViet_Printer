using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Models.Glasses;

public class GlassesParserResult
{
    //---------------------------------------------------------
    // Input
    //---------------------------------------------------------

    /// <summary>
    /// Chuỗi gốc.
    /// </summary>
    public string OriginalText { get; set; } = "";

    /// <summary>
    /// Chuỗi sau Normalize.
    /// </summary>
    public string NormalizedText { get; set; } = "";

    //---------------------------------------------------------
    // Output
    //---------------------------------------------------------

    /// <summary>
    /// BaseCode parser tìm được.
    /// </summary>
    public string BaseCode { get; set; } = "";

    /// <summary>
    /// Rule đã match.
    /// </summary>
    public string RuleName { get; set; } = "";

    //---------------------------------------------------------
    // Debug
    //---------------------------------------------------------

    /// <summary>
    /// Danh sách token sau Lexer.
    /// </summary>
    public List<GlassesToken> Tokens { get; set; } = [];

    /// <summary>
    /// Trace toàn bộ Rule đã chạy.
    /// </summary>
    public List<string> RuleTrace { get; } = [];

    /// <summary>
    /// Log debug.
    /// </summary>
    public List<string> Logs { get; } = [];

    /// <summary>
    /// Thời gian Parse.
    /// </summary>
    public TimeSpan Elapsed { get; set; }

    //---------------------------------------------------------

    public bool Success =>
        !string.IsNullOrWhiteSpace(BaseCode);

    //---------------------------------------------------------

    public void AddLog(string log)
    {
        if (!string.IsNullOrWhiteSpace(log))
            Logs.Add(log);
    }

    //---------------------------------------------------------

    public void AddRuleTrace(string trace)
    {
        if (!string.IsNullOrWhiteSpace(trace))
            RuleTrace.Add(trace);
    }

    //---------------------------------------------------------

    public void Clear()
    {
        BaseCode = "";
        RuleName = "";

        Tokens.Clear();
        RuleTrace.Clear();
        Logs.Clear();

        Elapsed = TimeSpan.Zero;
    }
}