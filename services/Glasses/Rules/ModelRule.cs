using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class ModelRule : RuleBase
{
    public override string Name => "ModelRule";

    public override int Priority => 10;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            GlassesToken token = tokens[i];

            if (token.Type != TokenType.Model)
                continue;

            //-------------------------------------------------
            // MODEL xxxx
            //-------------------------------------------------

            GlassesToken? next =
                Next(tokens, i);

            if (!IsCode(next))
                continue;

            RuleResult result =
                RuleResult.Ok(
                    next!.Text,
                    Name);

            result.AddLog(
                $"MODEL -> {next.Text}");

            return result;
        }

        return RuleResult.Fail(
            "Không tìm thấy MODEL.");
    }
}