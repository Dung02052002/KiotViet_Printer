using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class PcnRule : IGlassesRule
{
    public string Name => nameof(PcnRule);

    public int Priority => 70;

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        GlassesToken? token =
            tokens.FirstOrDefault(x => x.Type == TokenType.Pcn);

        if (token == null)
            return RuleResult.Fail("Không có PCN.");

        return RuleResult.Ok(
            token.Text,
            Name);
    }

    public bool Match(List<GlassesToken> tokens)
    {
        throw new NotImplementedException();
    }
}