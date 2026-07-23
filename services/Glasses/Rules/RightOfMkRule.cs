using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public class RightOfMkRule : RuleBase
{
    public override string Name => "RightOfMkRule";

    public override int Priority => 40;

    public override RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].Type != TokenType.Mk)
                continue;

            int rightIndex = FindNextMeaningfulIndex(tokens, i);

            if (rightIndex < 0)
                continue;

            GlassesToken right = tokens[rightIndex];

            if (!IsCode(right))
                continue;

            int xIndex = FindNextMeaningfulIndex(tokens, rightIndex);

            if (xIndex >= 0 &&
                IsConnectorX(tokens[xIndex].Text))
            {
                int code2Index = FindNextMeaningfulIndex(tokens, xIndex);

                if (code2Index >= 0 &&
                    IsCode(tokens[code2Index]))
                {
                    string merged =
                        $"{right.Text}x{tokens[code2Index].Text}";

                    RuleResult mergedResult =
                        RuleResult.Ok(
                            merged,
                            Name);

                    mergedResult.AddLog(
                        $"RIGHT OF MK (PAIR) -> {merged}");

                    return mergedResult;
                }
            }

            RuleResult result =
                RuleResult.Ok(
                    right.Text,
                    Name);

            result.AddLog(
                $"RIGHT OF MK -> {right.Text}");

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

    private static bool IsConnectorX(string value)
    {
        return string.Equals(
            value?.Trim(),
            "X",
            StringComparison.OrdinalIgnoreCase);
    }
}