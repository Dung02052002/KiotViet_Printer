using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class FallbackRule : RuleBase
{
    public override string Name => "FallbackRule";

    public override int Priority => 999;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        if (tokens.Count == 0)
            return RuleResult.Fail(
                "Không có token.");

        GlassesToken token = tokens[0];

        RuleResult result =
            RuleResult.Ok(
                token.Text,
                Name);

        result.AddLog(
            $"FALLBACK : {token.Text}");

        return result;
    }
}