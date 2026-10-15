namespace KiotVietLabelPrinter.UI.Controls;

// DataGridView with buffered painting to reduce flicker while scrolling and resizing.
public class SmoothDataGridView : DataGridView
{
    public SmoothDataGridView()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    /// <summary>
    /// Xoá hết dòng và giải phóng ảnh (thumbnail) nằm trong các ô. Rows.Clear()
    /// không Dispose ảnh trong ô — mỗi ảnh giữ một GDI handle, xoá/thêm danh
    /// sách nhiều lần sẽ cạn giới hạn 10.000 handle của tiến trình và app lỗi
    /// "Parameter is not valid" khi vẽ.
    /// </summary>
    public void ClearRowsAndDisposeImages()
    {
        List<Image> images = new();
        foreach (DataGridViewRow row in Rows)
        {
            foreach (DataGridViewCell cell in row.Cells)
            {
                if (cell.Value is Image image)
                    images.Add(image);
            }
        }

        // Xoá dòng TRƯỚC rồi mới Dispose — tránh grid vẽ lại một ảnh đã giải phóng.
        Rows.Clear();
        foreach (Image image in images)
            image.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        // Grid gắn DataSource (Xem trước, Lịch sử...) không chứa thumbnail và
        // không cho Rows.Clear() — chỉ dọn grid không gắn dữ liệu.
        if (disposing && !IsDisposed && DataSource == null)
            ClearRowsAndDisposeImages();

        base.Dispose(disposing);
    }
}
