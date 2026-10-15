using KiotVietLabelPrinter.Infrastructure.BarTender.Backends;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Models;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class ExcelAndXmlTests
{
    [Fact]
    public void ReadProducts_FromKiotVietExport()
    {
        string source = TestData.CreateKiotVietSource(TestData.NewTempDir());
        Snapshot.Match(new ExcelService().ReadProducts(source));
    }

    // Đọc xong phải trả file ngay: handle còn giữ (dù FileShare.ReadWrite)
    // khiến Excel không lưu đè / người dùng không xoá, đổi tên được file nguồn
    // cho tới khi GC chạy.
    [Fact]
    public void ExcelService_ReleasesFileHandles_AfterReadAndWrite()
    {
        string dir = TestData.NewTempDir();
        string source = TestData.CreateKiotVietSource(dir);
        string target = TestData.CreateBarTenderDataTemplate(dir, "data.xls");
        ExcelService service = new();

        service.ReadProducts(source);
        service.ReadTextTable(source);
        service.WriteBarcodeLikeData(source, target, "NV1");

        using (new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        using (new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [Fact]
    public void ReadTextTable_FromKiotVietExport()
    {
        string source = TestData.CreateKiotVietSource(TestData.NewTempDir());
        Snapshot.Match(new ExcelService().ReadTextTable(source));
    }

    [Fact]
    public void WriteGenericLabelData_KeepPrice()
    {
        string dir = TestData.NewTempDir();
        string source = TestData.CreateKiotVietSource(dir);
        string target = TestData.CreateBarTenderDataTemplate(dir, "data.xls");

        new ExcelService().WriteGenericLabelData(source, target);

        Snapshot.Match(TestData.ReadAllCells(target));
    }

    [Fact]
    public void WriteGenericLabelData_UniformPrice_AndNameOverrides()
    {
        string dir = TestData.NewTempDir();
        string source = TestData.CreateKiotVietSource(dir);
        string target = TestData.CreateBarTenderDataTemplate(dir, "data.xls");

        new ExcelService().WriteGenericLabelData(source, target,
            new PriceOverride { Mode = PriceOverrideMode.Uniform, UniformPrice = 55000 },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["SP001"] = "Tên đã sửa" });

        Snapshot.Match(TestData.ReadAllCells(target));
    }

    [Fact]
    public void WriteGenericLabelData_PerProductPrice()
    {
        string dir = TestData.NewTempDir();
        string source = TestData.CreateKiotVietSource(dir);
        string target = TestData.CreateBarTenderDataTemplate(dir, "data.xls");

        PriceOverride price = new() { Mode = PriceOverrideMode.PerProduct };
        price.ProductOverrides["00123"] = 1;
        price.ProductOverrides["TL001"] = 77000;

        new ExcelService().WriteGenericLabelData(source, target, price);

        Snapshot.Match(TestData.ReadAllCells(target));
    }

    [Fact]
    public void WriteBarcodeLikeData_WithEmployeeCode()
    {
        string dir = TestData.NewTempDir();
        string source = TestData.CreateKiotVietSource(dir);
        string target = TestData.CreateBarTenderDataTemplate(dir, "data.xls");

        new ExcelService().WriteBarcodeLikeData(source, target, "NV7");

        Snapshot.Match(TestData.ReadAllCells(target));
    }

    [Fact]
    public void ParseNumberText_Cases()
    {
        string?[] inputs = [null, "", "  ", "150.000", "1,250", "2.5", "1,5", "1.234.567", "1,234,567.89", "1.234.567,89", "150.000 VNĐ", "-20", "abc", "12 VND", "0", "1e3"];
        Snapshot.Match(inputs.Select(i => new { Input = i, Value = ExcelService.ParseNumberText(i) }).ToList());
    }

    [Fact]
    public void PrintXml_WithNamedSubStrings_AndEscaping()
    {
        Snapshot.Match(new[]
        {
            PrintXmlBuilder.BuildXml(@"D:\In Tem\Tem Kinh\TEM <KINH> & co.btw",
                new Dictionary<string, string> { ["GLASSES_TITLE"] = "Kính \"mát\" <A&B>", ["GLASSES_INFO"] = "MK1234-C1" },
                "Máy in 'Xprinter'"),
            PrintXmlBuilder.BuildXml(@"D:\a.btw", null, ""),
            PrintXmlBuilder.BuildXml(@"D:\a.btw", new Dictionary<string, string>(), "P")
        }.Select(s => s.ReplaceLineEndings("\n")).ToList());
    }

    [Fact]
    public void PriceOverride_TryResolve()
    {
        PriceOverride per = new() { Mode = PriceOverrideMode.PerProduct };
        per.ProductOverrides["sp1"] = 5;
        PriceOverride uni = new() { Mode = PriceOverrideMode.Uniform, UniformPrice = 9 };
        PriceOverride keep = new();

        var results = new[] { (per, "SP1"), (per, "SP2"), (per, ""), (uni, "X"), (keep, "SP1") }
            .Select(x =>
            {
                bool ok = x.Item1.TryResolve(x.Item2, out double price);
                return new { x.Item1.Mode, Code = x.Item2, ok, price };
            })
            .ToList();
        Snapshot.Match(results);
    }
}
