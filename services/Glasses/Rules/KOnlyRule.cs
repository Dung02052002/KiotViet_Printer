using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class KOnlyRule : RuleBase
{
    public override string Name => "KOnlyRule";

    public override int Priority => 60;

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        GlassesToken? token =
            tokens.FirstOrDefault(
                x => x.Type == TokenType.K);

        if (token == null)
            return RuleResult.Fail();

        RuleResult result =
            RuleResult.Ok(
                token.Text,
                Name);

        result.AddLog(
            $"K -> {token.Text}");

        return result;
    }
}