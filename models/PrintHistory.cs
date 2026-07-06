namespace KiotVietLabelPrinter.Models;

public class PrintHistory
{
    public DateTime PrintTime { get; set; } = DateTime.Now;

    public string SourceExcelFile { get; set; } = "";
    public string LabelCode { get; set; } = "";
    public string LabelName { get; set; } = "";
    public string EmployeeCode { get; set; } = "";

    public int ProductCount { get; set; }
    public double TotalLabels { get; set; }

    public string MachineName { get; set; } = "";
    public string UserName { get; set; } = "";
}