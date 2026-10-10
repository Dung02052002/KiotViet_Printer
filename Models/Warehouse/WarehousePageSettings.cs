using System.Globalization;

namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>Lề trang in, đơn vị INCH (đúng như Excel lưu trong file).</summary>
public sealed record WarehouseMargins(double Left, double Right, double Top, double Bottom, double Header, double Footer)
{
    public const double CentimetersPerInch = 2.54;

    public string Describe() => $"Trái {Cm(Left)} · Phải {Cm(Right)} · Trên {Cm(Top)} · Dưới {Cm(Bottom)} cm";

    public static string Cm(double inches) =>
        (inches * CentimetersPerInch).ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"));
}

/// <summary>
/// Thiết lập trang in của sheet phiếu, đọc từ file (mẫu hoặc kết quả).
/// Khi không co giãn theo trang (<see cref="FitToPage"/> = false), Excel in
/// theo <see cref="Scale"/> %; ngược lại co cho vừa
/// <see cref="FitWidth"/> × <see cref="FitHeight"/> trang (0 = tự do).
/// </summary>
public sealed record WarehousePageSetup
{
    /// <summary>Mã khổ giấy của Excel (1 = Letter, 9 = A4...). Xem <see cref="WarehousePaperSizes"/>.</summary>
    public required short PaperSize { get; init; }
    public required bool Landscape { get; init; }
    public required WarehouseMargins Margins { get; init; }
    public required bool FitToPage { get; init; }
    public required short Scale { get; init; }
    public required short FitWidth { get; init; }
    public required short FitHeight { get; init; }

    /// <summary>Vùng in (null = không đặt — Excel in toàn bộ vùng có dữ liệu).</summary>
    public string? PrintArea { get; init; }

    public required int ManualRowBreakCount { get; init; }
    public required bool HasHeaderFooter { get; init; }
    public required bool CenterHorizontally { get; init; }
    public required bool CenterVertically { get; init; }

    public string PaperName => WarehousePaperSizes.Describe(PaperSize);
    public string OrientationName => Landscape ? "Ngang" : "Dọc";

    public string ScaleDescription =>
        !FitToPage ? $"{Scale}%"
        : FitWidth == 1 && FitHeight == 0 ? "Vừa chiều rộng 1 trang"
        : FitWidth == 1 && FitHeight == 1 ? "Vừa một trang"
        : $"Vừa {FitPart(FitWidth)} trang ngang × {FitPart(FitHeight)} trang dọc";

    private static string FitPart(short pages) => pages == 0 ? "tự do" : pages.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Cách co giãn khi in mà người dùng chọn ở phần thiết lập in.</summary>
public enum WarehouseScaleMode
{
    /// <summary>In theo tỷ lệ % cố định.</summary>
    Percent,

    /// <summary>Co cho vừa chiều rộng 1 trang, chiều cao tự do (số trang theo nội dung).</summary>
    FitWidth,

    /// <summary>Co cả phiếu vào đúng 1 trang — chữ có thể rất nhỏ khi nhiều dòng.</summary>
    FitOnePage
}

public sealed record WarehouseScaleChoice(WarehouseScaleMode Mode, short Percent = 100);

/// <summary>
/// Những thiết lập trang in người dùng CHỦ ĐỘNG đổi so với file mẫu. Mục nào
/// null thì giữ nguyên như mẫu — không bao giờ "đặt lại" một giá trị chỉ vì
/// màn hình đang hiển thị nó.
/// </summary>
public sealed class WarehousePageSettings
{
    public short? PaperSize { get; init; }
    public bool? Landscape { get; init; }

    /// <summary>Chỉ lề Trái/Phải/Trên/Dưới; lề header/footer luôn giữ như mẫu.</summary>
    public WarehouseMargins? Margins { get; init; }

    public WarehouseScaleChoice? Scale { get; init; }

    public bool KeepsTemplate => PaperSize == null && Landscape == null && Margins == null && Scale == null;

    public static WarehousePageSettings KeepTemplate() => new();

    public const short MinScalePercent = 10;
    public const short MaxScalePercent = 400;

    /// <summary>Thiết lập trang in sẽ có trên file kết quả: mẫu + các mục đã đổi.</summary>
    public WarehousePageSetup ApplyTo(WarehousePageSetup template)
    {
        WarehousePageSetup result = template with
        {
            PaperSize = PaperSize ?? template.PaperSize,
            Landscape = Landscape ?? template.Landscape,
            Margins = Margins == null
                ? template.Margins
                : template.Margins with { Left = Margins.Left, Right = Margins.Right, Top = Margins.Top, Bottom = Margins.Bottom }
        };

        return Scale?.Mode switch
        {
            WarehouseScaleMode.Percent => result with { FitToPage = false, Scale = Scale.Percent },
            WarehouseScaleMode.FitWidth => result with { FitToPage = true, FitWidth = 1, FitHeight = 0 },
            WarehouseScaleMode.FitOnePage => result with { FitToPage = true, FitWidth = 1, FitHeight = 1 },
            _ => result
        };
    }

    /// <summary>Mô tả ngắn các mục đã đổi (để ghi log / hiện thông báo).</summary>
    public string Describe()
    {
        if (KeepsTemplate)
            return "giữ nguyên như mẫu";

        List<string> parts = new();
        if (PaperSize is short paper) parts.Add("khổ " + WarehousePaperSizes.Describe(paper));
        if (Landscape is bool landscape) parts.Add(landscape ? "giấy ngang" : "giấy dọc");
        if (Margins != null) parts.Add("lề " + Margins.Describe());
        if (Scale != null) parts.Add(Scale.Mode switch
        {
            WarehouseScaleMode.FitWidth => "vừa chiều rộng trang",
            WarehouseScaleMode.FitOnePage => "vừa một trang",
            _ => $"tỷ lệ {Scale.Percent}%"
        });
        return string.Join(", ", parts);
    }
}

/// <summary>Khổ giấy thông dụng (mã Excel/NPOI → tên + kích thước).</summary>
public static class WarehousePaperSizes
{
    public sealed record Paper(short Code, string Name, double WidthMm, double HeightMm)
    {
        public string Display => $"{Name} ({(WidthMm / 10).ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"))} × " +
                                 $"{(HeightMm / 10).ToString("0.##", CultureInfo.GetCultureInfo("vi-VN"))} cm)";
    }

    public static readonly IReadOnlyList<Paper> Common =
    [
        new(9, "A4", 210, 297),
        new(1, "Letter", 215.9, 279.4),
        new(11, "A5", 148, 210),
        new(8, "A3", 297, 420),
        new(5, "Legal", 215.9, 355.6),
        new(13, "B5 (JIS)", 182, 257),
        new(7, "Executive", 184.2, 266.7)
    ];

    public static Paper? Find(short code) => Common.FirstOrDefault(p => p.Code == code);

    public static string Describe(short code) => Find(code)?.Display ?? $"Khổ giấy mã {code} (theo máy in)";
}
