using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using NPOI.SS.UserModel;
using HorizontalAlignment = NPOI.SS.UserModel.HorizontalAlignment;
using BorderStyle = NPOI.SS.UserModel.BorderStyle;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using Xunit;

namespace KiotVietLabelPrinter.Tests;

public class WarehouseTicketCodeTests
{
    [Theory]
    [InlineData("23609", "XK23609")]
    [InlineData("  23609  ", "XK23609")]
    [InlineData("XK23609", "XK23609")]
    [InlineData("xk23609", "XK23609")]
    [InlineData("023056", "XK023056")]
    public void Normalize_AddsPrefixOnce(string input, string expected)
    {
        TicketCodeResult result = WarehouseTicketCodeService.Normalize(input);
        Assert.True(result.IsValid, result.Error);
        Assert.Equal(expected, result.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("XK")]
    [InlineData("236 09")]
    [InlineData("23609/1")]
    [InlineData("phiếu1")]
    public void Normalize_RejectsInvalid(string? input)
    {
        TicketCodeResult result = WarehouseTicketCodeService.Normalize(input);
        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Normalize_UsesConfiguredPrefix()
    {
        Assert.Equal("PX23609", WarehouseTicketCodeService.Normalize("23609", "PX").Code);
        Assert.Equal("PX23609", WarehouseTicketCodeService.Normalize("px23609", "PX").Code);
    }
}

public class WarehouseUnitTests
{
    private readonly WarehouseUnitService _units = new();

    [Theory]
    [InlineData("Giày nữ Pucini")]
    [InlineData("Giày nữ, mũ giày bằng da tổng hợp + đế giày bằng nhựa, mã 3233-3, nhãn hiệu Pucini, mới 100%")]
    [InlineData("Giày búp bê trẻ em, có quai ngang, mũ giày bằng da tổng hợp kết hợp đế nhựa, mã A2297 - ( BEIGE 30 UG503 G7 )")]
    [InlineData("Giày boot trẻ em, đế cao 2cm, mã A901-2")]
    [InlineData("Giầy thể thao nam")]
    [InlineData("Dép quai ngang nữ, đế cao su")]
    [InlineData("DÉP NAM DA BÒ")]
    [InlineData("Guốc gỗ nữ cao 7cm")]
    [InlineData("Sandal nữ quai mảnh")]
    [InlineData("Xăng đan nam")]
    [InlineData("Boot cổ cao nữ")]
    [InlineData("Bốt da nữ")]
    [InlineData("Ủng đi mưa trẻ em")]
    [InlineData("SANDAL NU 2024")]
    [InlineData("BOOTS NAM")]
    public void PairProducts_AreDoi(string name)
    {
        UnitDetection unit = _units.Detect(name);
        Assert.Equal(WarehouseUnitService.PairUnit, unit.Unit);
        Assert.False(unit.IsAmbiguous, unit.Reason);
    }

    [Theory]
    [InlineData("Kính mắt Pucini")]
    [InlineData("Kính râm, không phải kính thuốc, gọng bằng sắt + nhựa, mã 6282, nhãn hiệu Pucini, mới 100%")]
    [InlineData("Túi xách tay nữ, kt(23-37*7.5-12*14- 21)cm(+/-10%), model 3450, nhãn hiệu PUCINI")]
    [InlineData("Thắt lưng nam, kt(120*4)cm(+/-10%), chất liệu dây bằng da thuộc từ da bò, model PUTL009")]
    [InlineData("Ví nữ cầm tay, KT (17x2x10) +-10%, mã 202304")]
    [InlineData("TÚI GIẤY PUCINI VT005 - WHITE - SMALL")]
    [InlineData("Túi đẹp mẫu mới")]
    [InlineData("Ủng hộ quỹ")]
    [InlineData("Bộ quà tặng")]
    public void OtherProducts_AreCai(string name)
    {
        UnitDetection unit = _units.Detect(name);
        Assert.Equal(WarehouseUnitService.PieceUnit, unit.Unit);
    }

    [Theory]
    [InlineData("Túi đựng giày du lịch", "Cái")]
    [InlineData("Dây giày trắng", "Cái")]
    [InlineData("Túi xách nữ, tặng kèm lót giày", "Cái")]
    [InlineData("GIAY NU 3233", "Đôi")]
    [InlineData("TUI GIAY VT005", "Cái")]
    [InlineData("DEP NAM", "Đôi")]
    public void AmbiguousNames_AreFlaggedForReview(string name, string expectedUnit)
    {
        UnitDetection unit = _units.Detect(name);
        Assert.Equal(expectedUnit, unit.Unit);
        Assert.True(unit.IsAmbiguous);
        Assert.False(string.IsNullOrWhiteSpace(unit.Reason));
    }

    [Fact]
    public void DecomposedUnicode_StillMatches()
    {
        string decomposed = "Giày nữ".Normalize(System.Text.NormalizationForm.FormD);
        Assert.Equal(WarehouseUnitService.PairUnit, _units.Detect(decomposed).Unit);
    }

    [Fact]
    public void Keyword_MustMatchWholeWord()
    {
        // "Ủngbo" / "boota" không phải từ khoá — không được khớp chuỗi con.
        Assert.Equal(WarehouseUnitService.PieceUnit, _units.Detect("Hộp bootanic, quà tặng").Unit);
    }
}

public class WarehouseProductNameTests
{
    private static WarehouseGoodsFile Goods(params WarehouseGoodsLine[] lines) => new()
    {
        SheetName = "S",
        HeaderRowNumber = 1,
        HasCodeColumn = true,
        QuantityColumnHeader = "Số lượng chuyển",
        Lines = lines.ToList()
    };

    private static WarehouseGoodsLine L(int row, string code, string name, double? qty, string? qtyText = null) => new()
    {
        SourceRowNumber = row,
        ProductCode = code,
        ProductName = name,
        Quantity = qty,
        QuantityText = qtyText ?? qty?.ToString() ?? ""
    };

    [Fact]
    public void ManyProducts_KeepOrderNamesUnitsAndQuantities()
    {
        List<WarehouseProductRow> rows = new WarehouseProductNameService().BuildRows(Goods(
            L(2, "SP001", "Giày nữ Pucini", 4),
            L(3, "SP002", "Kính mắt Pucini", 10),
            L(4, "SP003", "Túi xách tay nữ, model 3450", 3)));

        Assert.Equal(["SP001", "SP002", "SP003"], rows.Select(r => r.ProductCode));
        Assert.Equal(["Đôi", "Cái", "Cái"], rows.Select(r => r.Unit));
        Assert.Equal([4d, 10d, 3d], rows.Select(r => r.RequestedQuantity!.Value));
        Assert.Equal(rows.Select(r => r.RequestedQuantity), rows.Select(r => r.ActualQuantity));
        Assert.All(rows, r => Assert.Equal(WarehouseRowStatus.Ok, r.Status));
        Assert.Equal(rows.Select(r => r.SourceName), rows.Select(r => r.TicketName));
    }

    [Fact]
    public void MissingName_IsTakenOnlyFromExactSameCode()
    {
        List<WarehouseProductRow> rows = new WarehouseProductNameService().BuildRows(Goods(
            L(2, "SP001", "Giày nữ Pucini", 4),
            L(3, " sp001 ", "", 2),            // cùng mã (khác hoa/thường, khoảng trắng)
            L(4, "SP0011", "", 1)));          // mã gần giống — không được ghép

        Assert.Equal("Giày nữ Pucini", rows[1].TicketName);
        Assert.Equal(WarehouseRowStatus.Warning, rows[1].Status);
        Assert.Equal(2, rows[1].RequestedQuantity);
        Assert.Equal("", rows[2].TicketName);
        Assert.Equal(WarehouseRowStatus.Error, rows[2].Status);
        Assert.Contains(rows[2].Messages, m => m.Contains("Không tìm thấy tên hàng cho mã SP0011"));
    }

    [Fact]
    public void DuplicateCode_WithDifferentNames_IsError_NotGuessed()
    {
        List<WarehouseProductRow> rows = new WarehouseProductNameService().BuildRows(Goods(
            L(2, "SP001", "Giày nữ màu đen", 4),
            L(5, "SP001", "Giày nữ màu trắng", 1)));

        Assert.All(rows, r => Assert.Equal(WarehouseRowStatus.Error, r.Status));
        Assert.All(rows, r => Assert.Contains(r.Messages, m => m.Contains("2, 5") && m.Contains("tên khác nhau")));
        Assert.Equal("Giày nữ màu đen", rows[0].TicketName);
        Assert.Equal("Giày nữ màu trắng", rows[1].TicketName);
    }

    [Fact]
    public void DuplicateCode_WithSameName_IsWarning_AndQuantitiesStaySeparate()
    {
        List<WarehouseProductRow> rows = new WarehouseProductNameService().BuildRows(Goods(
            L(2, "SP001", "Giày nữ", 4),
            L(3, "SP001", "Giày  nữ ", 1)));

        Assert.All(rows, r => Assert.Equal(WarehouseRowStatus.Warning, r.Status));
        Assert.Equal([4d, 1d], rows.Select(r => r.RequestedQuantity!.Value));
    }

    [Theory]
    [InlineData(0d, "phải lớn hơn 0")]
    [InlineData(-2d, "phải lớn hơn 0")]
    [InlineData(null, "trống hoặc không phải số")]
    public void InvalidQuantity_IsError(double? qty, string message)
    {
        WarehouseProductRow row = new WarehouseProductNameService().BuildRows(Goods(L(2, "SP001", "Giày nữ", qty, qty == null ? "abc" : null)))[0];
        Assert.Equal(WarehouseRowStatus.Error, row.Status);
        Assert.Contains(row.Messages, m => m.Contains(message));
    }

    [Fact]
    public void DecimalQuantity_IsKept_WithWarning()
    {
        WarehouseProductRow row = new WarehouseProductNameService().BuildRows(Goods(L(2, "SP001", "Kính mắt", 2.5)))[0];
        Assert.Equal(2.5, row.RequestedQuantity);
        Assert.Equal(2.5, row.ActualQuantity);
        Assert.Equal(WarehouseRowStatus.Warning, row.Status);
    }

    [Fact]
    public void Revalidate_AfterUserEdit()
    {
        WarehouseProductRow row = new WarehouseProductNameService().BuildRows(Goods(L(2, "SP001", "Giày nữ", 0)))[0];
        Assert.Equal(WarehouseRowStatus.Error, row.Status);

        row.RequestedQuantity = 3;
        row.ActualQuantity = 3;
        WarehouseProductNameService.Revalidate(row);
        Assert.Equal(WarehouseRowStatus.Ok, row.Status);

        row.TicketName = "  ";
        WarehouseProductNameService.Revalidate(row);
        Assert.Equal(WarehouseRowStatus.Error, row.Status);
    }
}

public class WarehouseExcelTests
{
    private readonly WarehouseExcelReader _reader = new();
    private readonly WarehouseExcelWriter _writer = new();

    // Ngày cố định (khác ngày trong file mẫu 22/09/2026) — test không phụ thuộc ngày chạy.
    private static readonly DateTime DocumentDate = new(2026, 10, 10);

    private static List<WarehouseProductRow> Rows(int count) =>
        Enumerable.Range(1, count).Select(i => new WarehouseProductRow
        {
            SourceRowNumber = i + 1,
            ProductCode = $"SP{i:000}",
            SourceName = $"Sản phẩm {i}",
            TicketName = i % 2 == 0 ? $"Giày nữ mẫu {i}, mũ giày bằng da tổng hợp" : $"Kính mắt Pucini mẫu {i}",
            Unit = i % 2 == 0 ? "Đôi" : "Cái",
            RequestedQuantity = i,
            ActualQuantity = i
        }).ToList();

    private WarehouseExportResult Export(string template, string output, int count,
        WarehouseFontSettings? font = null, bool overwrite = false, string code = "XK23609") =>
        _writer.Export(new WarehouseExportRequest
        {
            TemplatePath = template,
            OutputPath = output,
            TicketCode = code,
            DocumentDate = DocumentDate,
            Rows = Rows(count),
            Font = font ?? WarehouseFontSettings.KeepTemplate(),
            OverwriteExisting = overwrite
        });

    private static IWorkbook Open(string path) => WarehouseExcelReader.OpenWorkbook(path, "test");

    private static string Text(ISheet s, string address)
    {
        CellReference r = new(address);
        ICell? cell = s.GetRow(r.Row)?.GetCell(r.Col);
        return cell == null ? "" : cell.CellType == CellType.Numeric ? cell.NumericCellValue.ToString() : cell.StringCellValue;
    }

    private static IFont FontOf(IWorkbook wb, ISheet s, string address)
    {
        CellReference r = new(address);
        return wb.GetFontAt(s.GetRow(r.Row).GetCell(r.Col).CellStyle.FontIndex);
    }

    [Fact]
    public void Layout_IsDetectedByContent()
    {
        string template = WarehouseTestData.CreateTemplate(TestData.NewTempDir());
        WarehouseTemplateLayout layout = _reader.ReadTemplateLayout(template);

        Assert.Equal("Sheet1", layout.SheetName);
        Assert.Equal((4, 1), (layout.TicketCodeRow, layout.TicketCodeColumn)); // B5
        Assert.Equal("Số : ", layout.TicketCodeLabel);
        Assert.Equal(WarehouseTestData.TemplateCode, layout.TemplateTicketCode);
        Assert.Equal((11, 12), (layout.HeaderFirstRow, layout.HeaderLastRow));
        Assert.Equal((0, 1, 2, 3), (layout.NameColumn, layout.UnitColumn, layout.RequestedColumn, layout.ActualColumn));
        Assert.Equal((13, 2), (layout.DataFirstRow, layout.DataRowCount));
        Assert.Equal("Times New Roman", layout.NameFontName);
        Assert.Equal(12, layout.NameFontSize);
    }

    [Fact]
    public void Template_MissingColumn_IsReported()
    {
        string template = WarehouseTestData.CreateTemplate(TestData.NewTempDir(), includeActualColumn: false);
        WarehouseException ex = Assert.Throws<WarehouseException>(() => _reader.ReadTemplateLayout(template));
        Assert.Equal(WarehouseErrorKind.TemplateStructure, ex.Kind);
        Assert.Contains("Thực xuất", ex.Message);
    }

    [Fact]
    public void Template_WithoutTable_IsReported()
    {
        string dir = TestData.NewTempDir();
        string notTemplate = WarehouseTestData.CreateGoods(dir, WarehouseTestData.Line("SP1", "Giày", 1));
        WarehouseException ex = Assert.Throws<WarehouseException>(() => _reader.ReadTemplateLayout(notTemplate));
        Assert.Equal(WarehouseErrorKind.TemplateStructure, ex.Kind);
    }

    [Fact]
    public void Files_NotFoundOrUnreadable_AreDistinguished()
    {
        string dir = TestData.NewTempDir();
        Assert.Equal(WarehouseErrorKind.FileNotFound,
            Assert.Throws<WarehouseException>(() => _reader.ReadTemplateLayout(Path.Combine(dir, "khong-co.xlsx"))).Kind);

        string broken = Path.Combine(dir, "hong.xlsx");
        File.WriteAllText(broken, "không phải excel");
        Assert.Equal(WarehouseErrorKind.WorkbookUnreadable,
            Assert.Throws<WarehouseException>(() => _reader.ReadGoods(broken)).Kind);
    }

    [Fact]
    public void Goods_ReadsKiotVietTransferExport()
    {
        string goods = WarehouseTestData.CreateGoods(TestData.NewTempDir(),
            ["SP055272", "Giày nữ Pucini", "", 4d, 0d],
            [null, null, null, null, null],
            ["SP005146", "TÚI GIẤY PUCINI VT005", "CHIẾC", "300", 0d],
            ["SP9", "Kính mắt", "", "abc", 0d]);

        WarehouseGoodsFile file = _reader.ReadGoods(goods);
        Assert.Equal("Số lượng chuyển", file.QuantityColumnHeader);
        Assert.True(file.HasCodeColumn);
        Assert.Equal([2, 4, 5], file.Lines.Select(l => l.SourceRowNumber));
        Assert.Equal([4d, 300d, null], file.Lines.Select(l => l.Quantity));
        Assert.Equal("CHIẾC", file.Lines[1].SourceUnit);
    }

    [Fact]
    public void Goods_MissingQuantityColumn_IsReported()
    {
        string goods = WarehouseTestData.CreateGoods(TestData.NewTempDir(), ["Mã hàng", "Tên hàng", "Số lượng nhận"],
            [["SP1", "Giày", 1d]]);
        WarehouseException ex = Assert.Throws<WarehouseException>(() => _reader.ReadGoods(goods));
        Assert.Equal(WarehouseErrorKind.GoodsStructure, ex.Kind);
        Assert.Contains("số lượng", ex.Message);
    }

    [Fact]
    public void Export_SameRowCount_UpdatesOnlyAllowedCells_AndKeepsTemplateFile()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string hash = WarehouseTestData.Sha256(template);
        string output = Path.Combine(dir, "out", "XK23609.xlsx");

        WarehouseExportResult result = Export(template, output, 2);

        Assert.Equal(hash, WarehouseTestData.Sha256(template));
        Assert.Equal(0, result.RowDelta);
        Assert.NotEmpty(result.VerifiedChecks);
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "~*"));

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        Assert.Equal("Số : XK23609", Text(s, "B5"));
        // Ngày lập phiếu thay bằng ngày đã chọn, khoảng trắng căn lề của mẫu giữ nguyên.
        Assert.Equal("Ngày  10  tháng   10     năm 2026", Text(s, "B4"));
        Assert.Equal("Kính mắt Pucini mẫu 1", Text(s, "A14"));
        Assert.Equal("Cái", Text(s, "B14"));
        Assert.Equal("1", Text(s, "C14"));
        Assert.Equal("1", Text(s, "D14"));
        Assert.Equal("Đôi", Text(s, "B15"));
        Assert.Equal("Người lập phiếu\n(Ký, họ tên)", Text(s, "A17"));
        Assert.Equal(2, wb.NumberOfSheets);
        Assert.Equal("Sheet phụ không được đụng tới", Text(wb.GetSheetAt(1), "A1"));
    }

    [Fact]
    public void Export_MoreRowsThanTemplate_InsertsFormattedRows_AndShiftsFooter()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK23609.xlsx");

        WarehouseExportResult result = Export(template, output, 5);
        Assert.Equal(3, result.RowDelta);

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        for (int i = 0; i < 5; i++)
        {
            IRow row = s.GetRow(13 + i);
            Assert.Equal(BorderStyle.Thin, row.GetCell(0).CellStyle.BorderBottom);
            Assert.True(row.GetCell(0).CellStyle.WrapText);
            Assert.Equal(HorizontalAlignment.Center, row.GetCell(1).CellStyle.Alignment);
            Assert.False(((XSSFRow)row).GetCTRow().customHeight, "Dòng hàng mới phải để Excel tự giãn chiều cao như mẫu.");
            Assert.Equal((i + 1).ToString(), Text(s, $"C{14 + i}"));
        }

        Assert.Equal("Người lập phiếu\n(Ký, họ tên)", Text(s, "A20"));   // mẫu: A17
        Assert.Equal(30.75f, s.GetRow(19).HeightInPoints);
        Assert.Equal("Ghi chú cuối phiếu (ô gộp dưới bảng)", Text(s, "A22")); // mẫu: A19

        HashSet<string> merges = Enumerable.Range(0, s.NumMergedRegions).Select(i => s.GetMergedRegion(i).FormatAsString()).ToHashSet();
        Assert.Equal(new HashSet<string> { "A8:D8", "A12:A13", "B12:B13", "C12:D12", "A22:B22" }, merges);
        Assert.Equal("Sheet1!$A$1:$D$22", wb.GetPrintArea(0));
        Assert.Equal(9, s.PrintSetup.PaperSize); // A4 (mã Excel) giữ nguyên như mẫu
        Assert.Equal(0.25, s.GetMargin(MarginType.LeftMargin));
    }

    [Fact]
    public void Export_FewerRowsThanTemplate_RemovesOnlyUnusedProductRows()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir, dataRows: 4);
        string output = Path.Combine(dir, "XK1.xlsx");

        WarehouseExportResult result = Export(template, output, 1, code: "XK1");
        Assert.Equal(-3, result.RowDelta);

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        Assert.Equal("Kính mắt Pucini mẫu 1", Text(s, "A14"));
        Assert.Equal("", Text(s, "A15"));
        Assert.Equal("Người lập phiếu\n(Ký, họ tên)", Text(s, "A16")); // mẫu: A19
        Assert.Equal("Tên hàng hóa", Text(s, "A12"));
        Assert.Contains("A18:B18", Enumerable.Range(0, s.NumMergedRegions).Select(i => s.GetMergedRegion(i).FormatAsString()));
        Assert.Equal("Sheet1!$A$1:$D$18", wb.GetPrintArea(0));
    }

    [Fact]
    public void Export_KeepTemplateFont_ChangesNoFont()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK.xlsx");
        Export(template, output, 3);

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        foreach (string a in new[] { "A14", "B14", "A16", "A12", "B3" })
            Assert.Equal("Times New Roman", FontOf(wb, s, a).FontName);
        Assert.Equal(16, FontOf(wb, s, "B3").FontHeightInPoints);
        Assert.Equal(12, FontOf(wb, s, "A16").FontHeightInPoints);
    }

    [Fact]
    public void Export_FontOnProductNames_OnlyTouchesNameCells()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK.xlsx");
        Export(template, output, 3, new WarehouseFontSettings
        {
            Scope = WarehouseFontApplyScope.ProductNameOnly, FontFamily = "Arial", FontSize = 10
        });

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        foreach (string a in new[] { "A14", "A15", "A16" })
        {
            Assert.Equal("Arial", FontOf(wb, s, a).FontName);
            Assert.Equal(10, FontOf(wb, s, a).FontHeightInPoints);
            Assert.True(s.GetRow(new CellReference(a).Row).GetCell(0).CellStyle.WrapText);
            Assert.Equal(BorderStyle.Thin, s.GetRow(new CellReference(a).Row).GetCell(0).CellStyle.BorderLeft);
        }

        Assert.Equal("Times New Roman", FontOf(wb, s, "B14").FontName);   // đơn vị
        Assert.Equal("Times New Roman", FontOf(wb, s, "A12").FontName);   // tiêu đề bảng
        Assert.Equal(12, FontOf(wb, s, "C15").FontHeightInPoints);
        Assert.Equal("Times New Roman", FontOf(wb, s, "A9").FontName);
    }

    [Fact]
    public void Export_FontSizeOnTable_TouchesHeaderAndRows_NotTitleOrSignature()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK.xlsx");
        Export(template, output, 2, new WarehouseFontSettings
        {
            Scope = WarehouseFontApplyScope.ProductTable, FontFamily = "Times New Roman", FontSize = 14
        });

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        foreach (string a in new[] { "A12", "C13", "A14", "B15", "D15" })
            Assert.Equal(14, FontOf(wb, s, a).FontHeightInPoints);
        Assert.True(FontOf(wb, s, "A12").IsBold);            // vẫn đậm như mẫu
        Assert.Equal(16, FontOf(wb, s, "B3").FontHeightInPoints);
        Assert.Equal(12, FontOf(wb, s, "A17").FontHeightInPoints);
        Assert.Equal(12, FontOf(wb, s, "B5").FontHeightInPoints);
    }

    [Fact]
    public void Export_FontOnWholeDocument_AlsoChangesRichTextRuns()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK.xlsx");
        Export(template, output, 2, new WarehouseFontSettings
        {
            Scope = WarehouseFontApplyScope.WholeDocument, FontFamily = "Arial", FontSize = 11
        });

        using IWorkbook wb = Open(output);
        ISheet s = wb.GetSheetAt(0);
        XSSFRichTextString sig = (XSSFRichTextString)s.GetRow(16).GetCell(0).RichStringCellValue;
        Assert.All(sig.GetCTRst().r, run => Assert.Equal("Arial", run.rPr.rFont.val));
        Assert.True(sig.GetCTRst().r[0].rPr.b.val);   // "Người lập phiếu" vẫn đậm
        Assert.True(sig.GetCTRst().r[1].rPr.i.val);   // "(Ký, họ tên)" vẫn nghiêng
        Assert.Equal("Arial", FontOf(wb, s, "B3").FontName);
        Assert.NotEqual("Arial", FontOf(wb, wb.GetSheetAt(1), "A1").FontName); // sheet khác không bị đụng
    }

    [Fact]
    public void Export_OutputFolderMissing_IsCreated()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "chua", "co", "XK2.xlsx");
        Export(template, output, 1);
        Assert.True(File.Exists(output));
    }

    [Fact]
    public void Export_ExistingOutput_RequiresConfirmation_AndLockedFileIsReported()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK3.xlsx");
        Export(template, output, 1);
        string firstHash = WarehouseTestData.Sha256(output);

        WarehouseException exists = Assert.Throws<WarehouseException>(() => Export(template, output, 2));
        Assert.Equal(WarehouseErrorKind.OutputExists, exists.Kind);
        Assert.Equal(firstHash, WarehouseTestData.Sha256(output));

        using (new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            WarehouseException locked = Assert.Throws<WarehouseException>(() => Export(template, output, 2, overwrite: true));
            Assert.Equal(WarehouseErrorKind.OutputLocked, locked.Kind);
        }

        Export(template, output, 2, overwrite: true);
        Assert.NotEqual(firstHash, WarehouseTestData.Sha256(output));
        Assert.Empty(Directory.GetFiles(dir, "~*"));
    }

    [Fact]
    public void Export_XlsTemplate_KeepsXlsFormat()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir, extension: ".xls");
        string output = WarehouseDocumentService.BuildOutputPath(Path.Combine(dir, "out"), "XK5", ".xls");
        Export(template, output, 4, code: "XK5");

        using IWorkbook wb = Open(output);
        Assert.IsType<NPOI.HSSF.UserModel.HSSFWorkbook>(wb);
        Assert.Equal("Số : XK5", Text(wb.GetSheetAt(0), "B5"));
        Assert.Equal("Người lập phiếu\n(Ký, họ tên)", Text(wb.GetSheetAt(0), "A19"));
    }

    [Fact]
    public void Export_RefusesToOverwriteTemplateItself()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir, fileName: "XK9.xlsx");
        WarehouseException ex = Assert.Throws<WarehouseException>(() => Export(template, template, 1, overwrite: true, code: "XK9"));
        Assert.Equal(WarehouseErrorKind.OutputNotWritable, ex.Kind);
    }

    /// <summary>Chạy trên các file mẫu THẬT nếu có trên máy (khác nhau ở số dòng hàng mẫu, style, chiều cao dòng).</summary>
    [Theory]
    [InlineData("Kho HCM", "Phiếu Xuất Kho.xlsx", "B5")]
    [InlineData("Kho HCM", "phiếu 1.xlsx", "B5")]
    [InlineData("ViVo", "Phiếu Xuất Kho.xlsx", "B5")]
    [InlineData("Thảo Điền", "PHIẾU XUẤT KHO.xlsx", "B5")]
    public void RealTemplates_AreDetectedAndExportedFaithfully(string folder, string file, string codeCell)
    {
        string template = Path.Combine(WarehouseTestData.RealTemplatesFolder, folder, file);
        if (!File.Exists(template))
            return; // máy không có file mẫu thật — các test tổng hợp ở trên vẫn bao phủ logic.

        string hash = WarehouseTestData.Sha256(template);
        WarehouseTemplateLayout layout = _reader.ReadTemplateLayout(template);
        Assert.Equal(codeCell, new CellReference(layout.TicketCodeRow, layout.TicketCodeColumn).FormatAsString());
        Assert.Equal(13, layout.DataFirstRow);

        string dir = TestData.NewTempDir();
        foreach (int count in new[] { 1, layout.DataRowCount, layout.DataRowCount + 7 })
        {
            string output = Path.Combine(dir, $"XK23609-{count}.xlsx");
            WarehouseExportResult result = Export(template, output, count, new WarehouseFontSettings
            {
                Scope = WarehouseFontApplyScope.ProductNameOnly, FontFamily = "Arial", FontSize = 11
            });
            Assert.Equal(count, result.RowCount);

            using IWorkbook wb = Open(output);
            Assert.Equal("Số : XK23609", Text(wb.GetSheetAt(0), codeCell));
        }

        Assert.Equal(hash, WarehouseTestData.Sha256(template));
    }
}

