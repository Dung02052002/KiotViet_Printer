using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Fonts;
using KiotVietLabelPrinter.Models.Warehouse;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>
/// Điều phối chức năng Phiếu xuất kho: đọc file mẫu + file hàng hóa, đối
/// chiếu thành các dòng phiếu, kiểm tra toàn bộ trước khi xuất, rồi giao cho
/// WarehouseExcelWriter ghi. Không hiện UI — mọi lỗi trả về bằng
/// WarehouseException / WarehouseValidationReport với thông báo tiếng Việt.
/// </summary>
public class WarehouseDocumentService
{
    private readonly WarehouseExcelReader _reader;
    private readonly WarehouseExcelWriter _writer;
    private readonly WarehouseProductNameService _nameService;
    private readonly InstalledFontService _fontService;

    public WarehouseDocumentService(
        WarehouseExcelReader? reader = null,
        WarehouseExcelWriter? writer = null,
        WarehouseProductNameService? nameService = null,
        InstalledFontService? fontService = null)
    {
        _reader = reader ?? new WarehouseExcelReader();
        _writer = writer ?? new WarehouseExcelWriter();
        _nameService = nameService ?? new WarehouseProductNameService();
        _fontService = fontService ?? new InstalledFontService();
    }

    public WarehouseTemplateLayout ReadTemplate(string templatePath) => _reader.ReadTemplateLayout(templatePath);

    public WarehouseDocument Load(string templatePath, string goodsPath)
    {
        WarehouseTemplateLayout layout = _reader.ReadTemplateLayout(templatePath);
        WarehouseGoodsFile goods = _reader.ReadGoods(goodsPath);
        if (goods.Lines.Count == 0)
            throw new WarehouseException(WarehouseErrorKind.GoodsStructure,
                $"File hàng hóa không có dòng hàng nào dưới dòng tiêu đề (dòng {goods.HeaderRowNumber}).");

        return new WarehouseDocument
        {
            TemplatePath = templatePath,
            Layout = layout,
            GoodsPath = goodsPath,
            Goods = goods,
            Rows = _nameService.BuildRows(goods)
        };
    }

    /// <summary>Tên file kết quả: mã phiếu + đuôi của file mẫu (mẫu .xls thì kết quả .xls).</summary>
    public static string BuildOutputPath(string outputFolder, string ticketCode, string templateExtension)
    {
        string extension = string.IsNullOrWhiteSpace(templateExtension) ? ".xlsx" : templateExtension.ToLowerInvariant();
        return Path.Combine(outputFolder, ticketCode + extension);
    }

