using System.ComponentModel;

namespace KiotVietLabelPrinter.UI;

// Compact animated switch that keeps the keyboard and Checked semantics of CheckBox.
public class ToggleSwitch : CheckBox
{
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 15 };
    private float _position;
    private float _targetPosition;
    private bool _hover;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Background;

    public ToggleSwitch()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);

        AutoSize = false;
        Height = 28;
        Font = AppTheme.Fonts.Body;
        ForeColor = AppTheme.Colors.TextPrimary;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;

        _animationTimer.Tick += AnimateSwitch;
    }

    protected override void OnCheckedChanged(EventArgs e)
    {
        _targetPosition = Checked ? 1f : 0f;

        if (!AppTheme.MotionEnabled || !IsHandleCreated)
        {
            _position = _targetPosition;
            Invalidate();
        }
        else
        {
            _animationTimer.Start();
        }

        base.OnCheckedChanged(e);
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
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
        base.OnEnabledChanged(e);
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

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        AppTheme.PaintContainerBackground(this, pevent, ContainerColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        AppTheme.PrepareSmoothing(g);

        const int trackWidth = 44;
        const int trackHeight = 24;
        const int knobSize = 18;

        // Chừa 2px bên trái để vòng focus (vẽ rộng hơn track 2px) không bị cắt cụt
        // ở mép trái control.
        const int trackX = 2;

        int trackY = Math.Max(2, (Height - trackHeight) / 2);
        RectangleF trackRect = new(trackX, trackY, trackWidth, trackHeight);

        Color offColor = _hover ? AppTheme.Colors.BorderStrong : AppTheme.Colors.Border;
        Color trackColor = Enabled
            ? AppTheme.Blend(offColor, AppTheme.Colors.Primary, _position)
            : AppTheme.Colors.Disabled;

        AppTheme.FillRounded(g, trackRect, trackHeight / 2f, trackColor);

        float knobX = trackX + 3f + ((trackWidth - knobSize - 6f) * _position);
        RectangleF knobRect = new(knobX, trackY + 3, knobSize, knobSize);

        using (SolidBrush shadowBrush = new(Color.FromArgb(28, 45, 36, 56)))
            g.FillEllipse(shadowBrush, knobRect.X, knobRect.Y + 1, knobRect.Width, knobRect.Height);

        using (SolidBrush knobBrush = new(Enabled ? Color.White : AppTheme.Colors.SurfaceHover))
            g.FillEllipse(knobBrush, knobRect);

        if (Focused && ShowFocusCues)
        {
            AppTheme.DrawRoundedBorder(
                g,
                RectangleF.Inflate(trackRect, 2f, 2f),
                (trackHeight / 2f) + 2f,
                AppTheme.Colors.FocusRing,
                1.4f);
        }

        Rectangle textRect = new(54, 0, Math.Max(0, Width - 54), Height);
        TextRenderer.DrawText(
            g,
            Text,
            Font,
            textRect,
            Enabled ? ForeColor : AppTheme.Colors.DisabledText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }

    private void AnimateSwitch(object? sender, EventArgs e)
    {
        float distance = _targetPosition - _position;
        _position += distance * 0.28f;

        if (Math.Abs(distance) < 0.015f)
        {
            _position = _targetPosition;
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