public class WarehouseDocumentServiceTests
{
    [Fact]
    public void Load_ThenValidate_BlocksOnErrors_AllowsWarnings()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string goods = WarehouseTestData.CreateGoods(dir,
            WarehouseTestData.Line("SP1", "Giày nữ Pucini", 4d),
            WarehouseTestData.Line("SP2", "Kính mắt Pucini", 0d));
        WarehouseDocumentService service = new();

        WarehouseDocument doc = service.Load(template, goods);
        WarehouseValidationReport report = service.Validate("23609", "XK", new DateTime(2026, 10, 10), doc, Path.Combine(dir, "out"), WarehouseFontSettings.KeepTemplate());
        Assert.False(report.CanExport);
        Assert.Contains(report.Errors, e => e.Contains("SP2") && e.Contains("lớn hơn 0"));

        doc.Rows[1].RequestedQuantity = 10;
        doc.Rows[1].ActualQuantity = 10;
        WarehouseProductNameService.Revalidate(doc.Rows[1]);
        report = service.Validate("23609", "XK", new DateTime(2026, 10, 10), doc, Path.Combine(dir, "out"), WarehouseFontSettings.KeepTemplate());
        Assert.True(report.CanExport, string.Join("\n", report.Errors));

        string output = WarehouseDocumentService.BuildOutputPath(Path.Combine(dir, "out"), "XK23609", doc.Layout.FileExtension);
        Assert.EndsWith(Path.Combine("out", "XK23609.xlsx"), output);
        WarehouseExportResult result = service.Export(new WarehouseExportRequest
        {
            TemplatePath = template, OutputPath = output, TicketCode = "XK23609", DocumentDate = new DateTime(2026, 10, 10), Rows = doc.Rows
        });

