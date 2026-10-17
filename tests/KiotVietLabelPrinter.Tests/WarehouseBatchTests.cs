using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

/// <summary>
/// Phiếu xuất kho NHIỀU PHIẾU: file "Danh sách chi tiết chuyển hàng" của
/// KiotViet chứa nhiều phiếu chuyển (cột Mã chuyển hàng) → mỗi phiếu một file.
/// </summary>
public class WarehouseBatchTests
{
    // Cột giống file thật (rút gọn): Mã chuyển hàng | Tới chi nhánh | Ghi chú chuyển | Tổng SL chuyển | Mã hàng | ĐVT | Tên hàng | SL chuyển | SL nhận
    private static readonly string[] BatchHeader =
        ["Mã chuyển hàng", "Tới chi nhánh", "Ghi chú chuyển", "Tổng SL chuyển", "Mã hàng", "ĐVT", "Tên hàng", "Số lượng chuyển", "Số lượng nhận"];

    private static object?[] BatchLine(string transfer, string note, string code, string name, object? qty) =>
        [transfer, "AEON MALL BÌNH TÂN", note, 99, code, null, name, qty, 0];

    // Thứ tự trong file giống file thật: phiếu mới nhất trước.
    private static string CreateBatchGoods(string dir) => WarehouseTestData.CreateGoods(dir, BatchHeader,
    [
        BatchLine("TRF023522", "BÌNH TÂN THÙNG 4", "SP1", "Túi xách tay nữ Pucini", 2),
        BatchLine("TRF023522", "BÌNH TÂN THÙNG 4", "SP2", "Giày cao gót nữ Pucini", 1),
        BatchLine("TRF023510", "BÌNH TÂN THÙNG 1", "SP1", "Túi xách tay nữ Pucini", 3),
        BatchLine("TRF023512", "BÌNH TÂN THÙNG 2", "SP3", "Ví nữ Pucini", 5),
        BatchLine("TRF023512", "BÌNH TÂN THÙNG 2", "SP4", "Kính mắt Pucini", 1),
        BatchLine("TRF023512", "BÌNH TÂN THÙNG 2", "SP5", "Dép nữ Pucini", 2),
    ], "DanhSachChiTietChuyenHang.xlsx");

