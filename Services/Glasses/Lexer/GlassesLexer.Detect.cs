using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Services.Glasses.Dictionaries;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
    //---------------------------------------------------------
    // Regex tĩnh dựng sẵn 1 lần — DetectType chạy cho MỌI token của MỌI dòng
    // sản phẩm khi import Excel; overload tĩnh Regex.IsMatch(value, pattern)
    // chỉ cache 15 pattern gần nhất/tiến trình nên với ~18 pattern trong hàm
    // này (cộng thêm pattern ở các class khác dùng xen kẽ) rất dễ bị đẩy khỏi
    // cache, gây biên dịch lại liên tục.
    //---------------------------------------------------------

    private static readonly Regex MkRegex = new(@"^MK\d+$", RegexOptions.Compiled);
    private static readonly Regex KRegex = new(@"^K\d+$", RegexOptions.Compiled);
    private static readonly Regex PcnRegex = new(@"^PCN\d*$", RegexOptions.Compiled);
    private static readonly Regex PureNumberRegex = new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex LettersDigitsRegex = new(@"^[A-Z]{1,6}\d+$", RegexOptions.Compiled);
    private static readonly Regex LettersDigitsHyphenDigitsRegex =
        new(@"^[A-Z]{1,6}\d+-\d+$", RegexOptions.Compiled);
    private static readonly Regex LettersDigitsHyphenLettersDigitsRegex =
        new(@"^[A-Z]{1,6}\d+-[A-Z]{1,6}\d+$", RegexOptions.Compiled);
    private static readonly Regex DigitsLettersHyphenDigitsRegex =
        new(@"^\d+[A-Z]{1,6}-\d+$", RegexOptions.Compiled);
    private static readonly Regex CurrencyLikeRegex = new(@"^\d+K$", RegexOptions.Compiled);
    private static readonly Regex DigitsLettersRegex = new(@"^\d+[A-Z]{1,6}$", RegexOptions.Compiled);
    private static readonly Regex DigitsHyphenDigitsRegex = new(@"^\d+-\d+$", RegexOptions.Compiled);
    private static readonly Regex LetterDigitsSlashDigitsRegex =
        new(@"^[A-Z]?\d+/\d+$", RegexOptions.Compiled);
    private static readonly Regex KmDashRegex = new(@"^[A-Z]{2,6}-\d{2}-\d+$", RegexOptions.Compiled);
    private static readonly Regex ThreeSegmentDashRegex =
        new(@"^[A-Z0-9]{1,6}-[A-Z0-9]{1,6}-[A-Z0-9]{1,6}$", RegexOptions.Compiled);
    private static readonly Regex LettersDigitsLettersRegex =
        new(@"^[A-Z]{1,3}\d{2,6}[A-Z]{1,3}(-\d{1,3})?$", RegexOptions.Compiled);
    private static readonly Regex LettersDashDigitsRegex = new(@"^[A-Z]{2,6}-\d{3,6}$", RegexOptions.Compiled);
    private static readonly Regex DigitsDashLetterDigitsRegex =
        new(@"^\d{2,6}-[A-Z]{1,3}\d{1,7}$", RegexOptions.Compiled);
    private static readonly Regex DigitsDashDigitsLetterRegex =
        new(@"^\d{2,4}-\d{1,3}[A-Z]{1,3}$", RegexOptions.Compiled);

    //---------------------------------------------------------
    // Detect TokenType
    //---------------------------------------------------------

    private static TokenType DetectType(string value)
    {
        value = value.Trim().ToUpper();

        if (value == "-")
            return TokenType.Separator;

        if (string.IsNullOrWhiteSpace(value))
            return TokenType.Word;

        //-----------------------------------------------------
        // MODEL
        //-----------------------------------------------------

        if (value == "MODEL")
            return TokenType.Model;

        //-----------------------------------------------------
        // KEYWORD
        //-----------------------------------------------------

        if (value is
            "MÃ"
            or "MÃSP"
            or "MÃ SP"
            or "MS"
            or "CODE")
            return TokenType.Keyword;

        //-----------------------------------------------------
        // MK
        //-----------------------------------------------------

        if (MkRegex.IsMatch(value))
            return TokenType.Mk;

        //-----------------------------------------------------
        // K020
        //-----------------------------------------------------

        if (KRegex.IsMatch(value))
            return TokenType.K;

        //-----------------------------------------------------
        // PCN
        //-----------------------------------------------------

        if (PcnRegex.IsMatch(value))
            return TokenType.Pcn;

        //-----------------------------------------------------
        // COLOR
        //-----------------------------------------------------

        if (ColorDictionary.Contains(value))
            return TokenType.Color;

        //-----------------------------------------------------
        // PURE NUMBER
        // 2113
        //-----------------------------------------------------

        //----------------------------------------------------

        if (BrandDictionary.Contains(value))
    return TokenType.Brand;

        //----------------------------------------------------
        //----------------------------------------------------

        if (PureNumberRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // XY35096
        // RD1007
        // P8315
        //-----------------------------------------------------

        if (LettersDigitsRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // P850-01 (letters+digits-hyphen-digits)
        //-----------------------------------------------------

        if (LettersDigitsHyphenDigitsRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // D2823-K026
        // AB102-K20
        // letters+digits-hyphen-letters+digits
        //-----------------------------------------------------

        if (LettersDigitsHyphenLettersDigitsRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // 3162P-01 (digits+letters-hyphen-digits)
        //-----------------------------------------------------

        if (DigitsLettersHyphenDigitsRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // 999K, 500K -> viết tắt tiền tệ (nghìn đồng), KHÔNG phải mã
        // sản phẩm (VD "KÍNH TẶNG HÓA ĐƠN 999K"). Chỉ loại riêng suffix
        // "K"; các suffix khác (P, R...) vẫn là mã hợp lệ như 6215P.
        //-----------------------------------------------------

        if (CurrencyLikeRegex.IsMatch(value))
            return TokenType.Word;

        //-----------------------------------------------------
        // 6215P (digits+letters, no hyphen)
        //-----------------------------------------------------

        if (DigitsLettersRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // 9805-01
        // 6250-2
        //-----------------------------------------------------

        if (DigitsHyphenDigitsRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // B305/147
        // 6233/66503
        //-----------------------------------------------------

        if (LetterDigitsSlashDigitsRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // KM-05-8820
        //-----------------------------------------------------

        if (KmDashRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // K05-17513-81K / Q07-10275-20C / 3485-47-18
        // Mã 3 đoạn nối gạch ngang (chữ+số/số thuần), mỗi đoạn alnum
        //-----------------------------------------------------

        if (ThreeSegmentDashRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // F1923B, F1736B (chữ+số+chữ, không gạch ngang)
        // F1923B-1, F1320A-2 (có thêm hậu tố -số phân biệt biến thể)
        //-----------------------------------------------------

        if (LettersDigitsLettersRegex.IsMatch(value))
            return TokenType.Code;

        //-----------------------------------------------------
        // GF-50501 (chữ thuần - số), 163-G57 / 622-K1170043
        // (số - chữ+số), 360-3A (số - số+chữ)
        //-----------------------------------------------------

        if (LettersDashDigitsRegex.IsMatch(value) ||
            DigitsDashLetterDigitsRegex.IsMatch(value) ||
            DigitsDashDigitsLetterRegex.IsMatch(value))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // DEFAULT
        //-----------------------------------------------------

        return TokenType.Word;
    }
}