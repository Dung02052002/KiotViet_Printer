using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public abstract class RuleBase : IGlassesRule
{
    public abstract string Name { get; }

    public abstract int Priority { get; }

    public abstract RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens);

    //-----------------------------------------------------

    protected static GlassesToken? Next(
        IReadOnlyList<GlassesToken> tokens,
        int index)
    {
        if (index + 1 >= tokens.Count)
            return null;

        return tokens[index + 1];
    }

    //-----------------------------------------------------

    protected static GlassesToken? Previous(
        IReadOnlyList<GlassesToken> tokens,
        int index)
    {
        if (index <= 0)
            return null;

        return tokens[index - 1];
    }

    //-----------------------------------------------------

    protected static bool IsCode(
        GlassesToken? token)
    {
        return token != null &&
               token.Type == TokenType.Code;
    }

    //-----------------------------------------------------

    protected static bool IsMk(
        GlassesToken? token)
    {
        return token != null &&
               token.Type == TokenType.Mk;
    }

    //-----------------------------------------------------

    protected static bool IsKeyword(
        GlassesToken? token)
    {
        return token != null &&
               (token.Type == TokenType.Keyword ||
                token.Type == TokenType.Model);
    }
}