    [Theory]
    [InlineData("TRF023522", "XK", "XK023522")]   // giữ số 0 ở đầu
    [InlineData("trf023522", "XK", "XK023522")]
    [InlineData("  TRF023510 ", "XK", "XK023510")]
    [InlineData("TRF23609.01", "XK", "XK23609.01")]
    [InlineData("TRF023522", "PX", "PX023522")]
    [InlineData("TRF023522", "", "023522")]
    public void FromTransferCode_ReplacesTrfWithPrefix_KeepingLeadingZeros(string transfer, string prefix, string expected)
    {
        TicketCodeResult result = WarehouseTicketCodeService.FromTransferCode(transfer, prefix);
        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("TRF")]
    [InlineData("HD023522")]       // không phải phiếu chuyển
    [InlineData("TRF0235/22")]     // ký tự không dùng làm tên file được
    public void FromTransferCode_RejectsInvalid(string? transfer)
    {
        TicketCodeResult result = WarehouseTicketCodeService.FromTransferCode(transfer, "XK");
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void SplitByTransfer_OneTicketPerTransferCode_SortedByCode()
    {
        string dir = TestData.NewTempDir();
        WarehouseDocumentService service = new();
        WarehouseDocument doc = service.Load(WarehouseTestData.CreateTemplate(dir), CreateBatchGoods(dir));

        Assert.True(doc.Goods.HasTransferColumn);
        List<WarehouseBatchTicket>? batch = service.SplitByTransfer(doc);

        Assert.NotNull(batch);
        Assert.Equal(["TRF023510", "TRF023512", "TRF023522"], batch!.Select(t => t.TransferCode));
        Assert.Equal(["XK023510", "XK023512", "XK023522"], batch.Select(t => t.TicketCode("XK").Code));
        Assert.Equal([1, 3, 2], batch.Select(t => t.Document.Rows.Count));
        Assert.Equal("BÌNH TÂN THÙNG 2", batch[1].Note);
        Assert.Equal("AEON MALL BÌNH TÂN", batch[1].Destination);
        Assert.Equal(8, batch[1].TotalQuantity);

        // Số lượng lấy đúng cột "Số lượng chuyển" (không lấy Tổng SL chuyển / Số lượng nhận); giữ số dòng gốc.
        WarehouseProductRow row = batch[1].Document.Rows[0];
        Assert.Equal(5, row.ActualQuantity);
        Assert.Equal(5, row.RequestedQuantity);
        Assert.Equal(5, row.SourceRowNumber);
    }

    [Fact]
    public void SplitByTransfer_SameProductInTwoTickets_IsNotADuplicateWarning()
    {
        string dir = TestData.NewTempDir();
        WarehouseDocumentService service = new();
        WarehouseDocument doc = service.Load(WarehouseTestData.CreateTemplate(dir), CreateBatchGoods(dir));

        // Đọc cả file như một phiếu: SP1 lặp ở 2 dòng → cảnh báo. Tách theo phiếu thì mỗi phiếu chỉ có một dòng SP1.
        Assert.Contains(doc.Rows, r => r.ProductCode == "SP1" && r.Status == WarehouseRowStatus.Warning);
        List<WarehouseBatchTicket> batch = service.SplitByTransfer(doc)!;
        Assert.All(batch.SelectMany(t => t.Document.Rows).Where(r => r.ProductCode == "SP1"),
            r => Assert.DoesNotContain(r.Messages, m => m.Contains("lặp lại")));
    }

    [Fact]
    public void SplitByTransfer_SingleTransferCode_StaysSingleTicket()
    {
        string dir = TestData.NewTempDir();
        string goods = WarehouseTestData.CreateGoods(dir, BatchHeader,
        [
            BatchLine("TRF023522", "THÙNG 4", "SP1", "Túi xách tay nữ", 2),
            BatchLine("TRF023522", "THÙNG 4", "SP2", "Giày nữ", 1),
        ]);
        WarehouseDocumentService service = new();
        WarehouseDocument doc = service.Load(WarehouseTestData.CreateTemplate(dir), goods);

        Assert.Null(service.SplitByTransfer(doc));
        Assert.Equal("TRF023522", WarehouseDocumentService.SingleTransferCode(doc.Goods));
    }

    [Fact]
    public void SplitByTransfer_FileWithoutTransferColumn_StaysSingleTicket()
    {
        string dir = TestData.NewTempDir();
        WarehouseDocumentService service = new();
        WarehouseDocument doc = service.Load(WarehouseTestData.CreateTemplate(dir),
            WarehouseTestData.CreateGoods(dir, WarehouseTestData.Line("SP1", "Giày nữ", 2d)));

        Assert.False(doc.Goods.HasTransferColumn);
        Assert.Null(service.SplitByTransfer(doc));
        Assert.Null(WarehouseDocumentService.SingleTransferCode(doc.Goods));
    }

    [Fact]
    public void SplitByTransfer_LinesWithoutTransferCode_BecomeAnInvalidTicketThatBlocksExport()
    {
        string dir = TestData.NewTempDir();
        string goods = WarehouseTestData.CreateGoods(dir, BatchHeader,
        [
            BatchLine("TRF023522", "THÙNG 4", "SP1", "Túi xách tay nữ", 2),
            BatchLine("TRF023510", "THÙNG 1", "SP2", "Giày nữ", 1),
            BatchLine("", "", "SP3", "Ví nữ", 1),
        ]);
        WarehouseDocumentService service = new();
        List<WarehouseBatchTicket> batch = service.SplitByTransfer(service.Load(WarehouseTestData.CreateTemplate(dir), goods))!;

        Assert.Equal(3, batch.Count);
        WarehouseBatchTicket orphan = batch[^1];
        Assert.Equal("", orphan.TransferCode);
        Assert.False(orphan.TicketCode("XK").IsValid);

        WarehouseValidationReport report = service.Validate(orphan.TicketCode("XK"), new DateTime(2026, 10, 17), orphan.Document,
            Path.Combine(dir, "out"), WarehouseFontSettings.KeepTemplate());
        Assert.False(report.CanExport);
    }

    [Fact]
    public void BatchExport_EachTicketToItsOwnFile_WithItsOwnCodeAndRows()
    {
        string dir = TestData.NewTempDir();
        string output = Path.Combine(dir, "out");
        string template = WarehouseTestData.CreateTemplate(dir);
        WarehouseDocumentService service = new();
        List<WarehouseBatchTicket> batch = service.SplitByTransfer(service.Load(template, CreateBatchGoods(dir)))!;
        DateTime today = new(2026, 10, 17);

        foreach (WarehouseBatchTicket ticket in batch)
        {
            TicketCodeResult code = ticket.TicketCode("XK");
            WarehouseValidationReport report = service.Validate(code, today, ticket.Document, output, WarehouseFontSettings.KeepTemplate());
            Assert.True(report.CanExport, string.Join("\n", report.Errors));

            WarehouseExportResult result = service.Export(new WarehouseExportRequest
            {
                TemplatePath = template,
                OutputPath = WarehouseDocumentService.BuildOutputPath(output, code.Code!, ".xlsx"),
                TicketCode = code.Code!,
                DocumentDate = today,
                Rows = ticket.Document.Rows
            });
            ticket.Export.Record(result);
            Assert.Equal(ticket.Document.Rows.Count, result.RowCount);
        }

        Assert.Equal(["XK023510.xlsx", "XK023512.xlsx", "XK023522.xlsx"],
            Directory.GetFiles(output).Select(Path.GetFileName).Order());

        using (FileStream fs = File.OpenRead(Path.Combine(output, "XK023512.xlsx")))
        {
            using XSSFWorkbook wb = new(fs);
            ISheet sheet = wb.GetSheetAt(0);
            Assert.Equal("Số : XK023512", sheet.GetRow(4).GetCell(1).StringCellValue);
            Assert.Equal("Ví nữ Pucini", sheet.GetRow(13).GetCell(0).StringCellValue);
            Assert.Equal("Dép nữ Pucini", sheet.GetRow(15).GetCell(0).StringCellValue);
        }

        // Mỗi phiếu theo dõi bản xuất riêng: sửa một phiếu không làm phiếu khác thành bản cũ.
        batch[0].Export.MarkStale();
        Assert.Equal(WarehouseExportState.Stale, batch[0].Export.Check());
        Assert.Equal(WarehouseExportState.Current, batch[1].Export.Check());
    }

    /// <summary>File THẬT người dùng gửi (4 phiếu chuyển → AEON MALL BÌNH TÂN) + mẫu Bình Tân thật, nếu có trên máy.</summary>
    [Fact]
    public void RealBatchFile_SplitsIntoFourTickets_AndExports()
    {
        string goods = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "DanhSachChiTietChuyenHang_KV17102026-110940-257.xlsx");
        string template = Path.Combine(WarehouseTestData.RealTemplatesFolder, "Bình Tân", "Phiếu Xuất Kho.xlsx");
        if (!File.Exists(goods) || !File.Exists(template))
            return; // máy không có file thật — các test tổng hợp ở trên vẫn bao phủ logic.

        WarehouseDocumentService service = new();
        List<WarehouseBatchTicket> batch = service.SplitByTransfer(service.Load(template, goods))!;

        Assert.Equal(["XK023510", "XK023512", "XK023519", "XK023522"], batch.Select(t => t.TicketCode("XK").Code));
        Assert.Equal([14, 16, 19, 13], batch.Select(t => t.Document.Rows.Count));
        Assert.Equal([27d, 30d, 25d, 25d], batch.Select(t => t.TotalQuantity));
        Assert.Equal("BÌNH TÂN THÙNG 1", batch[0].Note);

        string output = TestData.NewTempDir();
        foreach (WarehouseBatchTicket ticket in batch)
        {
            string code = ticket.TicketCode("XK").Code!;
            WarehouseValidationReport report = service.Validate(ticket.TicketCode("XK"), DateTime.Today, ticket.Document, output,
                WarehouseFontSettings.KeepTemplate());
            Assert.True(report.CanExport, code + ": " + string.Join("\n", report.Errors));

            WarehouseExportResult result = service.Export(new WarehouseExportRequest
            {
                TemplatePath = template,
                OutputPath = WarehouseDocumentService.BuildOutputPath(output, code, ".xlsx"),
                TicketCode = code,
                DocumentDate = DateTime.Today,
                Rows = ticket.Document.Rows
            });
            Assert.Equal(ticket.Document.Rows.Count, result.RowCount);
        }

        Assert.Equal(4, Directory.GetFiles(output, "XK0235*.xlsx").Length);
    }
}
