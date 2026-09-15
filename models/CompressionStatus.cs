namespace KiotVietLabelPrinter.Models;

public enum CompressionStatus
{
    Waiting,
    Compressing,
    Done,
    OriginalKept,
    Error,
    Cancelled
}
