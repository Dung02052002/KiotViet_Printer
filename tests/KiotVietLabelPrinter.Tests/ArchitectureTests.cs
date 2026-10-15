using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Giữ hướng phụ thuộc giữa các tầng (quét source của project chính):
///   Forms / Views / UI  →  Services  →  Models
///   Infrastructure      →  Services / Models
/// Models không biết tới tầng nào khác; Services và Infrastructure không
/// được hiện UI (MessageBox, hộp thoại) hay tham chiếu Forms/Views/UI.
/// </summary>
public class ArchitectureTests
{
    private static string RepoRoot([CallerFilePath] string file = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, "..", ".."));

    private static IEnumerable<(string File, string Code)> SourceFiles(string folder) =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot(), folder), "*.cs", SearchOption.AllDirectories)
            .Select(f => (Path.GetRelativePath(RepoRoot(), f), StripCommentsAndStrings(File.ReadAllText(f))));

    private static string StripCommentsAndStrings(string code)
    {
        code = Regex.Replace(code, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        code = Regex.Replace(code, @"//[^\n]*", " ");
        code = Regex.Replace(code, "\"\"\"[\\s\\S]*?\"\"\"", "\"\"");
        code = Regex.Replace(code, "@\"(?:[^\"]|\"\")*\"", "\"\"");
        return Regex.Replace(code, "\\$?\"(?:\\\\.|[^\"\\\\\\n])*\"", "\"\"");
    }

    private static List<string> Violations(string folder, string pattern) =>
        SourceFiles(folder)
            .Where(f => Regex.IsMatch(f.Code, pattern))
            .Select(f => f.File)
            .ToList();

    [Fact]
    public void Models_DependOnNothingElseInApp()
    {
        Assert.Empty(Violations("Models", @"using\s+KiotVietLabelPrinter\.(Services|Infrastructure|Forms|Views|UI)\b"));
    }

    [Theory]
    [InlineData("Services")]
    [InlineData("Infrastructure")]
    public void NonUiLayers_DoNotReferenceUi(string folder)
    {
        Assert.Empty(Violations(folder, @"using\s+KiotVietLabelPrinter\.(Forms|Views|UI)\b"));
        Assert.Empty(Violations(folder, @"\b(MessageBox|PrintDialog|OpenFileDialog|SaveFileDialog|FolderBrowserDialog)\b|\.ShowDialog\(|:\s*(Form|UserControl)\b"));
    }

    [Fact]
    public void Services_DoNotDependOnForms()
    {
        Assert.Empty(Violations("Services", @"\bKiotVietLabelPrinter\.Forms\b"));
    }

    [Fact]
    public void NamespaceMatchesFolder()
    {
        List<string> wrong = [];
        foreach (string folder in new[] { "Forms", "Views", "UI", "Models", "Services", "Infrastructure" })
        {
            foreach (string file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), folder), "*.cs", SearchOption.AllDirectories))
            {
                string rel = Path.GetDirectoryName(Path.GetRelativePath(RepoRoot(), file))!;
                string expected = "KiotVietLabelPrinter." + rel.Replace(Path.DirectorySeparatorChar, '.');
                Match m = Regex.Match(File.ReadAllText(file), @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline);
                // Thiếu khai báo namespace = lớp rơi vào global namespace — cũng là sai.
                if (!m.Success || m.Groups[1].Value != expected)
                    wrong.Add($"{Path.GetRelativePath(RepoRoot(), file)}: {(m.Success ? m.Groups[1].Value : "(không có namespace)")} (mong đợi {expected})");
            }
        }

        Assert.Empty(wrong);
    }
}
