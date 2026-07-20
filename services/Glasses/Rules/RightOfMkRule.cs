using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class RightOfMkRule : RuleBase
{
    public override string Name => "RightOfMkRule";

    public override int Priority => 40;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            GlassesToken current = tokens[i];

            if (!IsMk(current))
                continue;

            GlassesToken? right =
                Next(tokens, i);

            if (!IsCode(right))
                continue;

            RuleResult result =
                RuleResult.Ok(
                    right!.Text,
                    Name);

            result.AddLog(
                $"RIGHT OF MK : {right.Text}");

            return result;
        }

        return RuleResult.Fail(
            "Không có CODE bên phải MK.");
    }
}