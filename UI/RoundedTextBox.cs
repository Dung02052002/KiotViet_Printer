using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace KiotVietLabelPrinter.UI;

// Text input with an iOS-like surface, rounded outline and animated focus state.
public class RoundedTextBox : UserControl
{
    private readonly TextBox _editor = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 15 };

    private bool _hover;
    private bool _visualStateInitialized;
    private Color _currentFill;
    private Color _currentBorder;
    private Color _targetFill;
    private Color _targetBorder;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int CornerRadius { get; set; } = 11;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color FillColor { get; set; } = AppTheme.Colors.InputBackground;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ContainerColor { get; set; } = AppTheme.Colors.Surface;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ReadOnly
    {
        get => _editor.ReadOnly;
        set
        {
            _editor.ReadOnly = value;
            TransitionToCurrentState();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Multiline
    {
        get => _editor.Multiline;
        set
        {
            _editor.Multiline = value;
            LayoutEditor();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string PlaceholderText
    {
        get => _editor.PlaceholderText;
        set => _editor.PlaceholderText = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HorizontalAlignment TextAlign
    {
        get => _editor.TextAlign;
        set => _editor.TextAlign = value;
    }

    [AllowNull]
    public override string Text
    {
        get => _editor.Text;
        set
        {
            string next = value ?? string.Empty;
            if (_editor.Text != next)
                _editor.Text = next;
        }
    }

    public RoundedTextBox()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        Height = 38;
        MinimumSize = new Size(40, 36);
        Cursor = Cursors.IBeam;
        TabStop = false;

        _editor.BorderStyle = BorderStyle.None;
        _editor.BackColor = FillColor;
        _editor.ForeColor = AppTheme.Colors.TextPrimary;
        _editor.Font = AppTheme.Fonts.Body;
        _editor.TabIndex = 0;

        _editor.TextChanged += (_, _) =>
        {
            if (base.Text != _editor.Text)
                base.Text = _editor.Text;
        };
        _editor.MouseEnter += (_, _) => SetHoverState(true);
        _editor.MouseLeave += (_, _) => UpdateHoverFromPointer();
        _editor.GotFocus += (_, _) => TransitionToCurrentState();
        _editor.LostFocus += (_, _) => TransitionToCurrentState();

        Controls.Add(_editor);
        _animationTimer.Tick += AnimateColors;
        LayoutEditor();
    }

    public void Clear()
    {
        _editor.Clear();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);

        if (_editor != null)
        {
            _editor.Font = Font;
            LayoutEditor();
        }
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        base.OnForeColorChanged(e);

        if (_editor != null)
            _editor.ForeColor = ForeColor;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _editor.Enabled = Enabled;
        TransitionToCurrentState();
        base.OnEnabledChanged(e);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        LayoutEditor();
        base.OnSizeChanged(e);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        SetHoverState(true);
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        UpdateHoverFromPointer();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _editor.Focus();
        base.OnMouseDown(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        AppTheme.PaintContainerBackground(this, e, ContainerColor);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureVisualState();

        Graphics g = e.Graphics;
        AppTheme.PrepareSmoothing(g);

        RectangleF bounds = new(0, 0, Width, Height);

        AppTheme.FillRounded(g, bounds, CornerRadius, _currentFill);
        AppTheme.DrawRoundedBorder(g, bounds, CornerRadius, _currentBorder, _editor.Focused ? 1.6f : 1f);
    }

    private void LayoutEditor()
    {
        if (_editor == null || Width <= 0 || Height <= 0)
            return;

        const int horizontalPadding = 12;
        int preferredHeight = Math.Max(18, _editor.PreferredSize.Height);
        int top = _editor.Multiline ? 9 : Math.Max(1, (Height - preferredHeight) / 2);
        int editorHeight = _editor.Multiline ? Math.Max(18, Height - 18) : preferredHeight;

        _editor.SetBounds(
            horizontalPadding,
            top,
            Math.Max(1, Width - (horizontalPadding * 2)),
            editorHeight);
    }

    private void SetHoverState(bool hover)
    {
        if (_hover == hover)
            return;

        _hover = hover;
        TransitionToCurrentState();
    }

    private void UpdateHoverFromPointer()
    {
        SetHoverState(ClientRectangle.Contains(PointToClient(Cursor.Position)));
    }

    private (Color fill, Color border) ResolveColors()
    {
        if (!Enabled)
            return (AppTheme.Colors.Disabled, AppTheme.Colors.Border);

        if (_editor.Focused)
            return (AppTheme.Colors.Surface, AppTheme.Colors.Primary);

        return (
            ReadOnly ? AppTheme.Colors.PrimarySoft : FillColor,
            _hover ? AppTheme.Colors.BorderStrong : AppTheme.Colors.Border);
    }

    private void EnsureVisualState()
    {
        if (_visualStateInitialized)
            return;

        (_currentFill, _currentBorder) = ResolveColors();
        (_targetFill, _targetBorder) = (_currentFill, _currentBorder);
        _editor.BackColor = _currentFill;
        _visualStateInitialized = true;
    }

    private void TransitionToCurrentState()
    {
        if (!_visualStateInitialized)
        {
            Invalidate();
            return;
        }

        (_targetFill, _targetBorder) = ResolveColors();

        if (!AppTheme.MotionEnabled)
        {
            (_currentFill, _currentBorder) = (_targetFill, _targetBorder);
            _editor.BackColor = _currentFill;
            Invalidate();
            return;
        }

        _animationTimer.Start();
    }

    private void AnimateColors(object? sender, EventArgs e)
    {
        const float speed = 0.22f;
        _currentFill = AppTheme.Blend(_currentFill, _targetFill, speed);
        _currentBorder = AppTheme.Blend(_currentBorder, _targetBorder, speed);
        _editor.BackColor = _currentFill;

        if (AppTheme.IsClose(_currentFill, _targetFill) && AppTheme.IsClose(_currentBorder, _targetBorder))
        {
            (_currentFill, _currentBorder) = (_targetFill, _targetBorder);
            _editor.BackColor = _currentFill;
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
