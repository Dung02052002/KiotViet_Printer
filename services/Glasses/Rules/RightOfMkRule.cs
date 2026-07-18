using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class RightOfMkRule : IGlassesRule
{
    public string Name => nameof(RightOfMkRule);

    public int Priority => 40;

    public bool Match(List<GlassesToken> tokens)
    {
        return tokens.Any(t => t.Type == TokenType.Mk);
    }

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count - 1; i++)
        {
            if (tokens[i].Type != TokenType.Mk)
                continue;

            for (int j = i + 1; j < tokens.Count; j++)
            {
                GlassesToken token = tokens[j];

                switch (token.Type)
                {
                    case TokenType.Separator:
                        continue;

                    case TokenType.Code:
                        return RuleResult.Ok(
                            token.Text,
                            Name);
                }

                break;
            }
        }

        return RuleResult.Fail("Không tìm thấy Code bên phải MK.");
    }
}