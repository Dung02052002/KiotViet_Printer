namespace KiotVietLabelPrinter.Models;

public enum OutputImageFormat
{
    Jpeg,
    WebP
}

public class CompressionOptions
{
    public CompressionQuality Quality { get; set; } = CompressionQuality.Balanced;
    public bool KeepOriginalDimensions { get; set; }
    public OutputImageFormat Format { get; set; } = OutputImageFormat.Jpeg;
    public string OutputFolder { get; set; } = "";
}
