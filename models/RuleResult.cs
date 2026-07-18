namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class RuleResult
{
    public bool Success { get; init; }

    public string BaseCode { get; init; } = "";

    public string RuleName { get; init; } = "";

    public string? Reason { get; init; }

    public List<string> Logs { get; } = [];

    public static RuleResult Fail(string? reason = null)
    {
        return new RuleResult
        {
            Success = false,
            Reason = reason
        };
    }

    public static RuleResult Ok(
        string baseCode,
        string ruleName)
    {
        return new RuleResult
        {
            Success = true,
            BaseCode = baseCode,
            RuleName = ruleName
        };
    }

    public void AddLog(string log)
    {
        Logs.Add(log);
    }
}