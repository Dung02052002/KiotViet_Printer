using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class MkOnlyRule : RuleBase
{
    public override string Name => "MkOnlyRule";

    public override int Priority => 50;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        GlassesToken? mk =
            tokens.FirstOrDefault(
                x => x.Type == TokenType.Mk);

        if (mk == null)
            return RuleResult.Fail(
                "Không có MK.");

        RuleResult result =
            RuleResult.Ok(
                mk.Text,
                Name);

        result.AddLog(
            $"MK ONLY : {mk.Text}");

        return result;
    }
}