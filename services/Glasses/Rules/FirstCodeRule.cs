using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class FirstCodeRule : IGlassesRule
{
    public string Name => nameof(FirstCodeRule);

    public int Priority => 999;

    public bool Match(List<GlassesToken> tokens)
    {
        return tokens.Any(x => x.Type == TokenType.Code);
    }

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        GlassesToken? token =
            tokens.FirstOrDefault(x => x.Type == TokenType.Code);

        if (token == null)
            return RuleResult.Fail("Không tìm thấy Code.");

        return RuleResult.Ok(
            token.Text,
            Name);
    }
}