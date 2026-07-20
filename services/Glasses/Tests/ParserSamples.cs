namespace KiotVietLabelPrinter.Services.Glasses.Tests;

public static class ParserSamples
{
    public static List<ParserTestCase> Get()
    {
        return
        [
            new()
            {
                Name="MODEL",
                Input="MODEL 6282",
                Expected="6282"
            },

            new()
            {
                Name="MODEL-2",
                Input="MODEL P850-01",
                Expected="P850-01"
            },

            new()
            {
                Name="LEFT MK",
                Input="2113-MK108",
                Expected="2113"
            },

            new()
            {
                Name="LEFT MK 2",
                Input="RD1007-MK228",
                Expected="RD1007"
            },

            new()
            {
                Name="LEFT MK 3",
                Input="6233/66503-MK221",
                Expected="6233/66503"
            },

            new()
            {
                Name="LEFT MK 4",
                Input="6250-2-MK112",
                Expected="6250-2"
            },

            new()
            {
                Name="RIGHT MK",
                Input="MK109-P8315",
                Expected="P8315"
            },

            new()
            {
                Name="RIGHT MK 2",
                Input="MK061-AB102",
                Expected="AB102"
            },

            new()
            {
                Name="MK ONLY",
                Input="MK081",
                Expected="MK081"
            },

            new()
            {
                Name="K ONLY",
                Input="K020",
                Expected="K020"
            },

            new()
            {
                Name="PCN",
                Input="PCN15",
                Expected="PCN15"
            },

            new()
            {
                Name="CODE",
                Input="XY35096",
                Expected="XY35096"
            },

            new()
            {
                Name="NUMBER",
                Input="2113",
                Expected="2113"
            },

            new()
            {
                Name="MÃ",
                Input="MÃ P8315",
                Expected="P8315"
            },

            new()
            {
                Name="CODE KEYWORD",
                Input="CODE AB102",
                Expected="AB102"
            }
        ];
    }
}