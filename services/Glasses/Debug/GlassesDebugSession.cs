using KiotVietLabelPrinter.Services.Glasses.Lexer;
using KiotVietLabelPrinter.Services.Glasses.Logging;

namespace KiotVietLabelPrinter.Services.Glasses.Debug;

public class GlassesDebugSession
{
    public string Input { get; set; } = "";

    public List<GlassesToken> Tokens { get; set; } = [];

    public List<GlassesToken> RemovedTokens { get; set; } = [];

    public string RuleName { get; set; } = "";

    public string Result { get; set; } = "";

    public TimeSpan LexerTime { get; set; }

    public TimeSpan NormalizeTime { get; set; }

    public TimeSpan RuleTime { get; set; }

    public void Print()
    {
        if (!GlassesDebug.Enabled)
            return;

        GlassesDebug.Title("GLASSES PARSER REPORT");

        GlassesDebug.Info($"INPUT : {Input}");

        GlassesDebug.Separator();

        GlassesDebug.Info("TOKENS");

        foreach (var token in Tokens)
            GlassesDebug.Info(token.ToString());

        GlassesDebug.Separator();

        if (RemovedTokens.Count > 0)
        {
            GlassesDebug.Info("REMOVED");

            foreach (var token in RemovedTokens)
                GlassesDebug.Info(token.ToString());

            GlassesDebug.Separator();
        }

        GlassesDebug.Info($"RULE   : {RuleName}");
        GlassesDebug.Info($"RESULT : {Result}");

        GlassesDebug.Separator();

        GlassesDebug.Info($"Lexer      : {LexerTime.TotalMilliseconds:0.##} ms");
        GlassesDebug.Info($"Normalize  : {NormalizeTime.TotalMilliseconds:0.##} ms");
        GlassesDebug.Info($"RuleEngine : {RuleTime.TotalMilliseconds:0.##} ms");
    }
}