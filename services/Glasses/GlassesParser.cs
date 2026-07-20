using System.Diagnostics;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Services.Glasses.Lexer;
using KiotVietLabelPrinter.Services.Glasses.Rules;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesParser
{
    //---------------------------------------------------------
    // Rule Engine
    //---------------------------------------------------------

    private readonly List<IGlassesRule> _rules =
    [
        new ModelRule(),
        new KeywordRule(),
        new LeftOfMkRule(),
        new RightOfMkRule(),
        new MkOnlyRule(),
        new KOnlyRule(),
        new PcnRule(),
        new FirstCodeRule(),
        new FallbackRule()
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

        return Parse(text);
    }

    //---------------------------------------------------------
    // Text
    //---------------------------------------------------------

    public GlassesParserResult Parse(string? text)
    {
        Stopwatch sw = Stopwatch.StartNew();

        GlassesParserResult result = new();

        if (string.IsNullOrWhiteSpace(text))
        {
            sw.Stop();
            result.Elapsed = sw.Elapsed;
            return result;
        }

        //-----------------------------------------------------
        // Original
        //-----------------------------------------------------

        result.OriginalText = text;

        //-----------------------------------------------------
        // Normalize
        //-----------------------------------------------------

        text = text.Trim();

        result.NormalizedText = text;

        result.AddLog("Normalize OK");

        //-----------------------------------------------------
        // Lexer
        //-----------------------------------------------------

        List<GlassesToken> tokens =
            GlassesLexer.Scan(text);

        result.Tokens = tokens;

        result.AddLog($"Token Count : {tokens.Count}");

        //-----------------------------------------------------
        // Rule Engine
        //-----------------------------------------------------

        foreach (IGlassesRule rule in
                 _rules.OrderBy(x => x.Priority))
        {
            RuleResult ruleResult =
                rule.Execute(tokens);

            if (ruleResult.Success)
            {
                result.BaseCode = ruleResult.BaseCode;

                result.RuleName = ruleResult.RuleName;

                result.AddRuleTrace(
                    $"✔ {rule.Name}");

                foreach (string log in ruleResult.Logs)
                    result.AddLog(log);

                break;
            }

            result.AddRuleTrace(
                $"✘ {rule.Name}");
        }

        //-----------------------------------------------------
        // Fallback
        //-----------------------------------------------------

        if (!result.Success)
        {
            result.AddLog("Không Rule nào match.");
        }

        //-----------------------------------------------------
        // Time
        //-----------------------------------------------------

        sw.Stop();

        result.Elapsed = sw.Elapsed;

        return result;
    }
}