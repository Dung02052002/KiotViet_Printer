using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Models.Barcodes;
using ZXing.Common;
using ZXing.OneD;

namespace KiotVietLabelPrinter.Services.Barcodes;

// Kết quả vẽ một mã vạch: ảnh (đã gắn DPI để in đúng kích thước thật) và các
// số đo thực tế sau khi làm tròn pixel — hiển thị lại cho người dùng.
public sealed class BarcodeImage : IDisposable
{
    public required Bitmap Bitmap { get; init; }
    public required string Code { get; init; }
    public string Name { get; init; } = "";
    public required int Dpi { get; init; }
    public required int ModulePx { get; init; }
    public required int BarsWidthPx { get; init; }

    public double WidthMm => Bitmap.Width * 25.4 / Dpi;
    public double HeightMm => Bitmap.Height * 25.4 / Dpi;
    public double ModuleWidthMm => ModulePx * 25.4 / Dpi;
    public double BarsWidthMm => BarsWidthPx * 25.4 / Dpi;

    public void Dispose() => Bitmap.Dispose();
}

// Service cho tính năng "Tạo mã vạch": mã hoá Code 128 bằng ZXing.Net, tự vẽ
// vạch bằng GDI+ theo đúng số nguyên pixel/vạch (không co giãn, không khử răng
// cưa) rồi đọc ngược lại chính ảnh vừa vẽ để bảo đảm máy quét ra đúng mã hàng.
// Chuỗi mã hàng được mã hoá NGUYÊN VĂN — không trim, không đổi hoa/thường,
// không thêm tiền tố/hậu tố; dòng chữ bên dưới chỉ là text hiển thị.
public class Code128BarcodeService
{
    public const int MaxCodeLength = 80;

    // Code 128 bắt buộc khoảng trắng hai bên tối thiểu 10 lần độ rộng vạch.
    private const int QuietZoneModules = 10;

    // Tem cao cố định: vạch được hạ trước về PreferredMinBarHeightMm (trước
    // khi thu nhỏ chữ), và không bao giờ thấp hơn MinBarHeightMm để còn quét được.
    private const double PreferredMinBarHeightMm = 8;
    private const double MinBarHeightMm = 5;

    // Tem rộng cố định: chữ mã hàng không bị thu nhỏ hơn mức này.
    private const float MinCodeFontSizePt = 4;

    /// <summary>
    /// Kiểm tra mã hàng có mã hoá được bằng Code 128 không. Trả về null nếu
    /// hợp lệ, ngược lại là thông báo lỗi tiếng Việt cho người dùng.
    /// </summary>
    public static string? Validate(string? code)
    {
        if (string.IsNullOrEmpty(code) || code.Trim().Length == 0)
            return "Vui lòng nhập mã hàng.";

        if (char.IsWhiteSpace(code[0]) || char.IsWhiteSpace(code[^1]))
            return "Mã hàng có khoảng trắng ở đầu hoặc cuối. Hãy xoá khoảng trắng rồi thử lại.";

        if (code.Length > MaxCodeLength)
            return $"Mã hàng dài {code.Length} ký tự, tối đa {MaxCodeLength} ký tự để mã vạch còn quét được.";

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            // ASCII in được (32..126). Ký tự có dấu tiếng Việt, tab, xuống
            // dòng... không thuộc bảng ký tự Code 128. ZXing còn coi 'ñ'..'ô'
            // là mã điều khiển FNC — cũng bị loại ở đây.
            if (c < 32 || c > 126)
            {
                string shown = char.IsControl(c) ? $"mã {(int)c}" : $"'{c}'";
                return $"Ký tự {shown} ở vị trí {i + 1} không mã hoá được bằng Code 128.\n" +
                       "Chỉ dùng chữ không dấu, số và ký hiệu thông thường.";
            }
        }

