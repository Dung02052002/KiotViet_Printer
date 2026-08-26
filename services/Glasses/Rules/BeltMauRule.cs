using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

// Thắt lưng: "THẮT LƯNG NAM TL14 MẪU 32" / "... PCN-TL15 MẪU 9" -> mã thật
// là cả cụm "TL14 MẪU 32" / "TL15 MẪU 9" (bỏ tiền tố PCN- nếu có), không
// phải chỉ riêng "TL14" (bị FirstCodeRule nhặt nhầm) hay chỉ riêng số.
public class BeltMauRule : RuleBase
{
    public override string Name => "BeltMauRule";

    public override int Priority => 15;

    private static readonly Regex TlPattern =
        new(@"^(?:[A-Z]+-)?(TL\d+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            Match tlMatch = TlPattern.Match(tokens[i].Text);

            if (!tlMatch.Success)
                continue;

            int mauIndex = FindNextMeaningfulIndex(tokens, i);

            if (mauIndex < 0 ||
                !string.Equals(
                    tokens[mauIndex].Text,
                    "MẪU",
                    StringComparison.OrdinalIgnoreCase))
                continue;

            int numberIndex = FindNextMeaningfulIndex(tokens, mauIndex);

            if (numberIndex < 0 || !IsCode(tokens[numberIndex]))
                continue;

            string code =
                $"{tlMatch.Groups[1].Value.ToUpperInvariant()} MẪU {tokens[numberIndex].Text}";

            RuleResult result =
                RuleResult.Ok(code, Name);

            result.AddLog(
                $"BELT MẪU -> {code}");

            return result;
        }

        return RuleResult.Fail();
    }

    private static int FindNextMeaningfulIndex(
        IReadOnlyList<GlassesToken> tokens,
        int index)
    {
        for (int i = index + 1; i < tokens.Count; i++)
        {
            if (Ignore(tokens[i]))
                continue;

            return i;
        }

        return -1;
    }
}
