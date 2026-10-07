using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Barcodes;
using KiotVietLabelPrinter.Services.Glasses;
using KiotVietLabelPrinter.Services.Glasses.Tests;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class ParserTests
{
    private static GlassesParserResult ParseCase(GlassesParser parser, ParserTestCase t) =>
        string.IsNullOrEmpty(t.ProductName)
            ? parser.Parse(t.Input)
            : parser.Parse(new ProductRow { ProductName = t.ProductName, ProductNameWithAttr = t.Input });

    [Fact]
    public void GlassesParser_AllSamples()
    {
        GlassesParser parser = new();
        var results = TestData.AllParserCases().Select(t =>
        {
            GlassesParserResult r = ParseCase(parser, t);
            return new
            {
                t.Name,
                t.Input,
                t.Expected,
                Actual = r.BaseCode,
                Pass = r.BaseCode == t.Expected,
                r.RuleName,
                r.NormalizedText,
                Tokens = r.Tokens.Select(x => $"{x.Index}:{x.Type}:{x.Text}@{x.Start}-{x.End}").ToList(),
                r.RuleTrace,
                r.Success
            };
        }).ToList();

        Snapshot.Match(results);
    }

    [Fact]
    public void ParserTestRunner_RunAll_MatchesParserLab()
    {
        Snapshot.Match(ParserTestRunner.RunAll()
            .Select(r => new { r.TestCase.Name, r.TestCase.Expected, r.Actual })
            .ToList());
    }

    [Fact]
    public void BarcodeParser_AllSamples()
    {
        var results = TestData.AllParserCases().Select(t => new
        {
            t.Name,
            Parse = BarcodeParser.Parse(t.Input, "FALLBACK", t.ProductName),
            Full = BarcodeParser.ParseFull(t.Input, "FALLBACK", t.ProductName)
        }).ToList();

        Snapshot.Match(results);
    }

    [Fact]
    public void BarcodeParser_EdgeInputs()
    {
        string[] inputs = ["", "   ", "abc", "MK", "Kính MK1234", "123456", "TL", "Gọng kính model 6282 C3", "San pham 12.5 ABC"];
        Snapshot.Match(inputs.Select(i => new
        {
            Input = i,
            NoFallback = BarcodeParser.ParseFull(i),
            WithFallback = BarcodeParser.ParseFull(i, "FB01", "Tên gốc")
        }).ToList());
    }

    [Fact]
    public void BeltCodeExtractor_AllSamples()
    {
        var results = TestData.AllParserCases().Select(t =>
        {
            bool ok = BeltCodeExtractor.TryExtract(t.Input, t.ProductName, out string code, out string log);
            bool ok1 = BeltCodeExtractor.TryExtract(t.Input, out string code1, out _);
            return new { t.Name, ok, code, log, ok1, code1 };
        }).ToList();

        Snapshot.Match(results);
    }

    [Fact]
    public void GlassesDocumentBuilder_AllSamples()
    {
        Snapshot.Match(TestData.ProductsFromSamples().Select(p =>
        {
            GlassesDocument d = GlassesDocumentBuilder.Build(p, "NV7");
            return new { d.BaseCode, d.Barcode, d.AttributeText, d.GlassesTitle, d.GlassesInfo, d.GlassesInfoLeft, d.GlassesInfoRight };
        }).ToList());
    }
}
