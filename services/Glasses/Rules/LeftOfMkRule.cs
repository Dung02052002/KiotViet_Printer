using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class LeftOfMkRule : IGlassesRule
{
    public string Name => nameof(LeftOfMkRule);

    public int Priority => 30;

    public bool Match(List<GlassesToken> tokens)
    {
        return tokens.Any(t => t.Type == TokenType.Mk);
    }

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        for (int i = 1; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Mk)
                continue;

            GlassesToken left = tokens[i - 1];

            if (left.Type != TokenType.Code)
                continue;

            return RuleResult.Ok(
                left.Text,
                Name);
        }

        return RuleResult.Fail("Không tìm thấy Code bên trái MK.");
    }
}