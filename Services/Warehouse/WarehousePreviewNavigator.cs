namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>
/// Trang đang xem trong cửa sổ Xem trước bản in (tính từ 1). Tổng số trang là
/// số trang THỰC TẾ của bản xem trước — không bao giờ tự suy ra từ số dòng.
/// </summary>
public sealed class WarehousePreviewNavigator
{
    public int PageCount { get; }
    public int Current { get; private set; }

    public WarehousePreviewNavigator(int pageCount)
    {
        if (pageCount < 1)
            throw new ArgumentOutOfRangeException(nameof(pageCount), pageCount, "Bản xem trước phải có ít nhất 1 trang.");
        PageCount = pageCount;
        Current = 1;
    }

    public bool CanGoBack => Current > 1;
    public bool CanGoForward => Current < PageCount;

    public string Label => $"Trang {Current} / {PageCount}";

    /// <summary>Chuyển tới trang <paramref name="page"/> (ngoài phạm vi thì về trang đầu/cuối). Trả về true nếu trang đổi.</summary>
    public bool GoTo(int page)
    {
        int target = Math.Clamp(page, 1, PageCount);
        if (target == Current)
            return false;
        Current = target;
        return true;
    }

    public bool First() => GoTo(1);
    public bool Previous() => GoTo(Current - 1);
    public bool Next() => GoTo(Current + 1);
    public bool Last() => GoTo(PageCount);
}
