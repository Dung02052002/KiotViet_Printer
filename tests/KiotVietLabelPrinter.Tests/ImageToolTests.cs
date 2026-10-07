using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class ImageToolTests
{
    private static string PixelHash(Bitmap bmp)
    {
        Rectangle rect = new(0, 0, bmp.Width, bmp.Height);
        BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            byte[] buffer = new byte[Math.Abs(data.Stride) * bmp.Height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            return Convert.ToHexString(SHA256.HashData(buffer))[..16];
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    [Fact]
    public void Code128_Validate()
    {
        string?[] inputs = [null, "", "   ", " AB", "AB ", "ABC-123", "12345678", "Mã có dấu", "a\tb", new string('9', 80), new string('9', 81), "TL12-MẪU 4", "abc/DEF.ghi_01"];
        Snapshot.Match(inputs.Select(i => new { Input = i, Error = Code128BarcodeService.Validate(i) }).ToList());
    }

    [Fact]
    public void Code128_Render_Variants()
    {
        Code128BarcodeService service = new();
        var cases = new (string Code, string? Name, BarcodeRenderOptions Options)[]
        {
            ("ABC-123", "Kính mát nam", new BarcodeRenderOptions()),
            ("12345678", null, new BarcodeRenderOptions()),
            ("1234567890123", "", new BarcodeRenderOptions { ModuleWidthMm = 0.25, BarHeightMm = 8, Bold = false }),
            ("SP001", "Tên hàng rất dài cần phải tự xuống dòng khi tem có chiều rộng cố định nhỏ", new BarcodeRenderOptions { LabelWidthMm = 40, LabelHeightMm = 25 }),
            ("tl10mau01", "Thắt lưng", new BarcodeRenderOptions { TargetWidthMm = 30, Dpi = 203 })
        };

        Snapshot.Match(cases.Select(c =>
        {
            using BarcodeImage img = service.Render(c.Code, c.Name, c.Options);
            return new
            {
                c.Code,
                img.Bitmap.Width,
                img.Bitmap.Height,
                img.ModulePx,
                img.BarsWidthPx,
                img.Dpi,
                Pixels = PixelHash(img.Bitmap)
            };
        }).ToList());
    }

    [Fact]
    public void Code128_FileNames_AndTables()
    {
        string dir = TestData.NewTempDir();
        string csv = Path.Combine(dir, "ds.csv");
        File.WriteAllText(csv,
            "Tên hàng,Mã hàng\nKính mát,SP001\n\"Gọng, nhựa\",00123\n,\nThắt lưng;x,TL10\n",
            new System.Text.UTF8Encoding(true));

        List<List<string>> table = Code128BarcodeService.ReadTableFromCsv(csv);

        Snapshot.Match(new
        {
            FileNames = new[] { "ABC-123", "a/b\\c:d*e?f\"g<h>i|j", "  x  ", "TL12-MẪU 4" }
                .Select(Code128BarcodeService.ToFileName).ToList(),
            Csv = table,
            Items = Code128BarcodeService.ParseLabelTable(table),
            NoHeader = Code128BarcodeService.ParseLabelTable([["A1", "Tên A"], ["B2", "Tên B"]]),
            Headers = new[] { "Tên hàng", "ten hang", "Mã hàng", "MA HANG", "Mã vạch", "Barcode", "Name", "Code", "Khác" }
                .Select(h => new { h, Name = Code128BarcodeService.IsNameHeader(h), Code = Code128BarcodeService.IsCodeHeader(h) })
                .ToList()
        });
    }

    [Fact]
    public void ImageCompression_JpegAndWebP()
    {
        string dir = TestData.NewTempDir();
        string input = Path.Combine(dir, "input.png");
        using (Bitmap bmp = new(2000, 1200))
        {
            using Graphics g = Graphics.FromImage(bmp);
            using LinearGradientBrush brush = new(new Rectangle(0, 0, 2000, 1200), Color.Navy, Color.Orange, 30f);
            g.FillRectangle(brush, 0, 0, 2000, 1200);
            g.FillEllipse(Brushes.White, 400, 300, 800, 500);
            bmp.Save(input, ImageFormat.Png);
        }

        string outDir = Path.Combine(dir, "out");
        ImageCompressionService service = new();
        var results = new[]
        {
            new CompressionOptions { Quality = CompressionQuality.Balanced, Format = OutputImageFormat.Jpeg, OutputFolder = outDir },
            new CompressionOptions { Quality = CompressionQuality.Strong, Format = OutputImageFormat.WebP, OutputFolder = outDir },
            new CompressionOptions { Quality = CompressionQuality.High, KeepOriginalDimensions = true, OutputFolder = outDir }
        }.Select(o =>
        {
            CompressionResult r = service.Compress(input, o, CancellationToken.None);
            return new
            {
                o.Quality,
                o.Format,
                r.Success,
                r.Error,
                r.OriginalWidth,
                r.OriginalHeight,
                r.OutputWidth,
                r.OutputHeight,
                r.OutputBytes,
                r.OriginalKept,
                OutputName = Path.GetFileName(r.OutputPath)
            };
        }).ToList();

        using Bitmap? thumb = ImageCompressionService.TryDecodeThumbnail(input, 200, out int w, out int h);
        Snapshot.Match(new { results, Thumb = new { thumb?.Width, thumb?.Height, w, h } });
    }
}
