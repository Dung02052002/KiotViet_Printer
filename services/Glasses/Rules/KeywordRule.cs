using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class KeywordRule : RuleBase
{
    public override string Name => "KeywordRule";

    public override int Priority => 20;

    //---------------------------------------------------------

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            GlassesToken token = tokens[i];

            if (!IsKeyword(token))
                continue;

            //-------------------------------------------------
            // KEYWORD CODE
            //-------------------------------------------------

            GlassesToken? next = Next(tokens, i);

            if (IsCode(next))
            {
                RuleResult result =
                    RuleResult.Ok(
                        next!.Text,
                        Name);

                result.AddLog(
                    $"{token.Text} -> {next.Text}");

                return result;
            }

            //-------------------------------------------------
            // KEYWORD : CODE
            //-------------------------------------------------

            if (next?.Type == TokenType.Separator &&
                next.Text == ":")
            {
                GlassesToken? next2 =
                    Next(tokens, i + 1);

                if (IsCode(next2))
                {
                    RuleResult result =
                        RuleResult.Ok(
                            next2!.Text,
                            Name);

                    result.AddLog(
                        $"{token.Text}: {next2.Text}");

                    return result;
                }
            }
        }

        return RuleResult.Fail(
            "Không tìm thấy Keyword.");
    }
}