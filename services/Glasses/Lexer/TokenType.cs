namespace KiotVietLabelPrinter.Services.Glasses.Lexer;

public enum TokenType
{
    Unknown = 0,

    //------------------------------------
    // Common
    //------------------------------------

    Word,

    Number,

    Code,

    Separator,

    //------------------------------------
    // Keyword
    //------------------------------------

    Keyword,

    Model,

    //------------------------------------
    // Glasses
    //------------------------------------

    Mk,

    K,

    Pcn,

    Brand,

    Color
}