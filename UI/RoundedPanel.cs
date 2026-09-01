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
    public int CornerRadius { get; set; } = 16;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = AppTheme.Colors.Surface;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BorderColor { get; set; } = AppTheme.Colors.Border;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int BorderThickness { get; set; } = 1;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Background;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool HoverEffect
    {
        get => _hoverEffect;
        set
        {
            _hoverEffect = value;
            SetStyle(ControlStyles.Selectable, value);
            TabStop = value;
            Invalidate();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverFillColor { get; set; } = AppTheme.Colors.Surface;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverBorderColor { get; set; } = AppTheme.Colors.Primary;

    private bool _hover;
    private bool _hoverEffect;
    private bool _visualStateInitialized;
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 15 };
    private Color _currentFill;
    private Color _currentBorder;
    private Color _targetFill;
    private Color _targetBorder;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        _animationTimer.Tick += AnimateColors;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (HoverEffect)
            SetHoverState(true);

        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (HoverEffect)
            SetHoverState(ClientRectangle.Contains(PointToClient(Cursor.Position)));

        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (HoverEffect)
            Select();

        base.OnMouseDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HoverEffect && e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnClick(EventArgs.Empty);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        if (e.Control != null)
            AttachHoverHandlers(e.Control);

        base.OnControlAdded(e);
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

        EnsureVisualState();

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = AppTheme.RoundedRect(rect, CornerRadius);

        using (SolidBrush brush = new(_currentFill))
            g.FillPath(brush, path);

        if (BorderThickness > 0 && _currentBorder.A > 0)
        {
            using Pen pen = new(_currentBorder, BorderThickness);
            g.DrawPath(pen, path);
        }

        if (HoverEffect && Focused && ShowFocusCues)
        {
            Rectangle focusRect = Rectangle.Inflate(rect, -3, -3);
            using GraphicsPath focusPath = AppTheme.RoundedRect(focusRect, Math.Max(1, CornerRadius - 3));
            using Pen focusPen = new(AppTheme.Colors.FocusRing, 1.5f);
            g.DrawPath(focusPen, focusPath);
        }

        base.OnPaint(e);
    }

    private void AttachHoverHandlers(Control control)
    {
        control.MouseEnter += ChildMouseEnter;
        control.MouseLeave += ChildMouseLeave;
        control.MouseDown += ChildMouseDown;
        control.ControlAdded += ChildControlAdded;

        foreach (Control child in control.Controls)
            AttachHoverHandlers(child);
    }

    private void ChildControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
            AttachHoverHandlers(e.Control);
    }

    private void ChildMouseEnter(object? sender, EventArgs e)
    {
        if (HoverEffect)
            SetHoverState(true);
    }

    private void ChildMouseLeave(object? sender, EventArgs e)
    {
        if (HoverEffect)
            SetHoverState(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    private void ChildMouseDown(object? sender, MouseEventArgs e)
    {
        if (HoverEffect)
            Select();
    }

    private void EnsureVisualState()
    {
        if (_visualStateInitialized)
            return;

        (_currentFill, _currentBorder) = ResolveColors();
        (_targetFill, _targetBorder) = (_currentFill, _currentBorder);
        _visualStateInitialized = true;
    }

    private void SetHoverState(bool hover)
    {
        if (_hover == hover)
            return;

        _hover = hover;

        if (!_visualStateInitialized)
        {
            Invalidate();
            return;
        }

        (_targetFill, _targetBorder) = ResolveColors();

        if (!AppTheme.MotionEnabled)
        {
            (_currentFill, _currentBorder) = (_targetFill, _targetBorder);
            Invalidate();
            return;
        }

        _animationTimer.Start();
    }

    private (Color fill, Color border) ResolveColors()
    {
        return _hover && HoverEffect
            ? (HoverFillColor, HoverBorderColor)
            : (FillColor, BorderColor);
    }

    private void AnimateColors(object? sender, EventArgs e)
    {
        const float speed = 0.2f;
        _currentFill = AppTheme.Blend(_currentFill, _targetFill, speed);
        _currentBorder = AppTheme.Blend(_currentBorder, _targetBorder, speed);

        if (AppTheme.IsClose(_currentFill, _targetFill) && AppTheme.IsClose(_currentBorder, _targetBorder))
        {
            (_currentFill, _currentBorder) = (_targetFill, _targetBorder);
            _animationTimer.Stop();
        }

        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animationTimer.Dispose();

        base.Dispose(disposing);
    }
}
