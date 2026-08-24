using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Services.Glasses.Dictionaries;

namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public static partial class GlassesLexer
{
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

        if (Regex.IsMatch(
            value,
            @"^MK\d+$"))
        {
            return TokenType.Mk;
        }

        //-----------------------------------------------------
        // K020
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^K\d+$"))
        {
            return TokenType.K;
        }

        //-----------------------------------------------------
        // PCN
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^PCN\d*$"))
        {
            return TokenType.Pcn;
        }

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

        if (Regex.IsMatch(
            value,
            @"^\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // XY35096
        // RD1007
        // P8315
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^[A-Z]{1,6}\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // P850-01 (letters+digits-hyphen-digits)
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^[A-Z]{1,6}\d+-\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // D2823-K026
        // AB102-K20
        // letters+digits-hyphen-letters+digits
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^[A-Z]{1,6}\d+-[A-Z]{1,6}\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // 3162P-01 (digits+letters-hyphen-digits)
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^\d+[A-Z]{1,6}-\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // 999K, 500K -> viết tắt tiền tệ (nghìn đồng), KHÔNG phải mã
        // sản phẩm (VD "KÍNH TẶNG HÓA ĐƠN 999K"). Chỉ loại riêng suffix
        // "K"; các suffix khác (P, R...) vẫn là mã hợp lệ như 6215P.
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^\d+K$"))
        {
            return TokenType.Word;
        }

        //-----------------------------------------------------
        // 6215P (digits+letters, no hyphen)
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^\d+[A-Z]{1,6}$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // 9805-01
        // 6250-2
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^\d+-\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // B305/147
        // 6233/66503
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^[A-Z]?\d+/\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // KM-05-8820
        //-----------------------------------------------------

        if (Regex.IsMatch(
            value,
            @"^[A-Z]{2,6}-\d{2}-\d+$"))
        {
            return TokenType.Code;
        }

        //-----------------------------------------------------
        // DEFAULT
        //-----------------------------------------------------

        return TokenType.Word;
    }
}