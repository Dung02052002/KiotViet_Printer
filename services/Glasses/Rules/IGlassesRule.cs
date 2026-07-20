using KiotVietLabelPrinter.Services.Glasses.Lexer;

namespace KiotVietLabelPrinter.Services.Glasses.Rules;

public interface IGlassesRule
{
    /// <summary>
    /// Tên Rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Độ ưu tiên.
    /// Rule nhỏ sẽ chạy trước.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Thực thi Rule.
    /// </summary>
    RuleResult Execute(
        IReadOnlyList<GlassesToken> tokens);
}