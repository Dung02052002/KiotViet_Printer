using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class LeftOfMkRule : RuleBase
{
    public override string Name => "LeftOfMkRule";

    public override int Priority => 30;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 1; i < tokens.Count; i++)
        {
            GlassesToken current = tokens[i];

            if (!IsMk(current))
                continue;

            GlassesToken? left =
                Previous(tokens, i);

            if (!IsCode(left))
                continue;

            RuleResult result =
                RuleResult.Ok(
                    left!.Text,
                    Name);

            result.AddLog(
                $"LEFT OF MK : {left.Text}");

            return result;
        }

        return RuleResult.Fail(
            "Không có CODE bên trái MK.");
    }
}