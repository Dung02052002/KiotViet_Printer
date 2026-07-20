using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class PcnRule : RuleBase
{
    public override string Name => "PcnRule";

    public override int Priority => 70;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        GlassesToken? token =
            tokens.FirstOrDefault(
                x => x.Type == TokenType.Pcn);

        if (token == null)
            return RuleResult.Fail(
                "Không tìm thấy PCN.");

        RuleResult result =
            RuleResult.Ok(
                token.Text,
                Name);

        result.AddLog(
            $"PCN : {token.Text}");

        return result;
    }
}