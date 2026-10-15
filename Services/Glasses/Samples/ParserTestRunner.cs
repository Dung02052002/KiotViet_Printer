using KiotVietLabelPrinter.Models.Labels;

namespace KiotVietLabelPrinter.Services.Glasses.Samples;

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