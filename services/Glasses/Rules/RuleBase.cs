using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public abstract class RuleBase : IGlassesRule
{
    //---------------------------------------------------------

    public abstract string Name { get; }

    public abstract int Priority { get; }

    public abstract RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens);

    //---------------------------------------------------------
    // Previous Meaningful
    //---------------------------------------------------------

    protected static GlassesToken? PreviousMeaningful(
        IReadOnlyList<GlassesToken> tokens,
        int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            GlassesToken token = tokens[i];

            if (Ignore(token))
                continue;

            return token;
        }

        return null;
    }

    //---------------------------------------------------------
    // Next Meaningful
    //---------------------------------------------------------

    protected static GlassesToken? NextMeaningful(
        IReadOnlyList<GlassesToken> tokens,
        int index)
    {
        for (int i = index + 1; i < tokens.Count; i++)
        {
            GlassesToken token = tokens[i];

            if (Ignore(token))
                continue;

            return token;
        }

        return null;
    }

    //---------------------------------------------------------

    protected static bool Ignore(
        GlassesToken token)
    {
        return token.Type == TokenType.Separator;
    }

    //---------------------------------------------------------

    protected static bool IsCode(
        GlassesToken? token)
    {
        return token != null &&
               token.Type == TokenType.Code;
    }

    //---------------------------------------------------------

    protected static bool IsMk(
        GlassesToken? token)
    {
        return token != null &&
               token.Type == TokenType.Mk;
    }

    //---------------------------------------------------------

    protected static bool IsKeyword(
        GlassesToken? token)
    {
        return token != null &&
               (token.Type == TokenType.Keyword ||
                token.Type == TokenType.Model);
    }
}