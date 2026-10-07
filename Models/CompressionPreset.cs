namespace KiotVietLabelPrinter.Models;

public enum CompressionQuality
{
    High,
    Balanced,
    Strong
}

// 3 preset nén — số liệu lấy từ file HTML tham khảo (Giảm dung lượng ảnh.html):
// High 92%/2400px, Medium 75%/1600px, Low 55%/1000px. Tách thành 1 nơi duy nhất
// (không hard-code rải rác) để đổi số liệu chỉ cần sửa ở đây.
public sealed class CompressionPreset
{
    public required CompressionQuality Id { get; init; }
    public required string Name { get; init; }
    public required int JpegQuality { get; init; }
    public required int MaxEdge { get; init; }

    public string Spec => $"{JpegQuality}% · Cạnh tối đa {MaxEdge}px";

    public static readonly CompressionPreset High = new()
    {
        Id = CompressionQuality.High,
        Name = "CAO",
        JpegQuality = 92,
        MaxEdge = 2400
    };

    public static readonly CompressionPreset Balanced = new()
    {
        Id = CompressionQuality.Balanced,
        Name = "CÂN BẰNG",
        JpegQuality = 75,
        MaxEdge = 1600
    };

    public static readonly CompressionPreset Strong = new()
    {
        Id = CompressionQuality.Strong,
        Name = "NÉN MẠNH",
        JpegQuality = 55,
        MaxEdge = 1000
    };

    public static readonly IReadOnlyList<CompressionPreset> All = new[] { High, Balanced, Strong };

    public static CompressionPreset Get(CompressionQuality id) => id switch
    {
        CompressionQuality.High => High,
        CompressionQuality.Strong => Strong,
        _ => Balanced
    };
}
