using System.Text.RegularExpressions;
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
    // Previous Meaningful, bỏ qua cả từ màu
    // VD "(M2 PINK MK6)" -> mã thật M2 nằm cách MK6 một từ màu,
    // không đứng liền kề như các trường hợp kính thông thường.
    //---------------------------------------------------------

    protected static GlassesToken? PreviousMeaningfulSkipColor(
        IReadOnlyList<GlassesToken> tokens,
        int index)
    {
        for (int i = index - 1; i >= 0; i--)
        {
            GlassesToken token = tokens[i];

            if (Ignore(token) || token.Type == TokenType.Color)
                continue;

            return token;
        }

        return null;
    }

    //---------------------------------------------------------

    protected static bool IsCode(
        GlassesToken? token)
    {
        return token != null &&
               token.Type == TokenType.Code;
    }

    //---------------------------------------------------------
    // "999K" mặc định là Word (giá tiền viết tắt, VD "TẶNG HÓA ĐƠN
    // 999K"), nhưng khi đứng ngay sau từ khoá "mã"/"model" thì chắc
    // chắn là mã thật (VD "mã 208K") chứ không ai ghi giá tiền ngay
    // sau từ "mã" cả.
    //---------------------------------------------------------

    private static readonly Regex CurrencyLikeCode =
        new(@"^\d+K$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected static bool IsCodeAfterKeyword(
        GlassesToken? token)
    {
        return IsCode(token) ||
               (token != null &&
                token.Type == TokenType.Word &&
                CurrencyLikeCode.IsMatch(token.Text));
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