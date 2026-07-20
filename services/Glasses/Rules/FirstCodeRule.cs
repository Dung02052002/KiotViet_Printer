using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class FirstCodeRule : RuleBase
{
    public override string Name => "FirstCodeRule";

    public override int Priority => 90;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        GlassesToken? token =
            tokens.FirstOrDefault(
                x => x.Type == TokenType.Code);

        if (token == null)
            return RuleResult.Fail(
                "Không có CODE.");

        RuleResult result =
            RuleResult.Ok(
                token.Text,
                Name);

        result.AddLog(
            $"FIRST CODE : {token.Text}");

        return result;
    }
}