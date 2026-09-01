namespace KiotVietLabelPrinter.UI;

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
}