        using IWorkbook wb = WarehouseExcelReader.OpenWorkbook(result.OutputPath, "test");
        Assert.Equal("Giày nữ Pucini", wb.GetSheetAt(0).GetRow(13).GetCell(0).StringCellValue);
        Assert.Equal("Đôi", wb.GetSheetAt(0).GetRow(13).GetCell(1).StringCellValue);
        Assert.Equal(10, wb.GetSheetAt(0).GetRow(14).GetCell(3).NumericCellValue);
    }

    [Fact]
    public void Validate_ReportsMissingFontAndBadSize()
    {
        WarehouseValidationReport report = new WarehouseDocumentService().Validate("1", "XK", new DateTime(2026, 10, 10), null, "", new WarehouseFontSettings
        {
            Scope = WarehouseFontApplyScope.ProductNameOnly, FontFamily = "Font Không Tồn Tại 123", FontSize = 50
        });

        Assert.Contains(report.Errors, e => e.Contains("Cỡ chữ"));
        Assert.Contains(report.Errors, e => e.Contains("chưa được cài"));
        Assert.Contains(report.Errors, e => e.Contains("thư mục output"));
    }
}

[Collection("Excel")]
public class WarehouseSettingsAndPrinterTests
{
    [Fact]
    public void Settings_AreStoredSeparately_AndNeverTouchLabelConfig()
    {
        string labelConfig = Path.Combine(AppContext.BaseDirectory, "Config", "config.json");
        string before = WarehouseTestData.Sha256(labelConfig);

        string path = Path.Combine(TestData.NewTempDir(), "Config", "warehouse-export.json");
        WarehouseSettingsStore store = new(path);
        store.Save(new WarehouseExportSettings
        {
            PrinterName = "Máy in phiếu",
            FontScope = WarehouseFontApplyScope.ProductNameOnly,
            FontFamily = "Arial",
            FontSize = 11
        });

        WarehouseExportSettings loaded = new WarehouseSettingsStore(path).Load();
        Assert.Equal("Máy in phiếu", loaded.PrinterName);
        Assert.Equal(WarehouseFontApplyScope.ProductNameOnly, loaded.FontScope);
        Assert.Equal(before, WarehouseTestData.Sha256(labelConfig));
        Assert.DoesNotContain("PrinterName\": \"Máy in phiếu", File.ReadAllText(labelConfig));
    }