    /// <summary>
    /// Kiểm tra MỌI điều kiện xuất phiếu. Lỗi = chặn xuất; cảnh báo = vẫn xuất
    /// được nhưng nên xem lại.
    /// </summary>
    public WarehouseValidationReport Validate(
        string ticketInput, string prefix, DateTime documentDate, WarehouseDocument? document, string outputFolder,
        WarehouseFontSettings font, WarehousePageSettings? page = null)
    {
        WarehouseValidationReport report = new();

        TicketCodeResult code = WarehouseTicketCodeService.Normalize(ticketInput, prefix);
        if (!code.IsValid)
            report.Errors.Add(code.Error!);

        if (WarehouseDocumentDateService.Validate(documentDate) is { } dateError)
            report.Errors.Add(dateError);

        if (document == null)
        {
            report.Errors.Add("Chưa đọc được dữ liệu: hãy chọn file phiếu mẫu và file hàng hóa.");
        }
        else
        {
            if (!File.Exists(document.TemplatePath))
                report.Errors.Add($"File phiếu mẫu không còn tồn tại: {document.TemplatePath}");
            if (!File.Exists(document.GoodsPath))
                report.Errors.Add($"File hàng hóa không còn tồn tại: {document.GoodsPath}");

            if (document.Rows.Count == 0)
                report.Errors.Add("Không có dòng hàng nào để xuất.");

            foreach (WarehouseProductRow row in document.Rows.Where(r => r.Status == WarehouseRowStatus.Error))
                report.Errors.Add($"Dòng {row.SourceRowNumber}{CodeSuffix(row)}: {string.Join(" ", row.Messages)}");
            foreach (WarehouseProductRow row in document.Rows.Where(r => r.Status == WarehouseRowStatus.Warning))
                report.Warnings.Add($"Dòng {row.SourceRowNumber}{CodeSuffix(row)}: {string.Join(" ", row.Messages)}");

            if (!document.Goods.HasCodeColumn)
                report.Warnings.Add("File hàng hóa không có cột Mã hàng — tên hàng lấy nguyên từ cột Tên hàng, không đối chiếu theo mã.");
        }

        if (string.IsNullOrWhiteSpace(outputFolder))
            report.Errors.Add("Chưa chọn thư mục output.");
        else if (!Path.IsPathFullyQualified(outputFolder))
            report.Errors.Add($"Thư mục output không hợp lệ: {outputFolder}");
        else if (code.IsValid && document != null)
        {
            string output = BuildOutputPath(outputFolder, code.Code!, document.Layout.FileExtension);
            if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(document.TemplatePath), StringComparison.OrdinalIgnoreCase))
                report.Errors.Add("File kết quả sẽ trùng đúng file phiếu mẫu — chọn thư mục output khác.");
            else if (File.Exists(output))
                report.ExistingOutputPath = output;
        }

        ValidateFont(font, report);
        if (document != null)
            ValidatePage(page ?? WarehousePageSettings.KeepTemplate(), document.Layout, report);
        return report;
    }

    /// <summary>
    /// Thiết lập trang in người dùng đổi: chặn giá trị Excel không in được
    /// (lề âm, lề lớn hơn khổ giấy, tỷ lệ ngoài 10–400%), cảnh báo những gì
    /// có thể làm chữ quá nhỏ. Không tự sửa giá trị nào.
    /// </summary>
    public static void ValidatePage(WarehousePageSettings page, WarehouseTemplateLayout layout, WarehouseValidationReport report)
    {
        if (page.KeepsTemplate)
            return;

        WarehousePageSetup result = page.ApplyTo(layout.Page);

        if (page.Margins is { } m)
        {
            if (new[] { m.Left, m.Right, m.Top, m.Bottom }.Any(v => v < 0 || double.IsNaN(v)))
                report.Errors.Add("Lề trang không được âm.");

            if (WarehousePaperSizes.Find(result.PaperSize) is { } paper)
            {
                double widthIn = (result.Landscape ? paper.HeightMm : paper.WidthMm) / 25.4;
                double heightIn = (result.Landscape ? paper.WidthMm : paper.HeightMm) / 25.4;
                // Chừa tối thiểu 1 inch (2,54 cm) vùng in mỗi chiều.
                if (m.Left + m.Right > widthIn - 1 || m.Top + m.Bottom > heightIn - 1)
                    report.Errors.Add($"Lề trang quá lớn so với khổ {paper.Name} {(result.Landscape ? "ngang" : "dọc")} — không còn chỗ để in.");
            }

            if (layout.Page.HasHeaderFooter && (m.Top < layout.Page.Margins.Header || m.Bottom < layout.Page.Margins.Footer))
                report.Warnings.Add("Lề trên/dưới nhỏ hơn lề header/footer của mẫu — header/footer có thể đè lên nội dung phiếu.");
        }

        if (page.Scale is { Mode: WarehouseScaleMode.Percent } scale)
        {
            if (scale.Percent < WarehousePageSettings.MinScalePercent || scale.Percent > WarehousePageSettings.MaxScalePercent)
                report.Errors.Add($"Tỷ lệ in phải từ {WarehousePageSettings.MinScalePercent}% đến {WarehousePageSettings.MaxScalePercent}%.");
            else if (layout.NameFontSize * scale.Percent / 100.0 < 7)
                report.Warnings.Add($"Tỷ lệ in {scale.Percent}%: tên hàng {layout.NameFontSize:0.#}pt sẽ in ra chỉ khoảng " +
                                    $"{layout.NameFontSize * scale.Percent / 100.0:0.#}pt — có thể khó đọc.");
        }
        else if (page.Scale is { Mode: WarehouseScaleMode.FitOnePage })
        {
            report.Warnings.Add("\"Vừa một trang\": Excel sẽ co nhỏ cả phiếu vào 1 trang — nhiều dòng hàng thì chữ có thể rất nhỏ. " +
                                "Hãy kiểm tra bằng XEM TRƯỚC BẢN IN.");
        }
    }

    private void ValidateFont(WarehouseFontSettings font, WarehouseValidationReport report)
    {
        if (font.Scope == WarehouseFontApplyScope.KeepTemplate)
            return;

        if (font.FontSize < WarehouseFontSettings.MinFontSize || font.FontSize > WarehouseFontSettings.MaxFontSize)
            report.Errors.Add($"Cỡ chữ phải từ {WarehouseFontSettings.MinFontSize} đến {WarehouseFontSettings.MaxFontSize} pt.");

        FontCheck check = _fontService.Check(font.FontFamily);
        if (!check.IsInstalled)
            report.Errors.Add($"Font \"{font.FontFamily}\" chưa được cài trên máy này — chọn font khác.");
        else if (!check.SupportsVietnamese)
            report.Warnings.Add($"Font \"{font.FontFamily}\" thiếu một số chữ tiếng Việt có dấu — chữ có thể in ra ô vuông.");
    }

    public WarehouseExportResult Export(WarehouseExportRequest request) => _writer.Export(request);

    private static string CodeSuffix(WarehouseProductRow row) =>
        string.IsNullOrEmpty(row.ProductCode) ? "" : $" ({row.ProductCode})";
}
