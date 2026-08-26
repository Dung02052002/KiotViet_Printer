using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

// Mũ: mã luôn là "MUxx" (VD "MU009-CM262014" -> "MU009"). Mã chỉ nằm
// trong cụm màu cuối câu, không đi kèm từ khoá "mã"/"model", nên hay bị
// FirstCodeRule nhặt nhầm số đo (đường kính, size) đứng trước đó trong
// câu. Quét trực tiếp token bắt đầu bằng MU + số để tránh nhầm lẫn đó.
public class MuHatRule : RuleBase
{
    public override string Name => "MuHatRule";

    public override int Priority => 80;

    private static readonly Regex MuPattern =
        new(@"^(MU\d+)(-[A-Z0-9]+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        foreach (GlassesToken token in tokens)
        {
            Match match = MuPattern.Match(token.Text);

            if (!match.Success)
                continue;

            string code = match.Groups[1].Value.ToUpperInvariant();

            RuleResult result =
                RuleResult.Ok(code, Name);

            result.AddLog(
                $"MU HAT -> {code}");

            return result;
        }

        return RuleResult.Fail();
    }
}
