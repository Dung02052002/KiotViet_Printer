using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public interface IGlassesRule
{
    string Name { get; }

    int Priority { get; }

    bool Match(List<GlassesToken> tokens);

    RuleResult Execute(List<GlassesToken> tokens);
}