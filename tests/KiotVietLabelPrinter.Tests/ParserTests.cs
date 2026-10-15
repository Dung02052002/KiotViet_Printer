using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Models.Labels;
using KiotVietLabelPrinter.Services.Barcodes;
using KiotVietLabelPrinter.Services.Glasses.Samples;
using KiotVietLabelPrinter.Services.Glasses;
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

    // Các định dạng mã đã hỗ trợ (xem GlassesLexer.DetectType), cả chữ thường:
    // kết quả không được phụ thuộc hoa/thường.
    [Theory]
    [InlineData("MK101 - P8315", "P8315")]
    [InlineData("MK101-P8315", "P8315")]
    [InlineData("Kính 9805-01 MK112", "9805-01")]
    [InlineData("Kính B305/147 MK221", "B305/147")]
    [InlineData("Kính KM-05-8820 MK108", "KM-05-8820")]
    [InlineData("Kính mát MK101", "MK101")]
    public void GlassesParser_SupportedCodeFormats(string input, string expected)
    {
        GlassesParser parser = new();

        Assert.Equal(expected, parser.Parse(input).BaseCode);
        Assert.Equal(expected, parser.Parse(input.ToLowerInvariant()).BaseCode);
    }

    // Parse() chạy rule theo đúng thứ tự trong danh sách (không sắp lại), nên
    // danh sách phải theo Priority tăng dần, không trùng — rule tổng quát
    // (FirstCodeRule) luôn cuối cùng.
    [Fact]
    public void GlassesParser_RulesRunInPriorityOrder()
    {
        List<int> priorities = GlassesParser.Rules.Select(r => r.Priority).ToList();

        Assert.Equal(priorities.Order(), priorities);
        Assert.Equal(priorities.Count, priorities.Distinct().Count());
        Assert.Equal("FirstCodeRule", GlassesParser.Rules[^1].Name);
    }

    // KiotViet/Excel đôi khi trả tiếng Việt dạng tổ hợp (NFD: "Ã" = "A" + dấu
    // rời). Cùng một tên hàng thì dạng NFD phải ra đúng mã như dạng NFC —
    // trước đây từ khoá "MÃ"/"mã sp"/"nữ" không khớp nên rơi sang rule khác.
    [Fact]
    public void Parsers_GiveSameResult_ForDecomposedVietnamese()
    {
        GlassesParser parser = new();
        List<string> mismatches = [];

        // Thêm các ca mà CHỈ từ khoá có dấu ("mã") mới chọn đúng mã — mẫu sẵn
        // có đều được rule khác tình cờ ra cùng kết quả nên không lộ lỗi.
        ParserTestCase[] keywordCases =
        [
            new() { Name = "NFD mã sau số đo", Input = "Kính 52 mã 208K", Expected = "208K" },
            new() { Name = "NFD mã sau mã khác", Input = "Kính nữ D500 mã 6282", Expected = "6282" }
        ];

        foreach (ParserTestCase t in TestData.AllParserCases().Concat(keywordCases))
        {
            ParserTestCase nfd = new()
            {
                Name = t.Name,
                Input = t.Input.Normalize(System.Text.NormalizationForm.FormD),
                ProductName = t.ProductName?.Normalize(System.Text.NormalizationForm.FormD) ?? "",
                Expected = t.Expected
            };

            string glassesNfc = ParseCase(parser, t).BaseCode;
            string glassesNfd = ParseCase(parser, nfd).BaseCode.Normalize(System.Text.NormalizationForm.FormC);
            if (glassesNfc != glassesNfd)
                mismatches.Add($"Glasses {t.Name}: \"{glassesNfc}\" vs NFD \"{glassesNfd}\"");

            string barcodeNfc = BarcodeParser.Parse(t.Input, "FB", t.ProductName ?? "");
            string barcodeNfd = BarcodeParser.Parse(nfd.Input, "FB", nfd.ProductName).Normalize(System.Text.NormalizationForm.FormC);
            if (barcodeNfc != barcodeNfd)
                mismatches.Add($"Barcode {t.Name}: \"{barcodeNfc}\" vs NFD \"{barcodeNfd}\"");
        }

        Assert.Empty(mismatches);
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
