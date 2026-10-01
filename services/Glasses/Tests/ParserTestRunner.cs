using KiotVietLabelPrinter.Models;
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
                string.IsNullOrEmpty(test.ProductName)
                    ? parser.Parse(test.Input)
                    : parser.Parse(new ProductRow
                    {
                        ProductName = test.ProductName,
                        ProductNameWithAttr = test.Input
                    });

            results.Add(new ParserTestResult
            {
                TestCase = test,
                Actual = parse.BaseCode
            });
        }

        return results;
    }
}