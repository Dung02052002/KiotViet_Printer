using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class LeftOfMkRule : RuleBase
{
    public override string Name => "LeftOfMkRule";

    public override int Priority => 30;

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Mk)
                continue;

            GlassesToken? left =
                PreviousMeaningfulSkipColor(tokens, i);

            if (!IsCode(left))
                continue;

            RuleResult result =
                RuleResult.Ok(
                    left!.Text,
                    Name);

            result.AddLog(
                $"LEFT OF MK -> {left.Text}");

            return result;
        }

        return RuleResult.Fail();
    }
}