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
    public int CornerRadius { get; set; } = 11;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ButtonVariant Variant
    {
        get => _variant;
        set
        {
            if (_variant == value)
                return;

            _variant = value;
            TransitionToCurrentState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Background;

    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 15 };
    private ButtonVariant _variant = ButtonVariant.Secondary;
    private bool _hover;
    private bool _pressed;
    private bool _visualStateInitialized;
    private Color _currentFill;
    private Color _currentBorder;
    private Color _currentFore;
    private Color _targetFill;
    private Color _targetBorder;
    private Color _targetFore;

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

        _animationTimer.Tick += AnimateColors;
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        TransitionToCurrentState();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        TransitionToCurrentState();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        TransitionToCurrentState();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        TransitionToCurrentState();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        TransitionToCurrentState();
        base.OnEnabledChanged(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _pressed = false;
        TransitionToCurrentState();
        base.OnLostFocus(e);
    }

    protected override void OnKeyDown(KeyEventArgs kevent)
    {
        if (kevent.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = true;
            TransitionToCurrentState();
        }

        base.OnKeyDown(kevent);
    }

    protected override void OnKeyUp(KeyEventArgs kevent)
    {
        if (kevent.KeyCode is Keys.Space or Keys.Enter)
        {
            _pressed = false;
            TransitionToCurrentState();
        }

        base.OnKeyUp(kevent);
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

        EnsureVisualState();

        Rectangle rect = new(0, 0, Width - 1, Height - 1);
        using GraphicsPath path = AppTheme.RoundedRect(rect, CornerRadius);

        using (SolidBrush brush = new(_currentFill))
            g.FillPath(brush, path);

        if (_currentBorder.A > 0)
        {
            using Pen pen = new(_currentBorder, 1f);
            g.DrawPath(pen, path);
        }

        if (Focused && ShowFocusCues && Enabled)
        {
            Rectangle focusRect = Rectangle.Inflate(rect, -2, -2);
            using GraphicsPath focusPath = AppTheme.RoundedRect(focusRect, Math.Max(1, CornerRadius - 2));
            using Pen focusPen = new(AppTheme.Colors.FocusRing, 1.5f);
            g.DrawPath(focusPen, focusPath);
        }

        Rectangle textRect = rect;
        if (_pressed)
            textRect.Offset(0, 1);

        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textRect,
            _currentFore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    private void EnsureVisualState()
    {
        if (_visualStateInitialized)
            return;

        (_currentFill, _currentBorder, _currentFore) = ResolveColors();
        (_targetFill, _targetBorder, _targetFore) = (_currentFill, _currentBorder, _currentFore);
        _visualStateInitialized = true;
    }

    private void TransitionToCurrentState()
    {
        if (!_visualStateInitialized)
        {
            Invalidate();
            return;
        }

        (_targetFill, _targetBorder, _targetFore) = ResolveColors();

        if (!AppTheme.MotionEnabled)
        {
            (_currentFill, _currentBorder, _currentFore) = (_targetFill, _targetBorder, _targetFore);
            Invalidate();
            return;
        }

        _animationTimer.Start();
    }

    private void AnimateColors(object? sender, EventArgs e)
    {
        const float speed = 0.24f;
        _currentFill = AppTheme.Blend(_currentFill, _targetFill, speed);
        _currentBorder = AppTheme.Blend(_currentBorder, _targetBorder, speed);
        _currentFore = AppTheme.Blend(_currentFore, _targetFore, speed);

        if (AppTheme.IsClose(_currentFill, _targetFill) &&
            AppTheme.IsClose(_currentBorder, _targetBorder) &&
            AppTheme.IsClose(_currentFore, _targetFore))
        {
            (_currentFill, _currentBorder, _currentFore) = (_targetFill, _targetBorder, _targetFore);
            _animationTimer.Stop();
        }

        Invalidate();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _animationTimer.Dispose();

        base.Dispose(disposing);
    }
}
