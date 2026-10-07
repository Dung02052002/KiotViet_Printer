namespace KiotVietLabelPrinter.Services.Glasses.Tests;

public class ParserTestResult
{
    public ParserTestCase TestCase { get; init; } = new();

    public string Actual { get; init; } = "";

    public bool Success =>
        string.Equals(
            TestCase.Expected,
            Actual,
            StringComparison.OrdinalIgnoreCase);
}