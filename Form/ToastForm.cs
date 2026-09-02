using System.Drawing.Drawing2D;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

// Thông báo nhỏ, không chặn thao tác, tự đóng sau vài giây
// (thay cho MessageBox.Show ở các trường hợp chỉ cần xác nhận nhanh).
public class ToastForm : Form
{
    private readonly System.Windows.Forms.Timer _lifeTimer = new();
    private readonly System.Windows.Forms.Timer _animationTimer = new() { Interval = 15 };
    private long _animationStartedAt;
    private int _targetTop;
    private bool _isClosing;

    private ToastForm(string message, Color accentColor, string glyph, int durationMs)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = AppTheme.Colors.Surface;
        Padding = Padding.Empty;
        DoubleBuffered = true;
        Opacity = AppTheme.MotionEnabled ? 0 : 1;

        RoundedPanel card = new()
        {
            Dock = DockStyle.Fill,
            CornerRadius = 14,
            FillColor = AppTheme.Colors.SurfaceElevated,
            BorderColor = AppTheme.Colors.Border,
            BorderThickness = 1,
            ContainerColor = AppTheme.Colors.Surface
        };

        Label lbl = new()
        {
            Dock = DockStyle.Fill,
            Text = message,
            Font = AppTheme.Fonts.BodyBold,
            ForeColor = AppTheme.Colors.TextPrimary,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(58, 12, 18, 12),
            AutoSize = false,
            MaximumSize = new Size(420, 0),
            AutoEllipsis = false
        };

        RoundedPanel statusBadge = new()
        {
            Width = 28,
            Height = 28,
            Left = 16,
            CornerRadius = 14,
            FillColor = AppTheme.Blend(AppTheme.Colors.Surface, accentColor, 0.14f),
            BorderThickness = 0,
            ContainerColor = AppTheme.Colors.Surface
        };

        Label statusGlyph = new()
        {
            Text = glyph,
            Dock = DockStyle.Fill,
            ForeColor = accentColor,
            BackColor = Color.Transparent,
            Font = new Font(AppTheme.Fonts.Body.FontFamily, 10.5f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        statusBadge.Controls.Add(statusGlyph);

        card.Controls.Add(lbl);
        card.Controls.Add(statusBadge);
        statusBadge.BringToFront();
        Controls.Add(card);

        using (Graphics g = CreateGraphics())
        {
            SizeF measured = g.MeasureString(message, lbl.Font, 400 - lbl.Padding.Horizontal);
            lbl.Size = new Size(400, (int)measured.Height + lbl.Padding.Vertical);
        }

        ClientSize = new Size(lbl.Width, lbl.Height);
        statusBadge.Top = Math.Max(0, (ClientSize.Height - statusBadge.Height) / 2);

        using (GraphicsPath path = AppTheme.RoundedRect(
                   new Rectangle(0, 0, ClientSize.Width, ClientSize.Height),
                   14))
        {
            Region = new Region(path);
        }

        PositionBottomRight();

        _lifeTimer.Interval = durationMs;
        _lifeTimer.Tick += (_, _) =>
        {
            _lifeTimer.Stop();
            BeginExitAnimation();
        };

        _animationTimer.Tick += AnimateToast;
    }

    private void PositionBottomRight()
    {
        Screen screen = Form.ActiveForm is { } activeForm
            ? Screen.FromControl(activeForm)
            : Screen.FromPoint(Cursor.Position);
        Rectangle area = screen.WorkingArea;

        Location = new Point(
            area.Right - Width - 24,
            area.Bottom - Height - 24);

        _targetTop = Top;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (!AppTheme.MotionEnabled)
        {
            Opacity = 1;
            _lifeTimer.Start();
            return;
        }

        Opacity = 0;
        Top = _targetTop + 14;
        _animationStartedAt = Environment.TickCount64;
        _animationTimer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _lifeTimer.Dispose();
        _animationTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void BeginExitAnimation()
    {
        if (!AppTheme.MotionEnabled)
        {
            Close();
            return;
        }

        _isClosing = true;
        _animationStartedAt = Environment.TickCount64;
        _animationTimer.Start();
    }

    private void AnimateToast(object? sender, EventArgs e)
    {
        int duration = _isClosing ? 140 : 180;
        float progress = Math.Clamp(
            (Environment.TickCount64 - _animationStartedAt) / (float)duration,
            0f,
            1f);
        float eased = 1f - MathF.Pow(1f - progress, 3f);

        if (_isClosing)
        {
            Opacity = Math.Max(0, 1f - eased);
            Top = _targetTop + (int)Math.Round(8 * eased);
        }
        else
        {
            Opacity = eased;
            Top = _targetTop + (int)Math.Round(14 * (1f - eased));
        }

        if (progress < 1f)
            return;

        _animationTimer.Stop();

        if (_isClosing)
        {
            Close();
        }
        else
        {
            Opacity = 1;
            Top = _targetTop;
            _lifeTimer.Start();
        }
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CsDropShadow = 0x00020000;
            CreateParams parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    private static void ShowToast(string message, Color accentColor, string glyph, int durationMs)
    {
        ToastForm toast = new(message, accentColor, glyph, durationMs);
        toast.Show();
    }

    // Xanh lá — thao tác thành công, tự tắt sau ~1.5s
    public static void ShowSuccess(string message, int durationMs = 1500)
    {
        ShowToast(message, AppTheme.Colors.Success, "✓", durationMs);
    }

    // Xanh dương — thông báo thông tin chung, tự tắt sau ~1.5s
    public static void ShowInfo(string message, int durationMs = 1500)
    {
        ShowToast(message, AppTheme.Colors.Primary, "i", durationMs);
    }
}