    [Fact]
    public void Settings_CorruptFile_FallsBackToDefaults_WithWarning()
    {
        string path = Path.Combine(TestData.NewTempDir(), "warehouse-export.json");
        File.WriteAllText(path, "{ hỏng");
        WarehouseSettingsStore store = new(path);
        Assert.Equal("XK", store.Load().TicketPrefix);
        Assert.NotNull(store.LoadWarning);
    }

    [Fact]
    public void Printers_AreListed_AndUnknownPrinterIsRejected()
    {
        WarehousePrinterService printers = new();
        string defaultBefore = printers.GetWindowsDefaultPrinter();

        Assert.False(printers.IsPrinterAvailable("Máy in không tồn tại 123"));
        Assert.False(printers.IsPrinterAvailable(""));
        foreach (string name in printers.GetInstalledPrinters().Take(3))
            Assert.False(string.IsNullOrWhiteSpace(name));

        Assert.Equal(defaultBefore, printers.GetWindowsDefaultPrinter());
    }

    [Fact]
    public async Task Print_UnknownPrinter_FailsBeforeTouchingExcel()
    {
        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        WarehouseException ex = await Assert.ThrowsAsync<WarehouseException>(() =>
            new WarehousePrinterService().PrintWorkbookAsync(template, "Máy in không tồn tại 123"));
        Assert.Equal(WarehouseErrorKind.PrinterNotFound, ex.Kind);
    }

