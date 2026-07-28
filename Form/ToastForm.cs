using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.Forms;

// Thông báo nhỏ, không chặn thao tác, tự đóng sau vài giây
// (thay cho MessageBox.Show ở các trường hợp chỉ cần xác nhận nhanh).
public class ToastForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new();

    private ToastForm(string message, Color accentColor, int durationMs)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        Padding = new Padding(1);

        Panel border = new()
        {
            Dock = DockStyle.Fill,
            BackColor = accentColor,
            Padding = new Padding(3, 0, 0, 0)
        };

        Label lbl = new()
        {
            Dock = DockStyle.Fill,
            Text = message,
            Font = new Font("Segoe UI", 11, FontStyle.Regular),
            ForeColor = Color.FromArgb(40, 40, 40),
            BackColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 10, 18, 10),
            AutoSize = false,
            MaximumSize = new Size(420, 0),
            AutoEllipsis = false
        };

        border.Controls.Add(lbl);
        Controls.Add(border);

        using (Graphics g = CreateGraphics())
        {
            SizeF measured = g.MeasureString(message, lbl.Font, 400 - lbl.Padding.Horizontal);
            lbl.Size = new Size(400, (int)measured.Height + lbl.Padding.Vertical);
        }

        ClientSize = new Size(lbl.Width, lbl.Height);

        PositionBottomRight();

        _timer.Interval = durationMs;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            Close();
        };
    }

    private void PositionBottomRight()
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);

        Location = new Point(
            area.Right - Width - 24,
            area.Bottom - Height - 24);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _timer.Start();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Dispose();
        base.OnFormClosed(e);
    }

    private static void ShowToast(string message, Color accentColor, int durationMs)
    {
        ToastForm toast = new(message, accentColor, durationMs);
        toast.Show();
    }

    // Xanh lá — thao tác thành công, tự tắt sau ~1.5s
    public static void ShowSuccess(string message, int durationMs = 1500)
    {
        ShowToast(message, Color.FromArgb(46, 160, 67), durationMs);
    }

    // Xanh dương — thông báo thông tin chung, tự tắt sau ~1.5s
    public static void ShowInfo(string message, int durationMs = 1500)
    {
        ShowToast(message, Color.FromArgb(66, 133, 244), durationMs);
    }
}
