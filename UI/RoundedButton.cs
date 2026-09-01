using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.UI;

public enum ButtonVariant
{
    Primary,
    Secondary,
    Outline,
    Danger,
    Ghost
}

// Button bo góc, phẳng (flat), có hiệu ứng hover/pressed mượt — thay cho
// System.Windows.Forms.Button mặc định (vuông cạnh, style Windows cũ).
//
// Không dùng ControlStyles.SupportsTransparentBackColor (kiểu "nền trong suốt"
// của WinForms): cơ chế đó nhờ control cha tự vẽ lại phần nền phía sau, nhưng
// khi control cha cũng là control tự vẽ (RoundedPanel) thì việc phối hợp vẽ
// dễ bị lỗi (còn sót lại nét vẽ cũ ở góc bo tròn). Thay vào đó, ta biết trước
// màu nền của nơi đặt control (ContainerColor) và tự tô nó lên trước, sau đó
// vẽ khối bo góc chống răng cưa đè lên trên — chắc chắn không bị "ma hình".
public class RoundedButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 8;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonVariant Variant { get; set; } = ButtonVariant.Secondary;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Background;

    private bool _hover;
    private bool _pressed;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = AppTheme.Fonts.Button;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        pevent.Graphics.Clear(ContainerColor);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        Graphics g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        (Color fill, Color border, Color fore) = ResolveColors();

        using GraphicsPath path = AppTheme.RoundedRect(rect, CornerRadius);

        using (SolidBrush brush = new(fill))
            g.FillPath(brush, path);

        if (border != Color.Transparent)
        {
            using Pen pen = new(border, 1f);
            g.DrawPath(pen, path);
        }

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            rect,
            fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    private (Color fill, Color border, Color fore) ResolveColors()
    {
        if (!Enabled)
            return (AppTheme.Colors.Disabled, Color.Transparent, AppTheme.Colors.DisabledText);

        return Variant switch
        {
            ButtonVariant.Primary => (
                _pressed ? AppTheme.Colors.PrimaryPressed : _hover ? AppTheme.Colors.PrimaryHover : AppTheme.Colors.Primary,
                Color.Transparent,
                Color.White),

            ButtonVariant.Danger => (
                _pressed ? AppTheme.Colors.DangerPressed : _hover ? AppTheme.Colors.DangerHover : AppTheme.Colors.Danger,
                Color.Transparent,
                Color.White),

            ButtonVariant.Outline => (
                _pressed ? AppTheme.Colors.PrimaryLight : _hover ? AppTheme.Colors.PrimaryLight : ContainerColor,
                AppTheme.Colors.Primary,
                AppTheme.Colors.Primary),

            ButtonVariant.Ghost => (
                _pressed ? AppTheme.Colors.SurfacePressed : _hover ? AppTheme.Colors.SurfaceHover : ContainerColor,
                Color.Transparent,
                AppTheme.Colors.TextSecondary),

            _ => (
                _pressed ? AppTheme.Colors.SurfacePressed : _hover ? AppTheme.Colors.SurfaceHover : AppTheme.Colors.Surface,
                AppTheme.Colors.BorderStrong,
                AppTheme.Colors.TextPrimary)
        };
    }
}
