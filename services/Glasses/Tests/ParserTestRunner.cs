using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Services.Glasses;

namespace KiotVietLabelPrinter.Services.Glasses.Tests;

public static class ParserTestRunner
{
    public static List<ParserTestResult> RunAll()
    {
        List<ParserTestResult> results = [];

        GlassesParser parser = new();

        foreach (ParserTestCase test in ParserSamples.Get())
        {
            GlassesParserResult parse =
                parser.Parse(test.Input);

            results.Add(new ParserTestResult
            {
                TestCase = test,
                Actual = parse.BaseCode
            });
        }

        return results;
    }
}