namespace KiotVietLabelPrinter.Models.Warehouse;

/// <summary>Phiếu xuất kho đang soạn: file mẫu đã phân tích + các dòng hàng đã đối chiếu.</summary>
public sealed class WarehouseDocument
{
    public required string TemplatePath { get; init; }
    public required WarehouseTemplateLayout Layout { get; init; }
    public required string GoodsPath { get; init; }
    public required WarehouseGoodsFile Goods { get; init; }
    public required List<WarehouseProductRow> Rows { get; init; }

    public int ErrorCount => Rows.Count(r => r.Status == WarehouseRowStatus.Error);
    public int WarningCount => Rows.Count(r => r.Status == WarehouseRowStatus.Warning);
}

/// <summary>Kết quả kiểm tra trước khi xuất: lỗi chặn xuất + cảnh báo cần xem lại.</summary>
public sealed class WarehouseValidationReport
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    /// <summary>File kết quả cùng tên đã có — không phải lỗi, nhưng phải hỏi trước khi ghi đè.</summary>
    public string? ExistingOutputPath { get; set; }

    public bool CanExport => Errors.Count == 0;
}