        return null;
    }

    /// <summary>
    /// Vẽ một tem theo bố cục cố định từ trên xuống: TÊN HÀNG (căn giữa, tự
    /// xuống dòng) → VẠCH CODE 128 (mã hoá đúng <paramref name="code"/>) → MÃ
    /// HÀNG bằng chữ. Tên hàng chỉ để hiển thị, không bao giờ đưa vào vạch;
    /// để trống thì tem chỉ còn vạch + mã hàng.
    /// </summary>
    public BarcodeImage Render(string code, string? name, BarcodeRenderOptions options)
    {
        string? error = Validate(code);
        if (error != null)
            throw new ArgumentException(error, nameof(code));

        int dpi = Math.Clamp(options.Dpi, 72, 1200);
        bool[] modules = new Code128Writer().encode(code);
        string nameText = (name ?? "").Trim();

        int labelWidth = options.LabelWidthMm > 0 ? Math.Max(1, (int)Math.Round(MmToPx(options.LabelWidthMm, dpi))) : 0;
        int labelHeight = options.LabelHeightMm > 0 ? Math.Max(1, (int)Math.Round(MmToPx(options.LabelHeightMm, dpi))) : 0;

        int modulePx = options.TargetWidthMm > 0
            ? Math.Max(1, (int)Math.Floor(MmToPx(options.TargetWidthMm, dpi) / modules.Length))
            : Math.Max(1, (int)Math.Round(MmToPx(options.ModuleWidthMm, dpi)));

        // Tem có chiều rộng cố định: thu nhỏ vạch (vẫn nguyên pixel/vạch) cho
        // vừa cả quiet zone hai bên — không để vạch tràn ra ngoài tem.
        if (labelWidth > 0)
        {
            while (modulePx > 1 && (modules.Length + (2 * QuietZoneModules)) * modulePx > labelWidth)
                modulePx--;

            if ((modules.Length + (2 * QuietZoneModules)) * modulePx > labelWidth)
            {
                double neededMm = (modules.Length + (2 * QuietZoneModules)) * 25.4 / dpi;
                throw new InvalidOperationException(
                    $"Tem rộng {options.LabelWidthMm:0.#} mm không đủ chỗ cho mã vạch \"{code}\" " +
                    $"(cần tối thiểu {neededMm:0.#} mm ở {dpi} DPI). Hãy tăng chiều rộng tem, tăng DPI hoặc rút ngắn mã hàng.");
            }
        }

        int barsWidth = modules.Length * modulePx;
        int quietZone = QuietZoneModules * modulePx;
        int barHeight = Math.Max(1, (int)Math.Round(MmToPx(options.BarHeightMm, dpi)));
        int marginX = Math.Max(0, (int)Math.Round(MmToPx(options.MarginHorizontalMm, dpi)));
        int marginY = Math.Max(0, (int)Math.Round(MmToPx(options.MarginVerticalMm, dpi)));
        int nameGap = Math.Max(0, (int)Math.Round(MmToPx(options.NameGapMm, dpi)));
        int codeGap = Math.Max(0, (int)Math.Round(MmToPx(options.TextGapMm, dpi)));

        // Lề lớn so với tem nhỏ thì "ăn" hết chỗ của chữ — giới hạn lề ở 1/10 tem.
        if (labelWidth > 0)
            marginX = Math.Min(marginX, labelWidth / 10);
        if (labelHeight > 0)
            marginY = Math.Min(marginY, labelHeight / 10);

        FontStyle style = options.Bold ? FontStyle.Bold : FontStyle.Regular;
        Font MakeFont(float pt) => CreateFont(options.FontFamily, pt * dpi / 72f, style);

        // StringFormat/Font là đối tượng GDI+ không an toàn đa luồng — tạo mới
        // mỗi lần vẽ vì tạo hàng loạt chạy trên luồng nền.
        using StringFormat codeFormat = CreateTextFormat();
        using StringFormat nameFormat = CreateWrappedTextFormat();
        using Bitmap probe = new(1, 1);
        probe.SetResolution(dpi, dpi);
        using Graphics pg = Graphics.FromImage(probe);
        pg.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        Font? codeFont = null;
        Font? nameFont = null;
        try
        {
            // ---- Mã hàng (dòng dưới vạch, một dòng, đúng nguyên văn) ----
            float codePt = options.FontSizePt;
            SizeF codeSize = SizeF.Empty;
            if (codePt > 0)
            {
                codeFont = MakeFont(codePt);
                codeSize = pg.MeasureString(code, codeFont, int.MaxValue, codeFormat);

                // Tem rộng cố định: mã dài thì giảm cỡ chữ cho vừa, không cắt chữ.
                while (labelWidth > 0 && codeSize.Width > labelWidth - (2 * marginX) && codePt > MinCodeFontSizePt)
                {
                    codePt = Math.Max(MinCodeFontSizePt, codePt - 0.5f);
                    codeFont.Dispose();
                    codeFont = MakeFont(codePt);
                    codeSize = pg.MeasureString(code, codeFont, int.MaxValue, codeFormat);
                }

                // Nhỏ nhất rồi vẫn tràn thì báo lỗi — không in tem bị cắt mất ký tự.
                if (labelWidth > 0 && codeSize.Width > labelWidth)
                    throw new InvalidOperationException(
                        $"Mã hàng \"{code}\" quá dài để in chữ vừa tem rộng {options.LabelWidthMm:0.#} mm. " +
                        "Hãy tăng chiều rộng tem hoặc để Rộng tem = 0 (tự động).");
            }

            int codeHeight = (int)Math.Ceiling(codeSize.Height);

            // Tem tự động: chữ mã hàng dài hơn phần vạch thì nới ảnh ra, không
            // bao giờ cắt mất chữ/vạch. Tên hàng KHÔNG nới ảnh — nó xuống dòng.
            int width = labelWidth > 0
                ? labelWidth
                : Math.Max(barsWidth + (2 * Math.Max(marginX, quietZone)), (int)Math.Ceiling(codeSize.Width) + (2 * marginX));
            int textAreaWidth = Math.Max(1, width - (2 * marginX));

            // ---- Tên hàng (phía trên vạch, căn giữa, tự xuống dòng) ----
            float namePt = options.NameFontSizePt;
            if (nameText.Length > 0 && namePt > 0)
                nameFont = MakeFont(namePt);

            int MeasureName() => nameFont == null
                ? 0
                : (int)Math.Ceiling(pg.MeasureString(nameText, nameFont, new SizeF(textAreaWidth, 100000f), nameFormat).Height);

            int nameHeight = MeasureName();

            int ContentHeight() =>
                (nameHeight > 0 ? nameHeight + nameGap : 0) + barHeight + (codeFont != null ? codeGap + codeHeight : 0);

            // Tem cao cố định mà không đủ chỗ, co theo thứ tự để tem cân đối:
            // 1) hạ vạch về PreferredMinBarHeightMm (vẫn quét tốt), 2) giảm cỡ
            // chữ tên hàng, 3) hạ vạch tiếp về MinBarHeightMm, 4) báo lỗi.
            if (labelHeight > 0)
            {
                int available = labelHeight - (2 * marginY);

                void ShrinkBarTo(double floorMm)
                {
                    int floor = Math.Min(barHeight, Math.Max(1, (int)Math.Round(MmToPx(floorMm, dpi))));
                    if (ContentHeight() > available)
                        barHeight = Math.Max(floor, barHeight - (ContentHeight() - available));
                }

                ShrinkBarTo(PreferredMinBarHeightMm);

                while (nameFont != null && ContentHeight() > available && namePt > BarcodeRenderOptions.MinNameFontSizePt)
                {
                    namePt = Math.Max(BarcodeRenderOptions.MinNameFontSizePt, namePt - 0.5f);
                    nameFont.Dispose();
                    nameFont = MakeFont(namePt);
                    nameHeight = MeasureName();
                }

                ShrinkBarTo(MinBarHeightMm);

                if (ContentHeight() > available)
                    throw new InvalidOperationException(
                        $"Tem cao {options.LabelHeightMm:0.#} mm không đủ chỗ cho tên hàng + mã vạch + mã hàng \"{code}\". " +
                        "Hãy tăng chiều cao tem, giảm cỡ chữ / khoảng cách hoặc rút ngắn tên hàng.");
            }

            int height = labelHeight > 0 ? labelHeight : ContentHeight() + (2 * marginY);
            int top = labelHeight > 0 ? (labelHeight - ContentHeight()) / 2 : marginY;
            int barsTop = top + (nameHeight > 0 ? nameHeight + nameGap : 0);

            Bitmap bitmap = new(width, height, PixelFormat.Format24bppRgb);
            try
            {
                bitmap.SetResolution(dpi, dpi);

                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.White);
                    g.SmoothingMode = SmoothingMode.None;
                    g.PixelOffsetMode = PixelOffsetMode.None;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    // 1. Tên hàng
                    if (nameFont != null)
                        g.DrawString(nameText, nameFont, Brushes.Black, new RectangleF(marginX, top, textAreaWidth, nameHeight), nameFormat);

                    // 2. Vạch
                    int barsLeft = (width - barsWidth) / 2;
                    DrawBars(g, modules, barsLeft, barsTop, modulePx, barHeight);

                    // 3. Mã hàng
                    if (codeFont != null)
                    {
                        float textLeft = (width - codeSize.Width) / 2f;
                        float textTop = barsTop + barHeight + codeGap;
                        g.DrawString(code, codeFont, Brushes.Black, textLeft, textTop, codeFormat);
                    }
                }

                VerifyRendered(bitmap, barsTop + (barHeight / 2), code);

                return new BarcodeImage
                {
                    Bitmap = bitmap,
                    Code = code,
                    Name = nameText,
                    Dpi = dpi,
                    ModulePx = modulePx,
                    BarsWidthPx = barsWidth
                };
            }
            catch
            {
                bitmap.Dispose();
                throw;
            }
        }
        finally
        {
            codeFont?.Dispose();
            nameFont?.Dispose();
        }
    }

    private static StringFormat CreateTextFormat()
    {
        StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
        return format;
    }

    // Tên hàng: căn giữa từng dòng, xuống dòng theo từ (từ quá dài thì GDI+
    // ngắt giữa từ), không cắt "..." và không clip dòng cuối.
    private static StringFormat CreateWrappedTextFormat()
    {
        StringFormat format = (StringFormat)StringFormat.GenericTypographic.Clone();
        format.FormatFlags &= ~StringFormatFlags.NoWrap;
        format.FormatFlags |= StringFormatFlags.NoClip;
        format.Alignment = StringAlignment.Center;
        format.Trimming = StringTrimming.None;
        return format;
    }

    // Font không có kiểu đậm (hoặc không có trên máy) thì lùi dần về kiểu
    // thường / font sans-serif mặc định thay vì báo lỗi.
    private static Font CreateFont(string family, float sizePx, FontStyle style)
    {
        string name = string.IsNullOrWhiteSpace(family) ? "Arial" : family;
        try
        {
            FontStyle usable;
            using (FontFamily fontFamily = new(name))
                usable = fontFamily.IsStyleAvailable(style) ? style : FontStyle.Regular;

            return new Font(name, sizePx, usable, GraphicsUnit.Pixel);
        }
        catch (ArgumentException)
        {
            // Font không có trên máy (vd. config mang từ máy khác sang).
            return new Font(FontFamily.GenericSansSerif, sizePx, style, GraphicsUnit.Pixel);
        }
    }

    // Gộp các vạch đen liền nhau thành một hình chữ nhật — toạ độ đều là số
    // nguyên pixel nên cạnh vạch sắc, không có pixel xám ở mép.
    private static void DrawBars(Graphics g, bool[] modules, int left, int top, int modulePx, int barHeight)
    {
        int i = 0;
        while (i < modules.Length)
        {
            if (!modules[i])
            {
                i++;
                continue;
            }

            int start = i;
            while (i < modules.Length && modules[i])
                i++;

            g.FillRectangle(Brushes.Black, left + (start * modulePx), top, (i - start) * modulePx, barHeight);
        }
    }

    /// <summary>
    /// Đọc lại một hàng pixel ngang qua giữa phần vạch của ảnh vừa vẽ bằng bộ
    /// giải mã Code 128 của ZXing, và so khớp TUYỆT ĐỐI với mã hàng gốc.
    /// </summary>
    private static void VerifyRendered(Bitmap bitmap, int rowY, string expected)
    {
        int width = bitmap.Width;
        BitArray row = new(width);

        BitmapData data = bitmap.LockBits(new Rectangle(0, rowY, width, 1), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            byte[] pixels = new byte[width * 3];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            for (int x = 0; x < width; x++)
            {
                if (pixels[x * 3] < 128)
                    row[x] = true;
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        string? decoded = null;
        try
        {
            decoded = new Code128Reader().decodeRow(0, row, null)?.Text;
        }
        catch (ZXing.ReaderException)
        {
            // ZXing báo "không tìm thấy mã"/sai checksum bằng exception: decoded
            // giữ null và được báo lỗi rõ ràng ngay bên dưới.
            decoded = null;
        }

        if (!string.Equals(decoded, expected, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Kiểm tra lại mã vạch vừa tạo không khớp (đọc được: \"{decoded ?? "(không đọc được)"}\"). " +
                "Hãy tăng độ rộng vạch hoặc độ phân giải rồi thử lại.");
    }

    public static void Save(BarcodeImage image, string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
            Directory.CreateDirectory(folder);

        // Mã hoá ảnh vào bộ nhớ trước, xong mới ghi file: lỗi giữa chừng không
        // để lại file ảnh hỏng mang đúng tên mã hàng. Ghi qua FileStream thay
        // vì Bitmap.Save(path): GDI+ chỉ báo "A generic error occurred" khi
        // đường dẫn dài/không có quyền ghi, còn FileStream báo lỗi rõ ràng.
        using MemoryStream buffer = new();
        string ext = Path.GetExtension(path);
        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            ImageCodecInfo jpeg = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
            using EncoderParameters parameters = new(1);
            parameters.Param[0] = new EncoderParameter(Encoder.Quality, 100L);
            image.Bitmap.Save(buffer, jpeg, parameters);
        }
        else
        {
            image.Bitmap.Save(buffer, ImageFormat.Png);
        }

        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        buffer.Position = 0;
        buffer.CopyTo(stream);
    }

    private static readonly HashSet<string> ReservedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // Giới hạn độ dài tên file để đường dẫn đầy đủ không vượt MAX_PATH khi
    // tên hàng quá dài.
    private const int MaxFileNameLength = 120;

    /// <summary>
    /// Tên file lấy theo tên hàng; dòng không có tên hàng thì dùng mã hàng.
    /// Chỉ những ký tự Windows cấm trong tên file (\ / : * ? " &lt; &gt; |)
    /// mới được thay bằng '_'. Nội dung mã vạch không bị ảnh hưởng.
    /// </summary>
    public static string ToFileName(string productName, string code)
    {
        string source = string.Join(' ', productName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (source.Length == 0)
            source = code;

        char[] invalid = Path.GetInvalidFileNameChars();
        string name = new(source.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        if (name.Length > MaxFileNameLength)
            name = name[..MaxFileNameLength];
        name = name.Trim().TrimEnd('.', ' ');

        if (name.Length == 0)
            name = "_";
        if (ReservedFileNames.Contains(name))
            name += "_";

        return name;
    }

    /// <summary>
    /// Đọc file CSV/TXT thành bảng chữ (mỗi dòng một danh sách ô) để đưa vào
    /// ParseLabelTable. File Excel đi qua ExcelService.ReadTextTable.
    /// </summary>
    public static List<List<string>> ReadTableFromCsv(string path)
    {
        string[] lines = ReadCsvLines(path);
        List<List<string>> rows = new();
        if (lines.Length == 0)
            return rows;

        char separator = DetectSeparator(lines[0]);
        foreach (string line in lines)
            rows.Add(SplitCsvLine(line, separator));

        return rows;
    }

    // Excel "Lưu thành CSV" ghi theo bảng mã ANSI của Windows (tiếng Việt là
    // 1258), không phải UTF-8 — đọc UTF-8 trước, sai thì lùi về bảng mã ANSI
    // để tên hàng có dấu không bị lỗi font.
    private static string[] ReadCsvLines(string path)
    {
        // FileShare.ReadWrite: vẫn đọc được khi file đang mở trong Excel.
        byte[] bytes;
        using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (MemoryStream buffer = new())
        {
            stream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        string text;
        try
        {
            text = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            // Không theo bảng mã của máy (Windows tiếng Anh là 1252, không có
            // "đ/ơ/ư"): dữ liệu của app là tiếng Việt nên lùi về 1258. Bảng mã
            // 1258 ghi dấu thanh bằng ký tự tổ hợp — chuẩn hoá về dạng dựng sẵn.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            text = System.Text.Encoding.GetEncoding(1258).GetString(bytes).Normalize(System.Text.NormalizationForm.FormC);
        }

        text = text.TrimStart('﻿');
        return text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
    }

    /// <summary>
    /// Chọn cột Tên hàng / Mã hàng từ bảng đọc được (Excel hoặc CSV):
    /// <list type="bullet">
    /// <item>Dòng 1 có tiêu đề "Tên hàng"/"Mã hàng"/... (file xuất từ KiotViet)
    /// thì lấy đúng các cột đó từ dòng 2.</item>
    /// <item>Không có tiêu đề: cột 1 = Tên hàng, cột 2 = Mã hàng.</item>
    /// <item>File chỉ có một cột (định dạng cũ): cột đó là Mã hàng.</item>
    /// </list>
    /// Dòng trống bị bỏ qua; dòng có tên mà thiếu mã vẫn giữ lại để người dùng
    /// thấy và sửa (in/tạo sẽ báo lỗi đúng dòng đó).
    /// </summary>
    public static List<BarcodeLabelItem> ParseLabelTable(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        List<BarcodeLabelItem> items = new();
        if (rows.Count == 0)
            return items;

        IReadOnlyList<string> header = rows[0];
        int nameColumn = FindIndex(header, IsNameHeader);
        int codeColumn = FindIndex(header, IsCodeHeader);
        bool hasHeader = nameColumn >= 0 || codeColumn >= 0;
        int start = hasHeader ? 1 : 0;

        if (hasHeader)
        {
            // Chỉ nhận ra một trong hai tiêu đề: cột còn lại theo thứ tự mặc định.
            if (codeColumn < 0)
                codeColumn = nameColumn == 1 ? 0 : 1;
            if (nameColumn < 0)
                nameColumn = codeColumn == 0 ? -1 : 0;
        }
        else
        {
            bool singleColumn = rows.All(r => r.Skip(1).All(c => string.IsNullOrWhiteSpace(c)));
            nameColumn = singleColumn ? -1 : 0;
            codeColumn = singleColumn ? 0 : 1;
        }

        for (int i = start; i < rows.Count; i++)
        {
            IReadOnlyList<string> row = rows[i];
            string name = nameColumn >= 0 && nameColumn < row.Count ? row[nameColumn].Trim() : "";
            string code = codeColumn < row.Count ? row[codeColumn].Trim() : "";
            if (name.Length > 0 || code.Length > 0)
                items.Add(new BarcodeLabelItem(name, code));
        }

        return items;
    }

    private static int FindIndex(IReadOnlyList<string> cells, Func<string?, bool> match)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (match(cells[i]))
                return i;
        }

        return -1;
    }

    public static bool IsNameHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return false;

        string h = header.Trim().Trim('﻿').ToLowerInvariant().Replace('_', ' ');
        return h is "tên hàng" or "ten hang" or "tenhang" or "tên hàng hóa" or "tên hàng hoá" or "ten hang hoa"
            or "tên sp" or "ten sp" or "tên sản phẩm" or "ten san pham" or "tên" or "ten" or "name" or "product name";
    }

    public static bool IsCodeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return false;

        string h = header.Trim().Trim('﻿').ToLowerInvariant().Replace('_', ' ');
        return h is "mã hàng" or "ma hang" or "mahang" or "mã sp" or "ma sp" or "mã sản phẩm" or "ma san pham"
            or "sku" or "code" or "product code" or "barcode" or "mã vạch" or "ma vach";
    }

    private static char DetectSeparator(string headerLine)
    {
        int commas = headerLine.Count(c => c == ',');
        int semicolons = headerLine.Count(c => c == ';');
        int tabs = headerLine.Count(c => c == '\t');
        int pipes = headerLine.Count(c => c == '|');

        if (tabs > commas && tabs > semicolons && tabs >= pipes) return '\t';
        if (pipes > commas && pipes > semicolons) return '|';
        return semicolons > commas ? ';' : ',';
    }

    private static List<string> SplitCsvLine(string line, char separator)
    {
        List<string> cells = new();
        System.Text.StringBuilder current = new();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == separator)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }

    private static double MmToPx(double mm, int dpi) => mm * dpi / 25.4;
}
