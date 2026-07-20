using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class FallbackRule : IGlassesRule
{
    public string Name => nameof(FallbackRule);

    public int Priority => int.MaxValue;

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        GlassesToken? token =
            tokens.FirstOrDefault(x =>
                x.Type == TokenType.Code);

        if (token != null)
        {
            return RuleResult.Ok(
                token.Text,
                Name);
        }

        token =
            tokens.FirstOrDefault(x =>
                x.Type == TokenType.Mk);

        if (token != null)
        {
            return RuleResult.Ok(
                token.Text,
                Name);
        }

        token =
            tokens.FirstOrDefault(x =>
                x.Type == TokenType.K);

        if (token != null)
        {
            return RuleResult.Ok(
                token.Text,
                Name);
        }

        return RuleResult.Fail("Không tìm thấy mã.");
    }

    public bool Match(List<GlassesToken> tokens)
    {
        throw new NotImplementedException();
    }
}