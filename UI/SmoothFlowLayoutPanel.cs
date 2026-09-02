namespace KiotVietLabelPrinter.UI;

// Buffered category container to avoid flashing when cards are rebuilt or scrolled.
public class SmoothFlowLayoutPanel : FlowLayoutPanel
{
    public SmoothFlowLayoutPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }
}
