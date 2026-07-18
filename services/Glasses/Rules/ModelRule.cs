using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class ModelRule : IGlassesRule
{
    public string Name => nameof(ModelRule);

    public int Priority => 10;

    public bool Match(List<GlassesToken> tokens)
    {
        return tokens.Any(t => t.Type == TokenType.Model);
    }

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Model)
                continue;

            for (int j = i + 1; j < tokens.Count; j++)
            {
                GlassesToken token = tokens[j];

                switch (token.Type)
                {
                    case TokenType.Separator:
                        continue;

                    case TokenType.Code:
                    case TokenType.Mk:
                    case TokenType.K:
                        return RuleResult.Ok(
                            token.Text,
                            Name);
                }

                break;
            }
        }

        return RuleResult.Fail("Không tìm thấy mã sau MODEL.");
    }
}