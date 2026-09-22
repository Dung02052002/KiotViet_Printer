using System.Diagnostics;
using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Services.Glasses.Lexer;
using KiotVietLabelPrinter.Services.Glasses.Rules;

namespace KiotVietLabelPrinter.Services.Glasses;

public class GlassesParser
{
    //---------------------------------------------------------
    // Rule Engine
    //---------------------------------------------------------

    // static readonly: mọi IGlassesRule ở đây không giữ state (chỉ đọc tokens
    // truyền vào Execute), nên chia sẻ 1 danh sách dùng chung cho mọi lần Parse
    // thay vì cấp phát mới 10 rule object + 1 List cho mỗi GlassesParser/mỗi
    // dòng sản phẩm là an toàn.
    //
    // Danh sách PHẢI giữ đúng thứ tự Priority tăng dần (10,15,20,30,40,50,60,
    // 70,80,90) — Parse() không còn OrderBy lại nữa (xem bên dưới), rule nào
    // thêm sau này phải tự chèn đúng vị trí.
    private static readonly List<IGlassesRule> _rules =
    [
        new ModelRule(),
        new BeltMauRule(),
        new KeywordRule(),
        new LeftOfMkRule(),
        new RightOfMkRule(),
        new MkOnlyRule(),
        new KOnlyRule(),
        new PcnRule(),
        new MuHatRule(),
        new FirstCodeRule()

        // Không có FallbackRule: mọi rule phía trên đều quét TOÀN BỘ
        // token trước khi thua, nên nếu tới đây vẫn chưa ra được mã thì
        // chắc chắn tên không chứa mã thật (không Code/Mk/K/Pcn/Model+Code
        // nào cả) — "đoán đại" token đầu tiên chỉ tạo ra rác kiểu
        // Mã hàng:"KÍNH"/"KHĂN"/"VỎ". Trường hợp này để BaseCode rỗng rồi
        // ApplyProductCodeFallback lo (dùng Mã hàng gốc KiotViet nếu có,
        // không thì để trống).
    ];

    // FirstCodeRule là rule tin cậy thấp nhất còn lại (chỉ đoán mã số/
    // chữ đầu tiên gặp trong tên, không có ngữ cảnh MK/mã/model đi kèm).
    // Khi nó match, hoặc khi không rule nào match, ưu tiên dùng thẳng
    // Mã hàng gốc từ KiotViet (product.ProductCode) thay vì đoán từ tên —
    // tên không phải lúc nào cũng chứa mã thật (VD "KÍNH GỌNG KIM LOẠI",
    // "... AO MẪU 2" không có mã trong tên, mã thật nằm ở cột Mã hàng).
    private static readonly HashSet<string> LowConfidenceRuleNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "FirstCodeRule"
        };

    //---------------------------------------------------------
    // Product
    //---------------------------------------------------------

    public GlassesParserResult Parse(ProductRow product)
    {
        string text =
            !string.IsNullOrWhiteSpace(product.ProductNameWithAttr)
                ? product.ProductNameWithAttr
                : product.ProductName;

        GlassesParserResult result = Parse(text);

        ApplyProductCodeFallback(result, product);

        return result;
    }

    private static void ApplyProductCodeFallback(
        GlassesParserResult result,
        ProductRow product)
    {
        string productCode = product.ProductCode?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(productCode))
            return;

        bool isLowConfidence =
            !result.Success ||
            LowConfidenceRuleNames.Contains(result.RuleName);

        if (!isLowConfidence)
            return;

        result.BaseCode = NormalizeBaseCode(productCode);
        result.RuleName = "ProductCodeFallback";
        result.AddLog($"PRODUCT CODE FALLBACK -> {result.BaseCode}");
    }

    //---------------------------------------------------------
    // Text
    //---------------------------------------------------------

    public GlassesParserResult Parse(string? text)
    {
        Stopwatch sw = Stopwatch.StartNew();

        GlassesParserResult result = new();

        if (string.IsNullOrWhiteSpace(text))
        {
            sw.Stop();
            result.Elapsed = sw.Elapsed;
            return result;
        }

        //-----------------------------------------------------
        // Original
        //-----------------------------------------------------

        result.OriginalText = text;

        //-----------------------------------------------------
        // Normalize
        //-----------------------------------------------------

        text = text.Trim();

        result.NormalizedText = text;

        result.AddLog("Normalize OK");

        //-----------------------------------------------------
        // Lexer
        //-----------------------------------------------------

        List<GlassesToken> tokens =
            GlassesLexer.Scan(text);

        result.Tokens = tokens;

        result.AddLog($"Token Count : {tokens.Count}");

        //-----------------------------------------------------
        // Rule Engine
        //-----------------------------------------------------

        foreach (IGlassesRule rule in _rules)
        {
            RuleResult ruleResult =
                rule.Execute(tokens);

            if (ruleResult.Success)
            {
                result.BaseCode = NormalizeBaseCode(ruleResult.BaseCode);

                result.RuleName = ruleResult.RuleName;

                result.AddRuleTrace(
                    $"✔ {rule.Name}");

                foreach (string log in ruleResult.Logs)
                    result.AddLog(log);

                break;
            }

            result.AddRuleTrace(
                $"✘ {rule.Name}");
        }

        //-----------------------------------------------------
        // Fallback
        //-----------------------------------------------------

        if (!result.Success)
        {
            result.AddLog("Không Rule nào match.");
        }

        //-----------------------------------------------------
        // Time
        //-----------------------------------------------------

        sw.Stop();

        result.Elapsed = sw.Elapsed;

        return result;
    }

    private static readonly Regex CodeWithKSuffixRegex =
        new(@"^([A-Z]{1,6}\d+)-K\d+$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CodePairLettersRegex =
        new(@"^([A-Z]{1,6}\d+)X([A-Z]{1,6}\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CodePairDigitsRegex =
        new(@"^(\d+)X(\d+)$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string NormalizeBaseCode(string? baseCode)
    {
        string value = (baseCode ?? string.Empty).Trim().ToUpperInvariant();

        if (value.Length == 0)
            return value;

        // D2823-K026 -> D2823
        Match match = CodeWithKSuffixRegex.Match(value);

        if (match.Success)
            return match.Groups[1].Value;

        // H008XH019 -> H008xH019
        match = CodePairLettersRegex.Match(value);

        if (match.Success)
            return $"{match.Groups[1].Value}x{match.Groups[2].Value}";

        // 8616X8615 -> 8616x8615 (cặp mã số thuần, không có chữ nên
        // không đụng tới các mã đơn kiểu YX35096 ở nhánh trên)
        match = CodePairDigitsRegex.Match(value);

        if (match.Success)
            return $"{match.Groups[1].Value}x{match.Groups[2].Value}";

        return value;
    }
}