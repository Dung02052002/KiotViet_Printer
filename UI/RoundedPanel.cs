using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.UI;

// Panel bo góc dùng làm "card" (khối nội dung nổi trên nền xám của form).
// Hỗ trợ hiệu ứng đổi viền/nền khi hover, dùng cho các card có thể bấm được.
//
// ContainerColor = màu nền của nơi đặt panel này (form/panel cha), được tô
// trước để lấp phần góc ngoài khối bo tròn — xem ghi chú trong RoundedButton
// về lý do không dùng ControlStyles.SupportsTransparentBackColor.
public class RoundedPanel : Panel
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 12;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = AppTheme.Colors.Surface;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = AppTheme.Colors.Border;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderThickness { get; set; } = 1;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Background;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HoverEffect { get; set; }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverFillColor { get; set; } = AppTheme.Colors.Surface;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBorderColor { get; set; } = AppTheme.Colors.Primary;

    private bool _hover;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (HoverEffect)
        {
            _hover = true;
            Invalidate();
        }

        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (HoverEffect)
        {
            _hover = false;
            Invalidate();
        }

        base.OnMouseLeave(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(ContainerColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = AppTheme.RoundedRect(rect, CornerRadius);

        Color fill = _hover && HoverEffect ? HoverFillColor : FillColor;
        Color border = _hover && HoverEffect ? HoverBorderColor : BorderColor;

        using (SolidBrush brush = new(fill))
            g.FillPath(brush, path);

        if (BorderThickness > 0)
        {
            using Pen pen = new(border, BorderThickness);
            g.DrawPath(pen, path);
        }

        base.OnPaint(e);
    }
}