    /// <summary>
    /// In THẬT qua Excel ra "Microsoft Print to PDF" (không tốn giấy) và xác
    /// nhận máy in mặc định Windows không đổi. Mở Excel nên chỉ chạy khi đặt
    /// biến môi trường KV_WAREHOUSE_PRINT_TEST=1.
    /// </summary>
    [Fact]
    public async Task Print_ToPdfPrinter_ProducesFile_AndKeepsWindowsDefault()
    {
        const string pdfPrinter = "Microsoft Print to PDF";
        WarehousePrinterService printers = new();
        if (Environment.GetEnvironmentVariable("KV_WAREHOUSE_PRINT_TEST") != "1" ||
            !printers.IsPrinterAvailable(pdfPrinter) || !WarehousePrinterService.IsExcelAvailable())
            return;

        string dir = TestData.NewTempDir();
        string template = WarehouseTestData.CreateTemplate(dir);
        string output = Path.Combine(dir, "XK-PRINT.xlsx");
        new WarehouseExcelWriter().Export(new WarehouseExportRequest
        {
            TemplatePath = template,
            OutputPath = output,
            TicketCode = "XK-PRINT",
            DocumentDate = new DateTime(2026, 10, 10),
            Rows = [new WarehouseProductRow { SourceRowNumber = 2, TicketName = "Giày nữ", Unit = "Đôi", RequestedQuantity = 4, ActualQuantity = 4 }]
        });

        string defaultBefore = printers.GetWindowsDefaultPrinter();
        string pdf = Path.Combine(dir, "XK-PRINT.pdf");
        await printers.PrintWorkbookAsync(output, pdfPrinter, printToFile: pdf);

        // Excel trả về khi đã giao lệnh cho hàng đợi in; bộ đệm in (spooler)
        // còn đang ghi file PDF — chờ tới khi ghi xong.
        byte[]? content = null;
        for (int i = 0; i < 120 && content == null; i++)
        {
            try
            {
                if (File.Exists(pdf) && new FileInfo(pdf).Length > 0)
                    content = File.ReadAllBytes(pdf);
            }
            catch (IOException)
            {
                // Spooler vẫn đang giữ file.
            }

            if (content == null)
                await Task.Delay(500);
        }

        Assert.NotNull(content);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(content!, 0, 5));
        Assert.Equal(defaultBefore, printers.GetWindowsDefaultPrinter());
    }
}
