using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class MkOnlyRule : IGlassesRule
{
    public string Name => nameof(MkOnlyRule);

    public int Priority => 50;

    public RuleResult Execute(List<GlassesToken> tokens)
    {
        GlassesToken? mk =
            tokens.FirstOrDefault(x => x.Type == TokenType.Mk);

        if (mk == null)
            return RuleResult.Fail("Không có MK.");

        //-------------------------------------------------
        // Có Code bên trái?
        //-------------------------------------------------

        bool leftCode =
            tokens.Any(x =>
                x.End < mk.Start &&
                x.Type == TokenType.Code);

        if (leftCode)
            return RuleResult.Fail(
                "Đã có Code bên trái.");

        //-------------------------------------------------
        // Có Code bên phải?
        //-------------------------------------------------

        bool rightCode =
            tokens.Any(x =>
                x.Start > mk.End &&
                x.Type == TokenType.Code);

        if (rightCode)
            return RuleResult.Fail(
                "Đã có Code bên phải.");

        //-------------------------------------------------
        // Chỉ còn MK
        //-------------------------------------------------

        return RuleResult.Ok(
            mk.Text,
            Name);
    }

    public bool Match(List<GlassesToken> tokens)
    {
        throw new NotImplementedException();
    }
}