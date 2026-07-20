using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class FallbackRule : RuleBase
{
    public override string Name => "FallbackRule";

    public override int Priority => 999;

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        if (tokens.Count == 0)
            return RuleResult.Fail();

        RuleResult result =
            RuleResult.Ok(
                tokens[0].Text,
                Name);

        result.AddLog(
            $"FALLBACK -> {tokens[0].Text}");

        return result;
    }
}