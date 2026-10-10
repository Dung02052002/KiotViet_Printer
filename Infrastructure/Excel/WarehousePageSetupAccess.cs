using KiotVietLabelPrinter.Models.Warehouse;
using NPOI.SS.UserModel;

namespace KiotVietLabelPrinter.Infrastructure.Excel;

/// <summary>
/// Đọc / ghi thiết lập trang in của sheet phiếu qua NPOI. Ghi CHỈ những mục
/// người dùng đã đổi (<see cref="WarehousePageSettings"/>); mục null giữ
/// nguyên đúng giá trị trong file mẫu.
/// </summary>
public static class WarehousePageSetupAccess
{
    public static WarehousePageSetup Read(IWorkbook workbook, int sheetIndex)
    {
        ISheet sheet = workbook.GetSheetAt(sheetIndex);
        IPrintSetup p = sheet.PrintSetup;
        IHeader header = sheet.Header;
        IFooter footer = sheet.Footer;

        return new WarehousePageSetup
        {
            PaperSize = p.PaperSize,
            Landscape = p.Landscape,
            Margins = new WarehouseMargins(
                sheet.GetMargin(MarginType.LeftMargin),
                sheet.GetMargin(MarginType.RightMargin),
                sheet.GetMargin(MarginType.TopMargin),
                sheet.GetMargin(MarginType.BottomMargin),
                sheet.GetMargin(MarginType.HeaderMargin),
                sheet.GetMargin(MarginType.FooterMargin)),
            FitToPage = sheet.FitToPage,
            Scale = p.Scale,
            FitWidth = p.FitWidth,
            FitHeight = p.FitHeight,
            PrintArea = workbook.GetPrintArea(sheetIndex),
            ManualRowBreakCount = sheet.RowBreaks?.Length ?? 0,
            HasHeaderFooter = new[] { header.Left, header.Center, header.Right, footer.Left, footer.Center, footer.Right }
                .Any(s => !string.IsNullOrWhiteSpace(s)),
            CenterHorizontally = sheet.HorizontallyCenter,
            CenterVertically = sheet.VerticallyCenter
        };
    }

    public static void Apply(ISheet sheet, WarehousePageSettings settings)
    {
        if (settings.KeepsTemplate)
            return;

        IPrintSetup p = sheet.PrintSetup;
        if (settings.PaperSize is short paper)
            p.PaperSize = paper;
        if (settings.Landscape is bool landscape)
            p.Landscape = landscape;

        if (settings.Margins is { } m)
        {
            sheet.SetMargin(MarginType.LeftMargin, m.Left);
            sheet.SetMargin(MarginType.RightMargin, m.Right);
            sheet.SetMargin(MarginType.TopMargin, m.Top);
            sheet.SetMargin(MarginType.BottomMargin, m.Bottom);
        }

        switch (settings.Scale?.Mode)
        {
            case WarehouseScaleMode.Percent:
                sheet.FitToPage = false;
                p.Scale = settings.Scale.Percent;
                break;
            case WarehouseScaleMode.FitWidth:
                sheet.FitToPage = true;
                p.FitWidth = 1;
                p.FitHeight = 0;
                break;
            case WarehouseScaleMode.FitOnePage:
                sheet.FitToPage = true;
                p.FitWidth = 1;
                p.FitHeight = 1;
                break;
        }
    }
}
