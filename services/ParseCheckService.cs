using System.Text.RegularExpressions;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

public class ParseCheckRow
{
    public string ProductCode { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string ProductNameWithAttr { get; set; } = "";
    public string ParsedCode { get; set; } = "";
    public string FinalCode { get; set; } = "";
    public string Flags { get; set; } = "";

    public bool HasFlag => !string.IsNullOrWhiteSpace(Flags);
}

public static class ParseCheckService
{
    public static List<ParseCheckRow> Build(List<PreviewRow> rows)
    {
        return rows
            .Select(row => new ParseCheckRow
            {
                ProductCode = row.ProductCode,
                ProductName = row.ProductName,
                ProductNameWithAttr = row.ProductNameWithAttr,
                ParsedCode = row.ParsedBarcodeCode,
                FinalCode = row.FinalBarcodeCode,
                Flags = string.Join(" | ", ComputeFlags(row))
            })
            .ToList();
    }

    private static List<string> ComputeFlags(PreviewRow row)
    {
        List<string> flags = [];

        string parsed = (row.ParsedBarcodeCode ?? "").Trim();
        string productCode = (row.ProductCode ?? "").Trim();
        string final = (row.FinalBarcodeCode ?? "").Trim();

        // Mã "gốc" thực sự dùng để in: ưu tiên mã tách được từ tên,
        // không có thì các handler đều tự rơi về Mã hàng gốc KiotViet.
        string core = !string.IsNullOrWhiteSpace(parsed) ? parsed : productCode;

        if (string.IsNullOrWhiteSpace(core))
        {
            flags.Add("Không tách được mã, Mã hàng gốc cũng trống");
            return flags;
        }

        if (string.IsNullOrWhiteSpace(parsed))
        {
            flags.Add("Không tách được mã từ tên, đang dùng Mã hàng gốc");
        }

        if (core.Length <= 2)
            flags.Add("Mã quá ngắn, dễ trùng");

        if (core.Length > 20)
            flags.Add("Mã quá dài, có thể lẫn chữ thừa");

        if (Regex.IsMatch(core, @"[^A-Za-z0-9\-/xX]"))
            flags.Add("Mã chứa ký tự lạ");

        if (string.IsNullOrWhiteSpace(final))
            flags.Add("Mã in cuối bị trống");

        return flags;
    }
}
