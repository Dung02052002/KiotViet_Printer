using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.ImageTools;
using KiotVietLabelPrinter.Services.Labels;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

// ConfigService là singleton đọc Config/config.json cạnh .exe — chạy tuần tự.
[Collection("AppState")]
public class LabelFlowTests
{
    [Fact]
    public void Config_LoadsRealLabelCatalog()
    {
        AppConfig config = ConfigService.Instance.Config;
        Snapshot.Match(new
        {
            Labels = config.Labels.Select(l => new { l.Code, l.Name, l.HandlerType, l.IsEnabled, l.RequiresEmployeeCode, l.UseBarcodeParser, l.AppendEmployeeCode, l.TargetNameColumnIndex }).ToList(),
            Enabled = new LabelCatalogService().GetAllEnabled().Select(l => l.Code).ToList(),
            Tools = new ImageToolCatalogService().GetAllEnabled().Select(t => t.Code).ToList()
        });
    }

    [Fact]
    public void LabelCatalog_UnknownCode_Throws()
    {
        Exception ex = Assert.ThrowsAny<Exception>(() => new LabelCatalogService().GetByCode("KHONG_CO"));
        Assert.Equal("Không tìm thấy cấu hình tem: KHONG_CO", ex.Message);
    }

    [Fact]
    public void HandlerFactory_ResolvesAllHandlers()
    {
        LabelHandlerFactory factory = new();
        Snapshot.Match(new[] { "FULL", "BARCODE", "GENERIC", "GLASSES", "barcode" }
            .Select(t => new { Requested = t, factory.GetHandler(t).HandlerType, Type = factory.GetHandler(t).GetType().Name })
            .ToList());

        Exception ex = Assert.ThrowsAny<Exception>(() => factory.GetHandler("XYZ"));
        Assert.Equal("Chưa hỗ trợ handler: XYZ", ex.Message);
    }

    [Theory]
    [InlineData("FULL")]
    [InlineData("BARCODE")]
    [InlineData("GENERIC")]
    [InlineData("GLASSES")]
    public void Handler_BuildPreview(string handlerType)
    {
        LabelDefinition label = new() { Code = handlerType, HandlerType = handlerType, Name = handlerType };
        var handler = new LabelHandlerFactory().GetHandler(handlerType);
        List<PreviewRow> rows = handler.BuildPreview(TestData.ProductsFromSamples(), label, "NV7");

        Snapshot.Match(new
        {
            Rows = rows,
            ParseCheck = ParseCheckService.Build(rows).Select(r => new { r.HasFlag, r.Flags, Row = r }).ToList()
        }, member: $"{nameof(Handler_BuildPreview)}_{handlerType}");
    }

    [Fact]
    public void PreviewService_BuildPreviewRows()
    {
        Snapshot.Match(new
        {
            WithEmployee = new PreviewService().BuildPreviewRows(TestData.ProductsFromSamples(), true, false, " NV7 "),
            NoEmployee = new PreviewService().BuildPreviewRows(TestData.ProductsFromSamples(), false, true, "")
        });
    }

    [Theory]
    [InlineData("FULL")]
    [InlineData("BARCODE")]
    [InlineData("TEM_KINH")]
    public void LabelService_BuildPreview_FromKiotVietExcel(string labelCode)
    {
        string dir = TestData.NewTempDir();
        string source = TestData.CreateKiotVietSource(dir);

        List<PreviewRow> rows = new LabelService().BuildPreview(source, labelCode, "NV7");

        Snapshot.Match(rows, member: $"{nameof(LabelService_BuildPreview_FromKiotVietExcel)}_{labelCode}");
    }

    // File Excel nguồn là dữ liệu của MỘT lần xem trước/in — không được ghi
    // lên cấu hình tem dùng chung (sẽ bị lưu vào config.json ở lần Save sau,
    // và bị sửa chéo giữa các luồng).
    [Fact]
    public void LabelService_DoesNotWriteSourceFileIntoSharedConfig()
    {
        string source = TestData.CreateKiotVietSource(TestData.NewTempDir());

        new LabelService().BuildPreview(source, "FULL", "");

        LabelDefinition shared = new LabelCatalogService().GetByCode("FULL");
        Assert.Equal("", shared.SourceExcelFile);
        Assert.DoesNotContain("SourceExcelFile",
            Newtonsoft.Json.JsonConvert.SerializeObject(KiotVietLabelPrinter.Infrastructure.Storage.ConfigService.Instance.Config));
    }

    [Fact]
    public void LabelService_ReadProducts_Errors()
    {
        LabelService service = new();
        Snapshot.Match(new[]
        {
            Assert.ThrowsAny<Exception>(() => service.ReadProducts("")).Message,
            Assert.ThrowsAny<Exception>(() => service.ReadProducts(@"Z:\khong\co.xlsx")).Message.Replace("\r", "")
        });
        Assert.False(LabelService.IsPrinting);
    }

    [Fact]
    public void History_AddThenGetAll_RoundTrips()
    {
        HistoryService history = new();
        List<PrintHistory> before = history.GetAll();

        PrintHistory item = new()
        {
            PrintTime = new DateTime(2026, 10, 7, 9, 30, 0),
            LabelCode = "TEST",
            LabelName = "Tem test",
            SourceExcelFile = "test.xlsx",
            EmployeeCode = "NV7",
            ProductCount = 3,
            TotalLabels = 6
        };

        try
        {
            history.Add(item);
            List<PrintHistory> after = new HistoryService().GetAll();
            Assert.Equal(before.Count + 1, after.Count);
            Assert.Contains(after, h => h.LabelName == "Tem test" && h.TotalLabels == 6 && h.EmployeeCode == "NV7");
        }
        finally
        {
            history.SaveAll(before);
        }
    }
}
