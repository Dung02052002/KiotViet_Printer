namespace KiotVietLabelPrinter.Models;

public class BackgroundRemovalItem
{
    public string FilePath { get; set; } = "";
    public string FileName => Path.GetFileName(FilePath);

    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution => Width > 0 && Height > 0 ? $"{Width} × {Height}" : "";

    public ImageProcessStatus Status { get; set; } = ImageProcessStatus.Waiting;
    public string? ResultPath { get; set; }
    public string? ErrorMessage { get; set; }

    public string StatusText => Status switch
    {
        ImageProcessStatus.Waiting => "Waiting",
        ImageProcessStatus.Processing => "Processing",
        ImageProcessStatus.Done => "Done",
        ImageProcessStatus.Error => "Error",
        _ => ""
    };
}
