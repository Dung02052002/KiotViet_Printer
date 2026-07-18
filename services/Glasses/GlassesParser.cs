using System.Diagnostics;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Services.Glasses.Lexer;
using KiotVietLabelPrinter.Services.Glasses.Rules;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesParser
{
    private readonly List<IGlassesRule> _rules =
    [
        new ModelRule(),
        new KeywordRule(),
        new LeftOfMkRule(),
        new RightOfMkRule(),
        new FirstCodeRule()
    ];

    //---------------------------------------------------------
    // Product
    //---------------------------------------------------------

    public GlassesParserResult Parse(ProductRow product)
    {
        string text =
            !string.IsNullOrWhiteSpace(product.ProductNameWithAttr)
                ? product.ProductNameWithAttr
                : product.ProductName;

        return Parse(text ?? "");
    }

    //---------------------------------------------------------
    // Text
    //---------------------------------------------------------

    public GlassesParserResult Parse(string text)
    {
        Stopwatch sw = Stopwatch.StartNew();

        GlassesParserResult result = new()
        {
            OriginalText = text
        };

        if (string.IsNullOrWhiteSpace(text))
            return result;

        //-----------------------------------------------------
        // Normalize
        //-----------------------------------------------------

        text = text.Trim();

        result.NormalizedText = text;

        //-----------------------------------------------------
        // Lexer
        //-----------------------------------------------------

        List<GlassesToken> tokens =
            GlassesLexer.Scan(text);

        result.AddLog($"Token Count : {tokens.Count}");

        //-----------------------------------------------------
        // Rule Engine
        //-----------------------------------------------------

        foreach (IGlassesRule rule in
                 _rules.OrderBy(x => x.Priority))
        {
            RuleResult ruleResult =
                rule.Execute(tokens);

            if (!ruleResult.Success)
                continue;

            result.BaseCode = ruleResult.BaseCode;
            result.RuleName = ruleResult.RuleName;

            foreach (string log in ruleResult.Logs)
                result.AddLog(log);

            break;
        }


        sw.Stop();

        result.Elapsed = sw.Elapsed;

        return result;
    }
}