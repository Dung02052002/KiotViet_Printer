using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class KeywordRule : RuleBase
{
    public override string Name => "KeywordRule";

    public override int Priority => 20;

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            GlassesToken token = tokens[i];

            if (!IsKeyword(token))
                continue;

            GlassesToken? next =
                NextMeaningful(tokens, i);

            if (!IsCode(next))
                continue;

            RuleResult result =
                RuleResult.Ok(
                    next!.Text,
                    Name);

            result.AddLog(
                $"{token.Text} -> {next.Text}");

            return result;
        }

        return RuleResult.Fail();
    }
}