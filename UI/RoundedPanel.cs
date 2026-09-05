using System.ComponentModel;

namespace KiotVietLabelPrinter.UI;

// Panel bo góc dùng làm "card" (khối nội dung nổi trên nền xám của form).
// Hỗ trợ hiệu ứng đổi viền/nền khi hover, dùng cho các card có thể bấm được.
//
// Phần góc nằm ngoài đường bo tròn được lấy từ chính control cha
// (AppTheme.PaintContainerBackground); ContainerColor chỉ là màu dự phòng khi
// panel chưa có cha. Label con được đặt nền trong suốt để không tự tô ô vuông
// đè lên mặt card — xem BlendChildBackground.
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

    // Very soft layered drop shadow for elevated cards (category cards, the
    // detail workspace card). Off by default so plain panels/badges are unaffected.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShadowEnabled { get; set; }

    private bool _hover;
    private bool _hoverEffect;
    private bool _visualStateInitialized;
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 15 };
    private Color _currentFill;
    private Color _currentBorder;
    private Color _targetFill;
    private Color _targetBorder;

    internal Color DisplayFillColor => _visualStateInitialized
        ? _currentFill
        : ResolveColors().fill;

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
            PrepareChild(e.Control);

        base.OnControlAdded(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        AppTheme.PaintContainerBackground(this, pevent, ContainerColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        AppTheme.PrepareSmoothing(g);

        EnsureVisualState();

        RectangleF outer = new(0, 0, Width, Height);
        RectangleF bounds = outer;

        // No native drop-shadow support in GDI+/WinForms, so a soft shadow is
        // faked with a few translucent layers growing outward below the card,
        // inset from the control's own bounds since painting cannot spill
        // outside them.
        if (ShadowEnabled)
        {
            const float margin = 3f;
            bounds = RectangleF.Inflate(outer, -margin, -margin);

            DrawShadowLayer(g, bounds, 3f, 0.5f, 18);
            DrawShadowLayer(g, bounds, 2f, 1.5f, 14);
            DrawShadowLayer(g, bounds, 1f, 2.5f, 10);
        }

        AppTheme.FillRounded(g, bounds, CornerRadius, _currentFill);

        if (BorderThickness > 0)
            AppTheme.DrawRoundedBorder(g, bounds, CornerRadius, _currentBorder, BorderThickness);

        if (HoverEffect && Focused && ShowFocusCues)
        {
            AppTheme.DrawRoundedBorder(
                g,
                RectangleF.Inflate(bounds, -4f, -4f),
                Math.Max(1, CornerRadius - 4),
                AppTheme.Colors.FocusRing,
                1.5f);
        }

        base.OnPaint(e);
    }

    private void DrawShadowLayer(Graphics g, RectangleF content, float grow, float dy, int alpha)
    {
        RectangleF layer = RectangleF.Inflate(content, grow, grow);
        layer.Offset(0, dy);
        AppTheme.FillRounded(g, layer, CornerRadius + grow, Color.FromArgb(alpha, AppTheme.Colors.ShadowInk));
    }

    private void PrepareChild(Control control)
    {
        BlendChildBackground(control);

        control.MouseEnter += ChildMouseEnter;
        control.MouseLeave += ChildMouseLeave;
        control.MouseDown += ChildMouseDown;
        control.ControlAdded += ChildControlAdded;

        foreach (Control child in control.Controls)
            PrepareChild(child);
    }

    // Panel mặc định mang BackColor = SystemColors.Control (xám). Label đặt trên
    // card kế thừa đúng màu xám đó rồi tô kín ô chữ nhật của nó, thành ra chữ nào
    // cũng nằm trong một ô vuông xám đè lên mặt card mà RoundedPanel vẽ ra.
    // Đặt Transparent để Label lấy lại đúng nền đã vẽ (kể cả màu hover), thay vì
    // tự tô một hình chữ nhật lệch màu.
    private static void BlendChildBackground(Control control)
    {
        if (control is Label && control.BackColor != Color.Transparent)
            control.BackColor = Color.Transparent;
    }

    private void ChildControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
            PrepareChild(e.Control);
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
            Invalidate(true);
            return;
        }

        (_targetFill, _targetBorder) = ResolveColors();

        if (!AppTheme.MotionEnabled)
        {
            (_currentFill, _currentBorder) = (_targetFill, _targetBorder);
            Invalidate(true);
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

        // Invalidate(true): các Label con để nền trong suốt nên phải vẽ lại cùng
        // nhịp với mặt card, nếu không chúng giữ lại màu nền của khung hình trước.
        Invalidate(true);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animationTimer.Dispose();

        base.Dispose(disposing);
    }
}
