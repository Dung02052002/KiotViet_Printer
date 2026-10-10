using KiotVietLabelPrinter.Models.Warehouse;

namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>Tình trạng file phiếu của lần xuất gần nhất.</summary>
public enum WarehouseExportState
{
    /// <summary>Chưa xuất lần nào (hoặc đã xoá dữ liệu).</summary>
    None,

    /// <summary>File đúng là bản vừa xuất từ dữ liệu / thiết lập đang hiển thị — xem trước và in được.</summary>
    Current,

    /// <summary>Dữ liệu, mã phiếu, font, thiết lập in, file nguồn hoặc thư mục output đã đổi sau lần xuất.</summary>
    Stale,

    /// <summary>File kết quả không còn trên đĩa.</summary>
    Missing,

    /// <summary>File kết quả bị sửa / ghi đè bên ngoài sau khi xuất.</summary>
    ModifiedExternally
}

/// <summary>
/// Theo dõi file phiếu đã xuất để màn hình không bao giờ xem trước hay in một
/// bản CŨ. Việc file kết quả còn tồn tại không đủ để coi là hợp lệ: bản xuất
/// chỉ dùng được khi chưa có thay đổi nào ảnh hưởng tới nội dung file kể từ
/// lần xuất, VÀ file trên đĩa vẫn đúng dấu (giờ ghi + kích thước) lúc xuất.
/// </summary>
public sealed class WarehouseExportTracker
{
    private bool _stale;
    private WarehouseFileStamp? _previewedStamp;

    /// <summary>Kết quả lần xuất gần nhất (kể cả khi đã cũ) — null nếu chưa xuất.</summary>
    public WarehouseExportResult? Last { get; private set; }

    /// <summary>Ghi nhận một lần xuất thành công: từ giờ bản này là bản hiện hành, chưa được xem trước.</summary>
    public void Record(WarehouseExportResult result)
    {
        Last = result;
        _stale = false;
        _previewedStamp = null;
    }

    /// <summary>Một thay đổi ảnh hưởng tới nội dung file kết quả: bản đã xuất (nếu có) thành bản cũ.</summary>
    public void MarkStale()
    {
        if (Last != null)
            _stale = true;
    }

    /// <summary>Bỏ hẳn bản đã xuất (xoá dữ liệu phiếu).</summary>
    public void Reset()
    {
        Last = null;
        _stale = false;
        _previewedStamp = null;
    }

    /// <summary>Đã dựng xem trước bản in cho file có dấu <paramref name="stamp"/>.</summary>
    public void MarkPreviewed(WarehouseFileStamp stamp) => _previewedStamp = stamp;

    /// <summary>Bản xem trước trước đó không còn đại diện cho bản in (vd đổi máy in → phân trang khác).</summary>
    public void ForgetPreview() => _previewedStamp = null;

    /// <summary>Bản hiện hành đã được xem trước (đúng file, đúng dấu) chưa.</summary>
    public bool WasPreviewed => Last != null && _previewedStamp != null && _previewedStamp == Last.Stamp;

    /// <summary>Kiểm tra lại ngay trên đĩa (không dùng kết quả cũ) — gọi trước mỗi lần xem trước / in.</summary>
    public WarehouseExportState Check()
    {
        if (Last == null)
            return WarehouseExportState.None;
        if (_stale)
            return WarehouseExportState.Stale;
        if (!File.Exists(Last.OutputPath))
            return WarehouseExportState.Missing;
        return Last.Stamp.Matches(Last.OutputPath) ? WarehouseExportState.Current : WarehouseExportState.ModifiedExternally;
    }

    /// <summary>Lý do không xem trước / in được, theo <paramref name="state"/> (rỗng khi <see cref="WarehouseExportState.Current"/>).</summary>
    public string Describe(WarehouseExportState state) => state switch
    {
        WarehouseExportState.None => "Chưa có file phiếu. Hãy bấm XUẤT PHIẾU EXCEL trước.",
        WarehouseExportState.Stale =>
            "Dữ liệu, mã phiếu, font, thiết lập in hoặc thư mục output đã thay đổi sau lần xuất trước — bản đã xuất là bản CŨ.\n\n" +
            "Hãy bấm XUẤT PHIẾU EXCEL lại.",
        WarehouseExportState.Missing => $"File phiếu không còn tồn tại:\n{Last?.OutputPath}\n\nHãy xuất lại.",
        WarehouseExportState.ModifiedExternally =>
            $"File phiếu đã bị sửa hoặc ghi đè bên ngoài sau khi xuất:\n{Last?.OutputPath}\n\nHãy xuất lại để in đúng dữ liệu đã kiểm tra.",
        _ => ""
    };
}
