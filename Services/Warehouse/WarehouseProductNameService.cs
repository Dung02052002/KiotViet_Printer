using System.Text;
using KiotVietLabelPrinter.Models.Warehouse;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>
/// Đối chiếu tên hàng theo MÃ HÀNG trong file hàng hóa (file xuất từ KiotViet
/// có cột Mã hàng + Tên hàng — tên ở đây chính là tên đầy đủ in trên phiếu).
///
/// Quy tắc:
/// - So mã sau khi bỏ khoảng trắng thừa và không phân biệt hoa/thường; dữ
///   liệu gốc giữ nguyên.
/// - Cùng một mã mà tên khác nhau → LỖI (không tự chọn tên nào).
/// - Cùng mã, cùng tên ở nhiều dòng → cảnh báo, giữ nguyên từng dòng và số
///   lượng (không tự cộng gộp).
/// - Dòng thiếu tên: chỉ lấy tên từ một dòng KHÁC CÓ CÙNG MÃ trong file; không
///   có thì báo lỗi — không đoán tên, không ghép theo thứ tự dòng.
/// - Không dòng nào bị bỏ qua âm thầm: dòng có dữ liệu mà không xuất được đều
///   hiện trong bảng với trạng thái lỗi.
/// </summary>
public class WarehouseProductNameService
{
    private readonly WarehouseUnitService _unitService;

    public WarehouseProductNameService(WarehouseUnitService? unitService = null)
    {
        _unitService = unitService ?? new WarehouseUnitService();
    }

    public static string NormalizeCode(string? code) =>
        VietnameseText.CollapseSpaces(code).ToUpperInvariant();

    // NFC: cùng một tên ở dạng dấu rời (NFD) không được tính là "tên khác".
    private static string NormalizeName(string? name) =>
        VietnameseText.CollapseSpaces(name).Normalize(NormalizationForm.FormC).ToUpperInvariant();

    public List<WarehouseProductRow> BuildRows(WarehouseGoodsFile goods)
    {
        // Gom theo mã để phát hiện mã trùng / mã trùng nhưng khác tên.
        Dictionary<string, List<WarehouseGoodsLine>> byCode = goods.Lines
            .Where(l => NormalizeCode(l.ProductCode).Length > 0)
            .GroupBy(l => NormalizeCode(l.ProductCode))
            .ToDictionary(g => g.Key, g => g.ToList());

        List<WarehouseProductRow> rows = new();
        foreach (WarehouseGoodsLine line in goods.Lines)
        {
            string code = VietnameseText.CollapseSpaces(line.ProductCode);
            string sourceName = VietnameseText.CollapseSpaces(line.ProductName);

            WarehouseProductRow row = new()
            {
                SourceRowNumber = line.SourceRowNumber,
                ProductCode = code,
                SourceName = sourceName,
                TicketName = sourceName,
                RequestedQuantity = line.Quantity,
                ActualQuantity = line.Quantity
            };

            if (code.Length == 0)
            {
                if (goods.HasCodeColumn)
                    row.MatchIssues.Add((WarehouseRowStatus.Warning, "Dòng không có mã hàng — tên hàng lấy nguyên từ file, không đối chiếu được theo mã."));
            }
            else
            {
                ResolveByCode(row, byCode[NormalizeCode(code)]);
            }

            if (row.TicketName.Length == 0 && code.Length == 0)
                row.MatchIssues.Add((WarehouseRowStatus.Error, "Dòng có số lượng nhưng không có mã hàng lẫn tên hàng."));

            UnitDetection unit = _unitService.Detect(row.TicketName);
            row.Unit = unit.Unit;
            if (unit.IsAmbiguous && row.TicketName.Length > 0)
                row.MatchIssues.Add((WarehouseRowStatus.Warning, unit.Reason ?? "Đơn vị tính chưa chắc chắn — kiểm tra lại."));

            Revalidate(row);
            rows.Add(row);
        }

        return rows;
    }

    private static void ResolveByCode(WarehouseProductRow row, List<WarehouseGoodsLine> sameCode)
    {
        List<WarehouseGoodsLine> named = sameCode
            .Where(l => NormalizeName(l.ProductName).Length > 0)
            .ToList();
        List<string> distinctNames = named
            .Select(l => NormalizeName(l.ProductName))
            .Distinct()
            .ToList();
        string lines = string.Join(", ", sameCode.Select(l => l.SourceRowNumber));

        if (distinctNames.Count > 1)
        {
            row.MatchIssues.Add((WarehouseRowStatus.Error,
                $"Mã hàng {row.ProductCode} xuất hiện ở các dòng {lines} với {distinctNames.Count} tên khác nhau — sửa lại file hàng hóa cho thống nhất."));
            return;
        }

        if (sameCode.Count > 1)
        {
            row.MatchIssues.Add((WarehouseRowStatus.Warning,
                $"Mã hàng {row.ProductCode} lặp lại ở các dòng {lines} — mỗi dòng giữ nguyên số lượng riêng, không cộng gộp."));
        }

        if (row.TicketName.Length > 0)
            return;

        if (named.Count == 0)
        {
            row.MatchIssues.Add((WarehouseRowStatus.Error,
                $"Không tìm thấy tên hàng cho mã {row.ProductCode} trong file hàng hóa."));
            return;
        }

        WarehouseGoodsLine source = named[0];
        row.TicketName = VietnameseText.CollapseSpaces(source.ProductName);
        row.MatchIssues.Add((WarehouseRowStatus.Warning,
            $"Dòng thiếu tên: đã lấy tên theo cùng mã {row.ProductCode} ở dòng {source.SourceRowNumber}."));
    }

    /// <summary>
    /// Tính lại trạng thái + thông báo của một dòng — gọi lại sau khi người
    /// dùng sửa tên/đơn vị/số lượng ở bảng xem trước.
    /// </summary>
    public static void Revalidate(WarehouseProductRow row)
    {
        row.Messages.Clear();
        WarehouseRowStatus status = WarehouseRowStatus.Ok;

        void Add(WarehouseRowStatus level, string message)
        {
            row.Messages.Add(message);
            if (level > status)
                status = level;
        }

        foreach ((WarehouseRowStatus level, string message) in row.MatchIssues)
            Add(level, message);

        if (string.IsNullOrWhiteSpace(row.TicketName) && row.MatchIssues.All(i => i.Level != WarehouseRowStatus.Error))
            Add(WarehouseRowStatus.Error, "Tên hàng trên phiếu đang trống.");

        if (string.IsNullOrWhiteSpace(row.Unit))
            Add(WarehouseRowStatus.Error, "Đơn vị tính đang trống.");

        CheckQuantity(row.RequestedQuantity, "Số lượng yêu cầu", Add);
        CheckQuantity(row.ActualQuantity, "Số lượng thực xuất", Add);

        row.Status = status;
    }

    private static void CheckQuantity(double? quantity, string label, Action<WarehouseRowStatus, string> add)
    {
        if (quantity == null)
            add(WarehouseRowStatus.Error, $"{label} đang trống hoặc không phải số.");
        else if (double.IsNaN(quantity.Value) || double.IsInfinity(quantity.Value))
            add(WarehouseRowStatus.Error, $"{label} không hợp lệ.");
        else if (quantity.Value <= 0)
            add(WarehouseRowStatus.Error, $"{label} phải lớn hơn 0 (đang là {quantity.Value:0.###}).");
        else if (quantity.Value != Math.Floor(quantity.Value))
            add(WarehouseRowStatus.Warning, $"{label} là số lẻ ({quantity.Value:0.###}) — kiểm tra lại.");
    }
}
