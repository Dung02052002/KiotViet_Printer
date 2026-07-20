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