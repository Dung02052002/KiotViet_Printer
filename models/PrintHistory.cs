namespace KiotVietLabelPrinter.Models;

public class PrintHistory
{
    public DateTime PrintTime { get; set; }

    public string SourceExcelFile { get; set; } = "";

    public bool PrintedFullLabel { get; set; }

    public bool PrintedBarcodeLabel { get; set; }

    public string EmployeeCode { get; set; } = "";

    public int ProductCount { get; set; }

    public double TotalLabels { get; set; }

    public string MachineName { get; set; } = "";

    public string UserName { get; set; } = "";
}