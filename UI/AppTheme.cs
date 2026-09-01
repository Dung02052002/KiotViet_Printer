using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.UI;

// Bảng màu / font / helper style dùng chung cho toàn bộ giao diện,
// để các form không tự định nghĩa màu sắc rời rạc từng nơi.
public static class AppTheme
{
    public static bool MotionEnabled =>
        SystemInformation.IsMenuAnimationEnabled && !SystemInformation.TerminalServerSession;

    public static class Colors
    {
        // Light graphite + lilac palette inspired by iOS system surfaces.
        public static readonly Color Primary = Color.FromArgb(126, 107, 224);
        public static readonly Color PrimaryHover = Color.FromArgb(115, 95, 214);
        public static readonly Color PrimaryPressed = Color.FromArgb(99, 80, 192);
        public static readonly Color PrimaryLight = Color.FromArgb(239, 235, 252);
        public static readonly Color PrimarySoft = Color.FromArgb(247, 244, 253);
        public static readonly Color FocusRing = Color.FromArgb(183, 169, 239);

        public static readonly Color Danger = Color.FromArgb(205, 83, 105);
        public static readonly Color DangerHover = Color.FromArgb(188, 70, 93);
        public static readonly Color DangerPressed = Color.FromArgb(164, 57, 80);
        public static readonly Color DangerLight = Color.FromArgb(252, 237, 241);

        public static readonly Color Success = Color.FromArgb(71, 153, 112);

        public static readonly Color Background = Color.FromArgb(245, 243, 248);
        public static readonly Color Surface = Color.FromArgb(255, 254, 255);
        public static readonly Color SurfaceElevated = Color.White;
        public static readonly Color SurfaceHover = Color.FromArgb(249, 247, 252);
        public static readonly Color SurfacePressed = Color.FromArgb(240, 236, 246);
        public static readonly Color InputBackground = Color.FromArgb(249, 248, 251);

        public static readonly Color Border = Color.FromArgb(229, 224, 235);
        public static readonly Color BorderStrong = Color.FromArgb(207, 199, 218);

        public static readonly Color TextPrimary = Color.FromArgb(45, 41, 51);
        public static readonly Color TextSecondary = Color.FromArgb(105, 98, 116);
        public static readonly Color TextMuted = Color.FromArgb(148, 140, 160);

        public static readonly Color Disabled = Color.FromArgb(237, 234, 241);
        public static readonly Color DisabledText = Color.FromArgb(172, 164, 182);

        public static readonly Color GridHeaderBack = Color.FromArgb(242, 239, 246);
        public static readonly Color GridAltRow = Color.FromArgb(250, 249, 252);
        public static readonly Color GridSelection = Color.FromArgb(235, 229, 251);
    }

    public static class Fonts
    {
        private static readonly string Family = ResolveFontFamily("Segoe UI Variable Text", "Segoe UI");
        private static readonly string DisplayFamily = ResolveFontFamily("Segoe UI Variable Display", Family);

        public static readonly Font Title = new(DisplayFamily, 20f, FontStyle.Bold);
        public static readonly Font Subtitle = new(Family, 10f, FontStyle.Regular);
        public static readonly Font SectionTitle = new(Family, 12.5f, FontStyle.Bold);
        public static readonly Font Body = new(Family, 9.5f, FontStyle.Regular);
        public static readonly Font BodyBold = new(Family, 9.5f, FontStyle.Bold);
        public static readonly Font Hint = new(Family, 8.75f, FontStyle.Regular);
        public static readonly Font Button = new(Family, 9.75f, FontStyle.Bold);
        public static readonly Font ButtonRegular = new(Family, 9.5f, FontStyle.Regular);
        public static readonly Font Icon = new("Segoe UI Emoji", 20f, FontStyle.Regular);
        public static readonly Font IconSmall = new("Segoe UI Emoji", 14f, FontStyle.Regular);

        private static string ResolveFontFamily(string preferred, string fallback)
        {
            try
            {
                using FontFamily family = new(preferred);
                return family.Name;
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }
    }

    // Nền form: xám rất nhạt, giúp các khối card nổi bật hơn so với nền trắng phẳng.
    // Lưu ý: DoubleBuffered là protected trên Control nên phải set ngay trong
    // constructor của từng Form (DoubleBuffered = true;), không set được từ đây.
    public static void StyleForm(Form form)
    {
        form.BackColor = Colors.Background;
        form.Font = Fonts.Body;
        form.AutoScaleMode = AutoScaleMode.Dpi;
        UiMotion.EnableFadeIn(form);
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Colors.InputBackground;
        textBox.ForeColor = Colors.TextPrimary;
        textBox.Font = Fonts.Body;
    }

    public static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.BackColor = Colors.InputBackground;
        comboBox.ForeColor = Colors.TextPrimary;
        comboBox.Font = Fonts.Body;
        comboBox.IntegralHeight = false;
        comboBox.ItemHeight = 26;
        comboBox.DropDownHeight = 260;
        ApplyRoundedRegion(comboBox, 9);
    }

    // Style DataGridView theo phong cách phẳng, hiện đại: bỏ lưới rối mắt,
    // header có màu nền riêng, hàng chẵn/lẻ so le cho dễ đọc.
    public static void StyleGrid(DataGridView grid)
    {
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Colors.Surface;
        grid.GridColor = Colors.Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.RowHeadersVisible = false;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 42;
        grid.RowTemplate.Height = 36;
        grid.Font = Fonts.Body;

        grid.ColumnHeadersDefaultCellStyle.BackColor = Colors.GridHeaderBack;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Colors.TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = Fonts.BodyBold;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

        grid.DefaultCellStyle.BackColor = Colors.Surface;
        grid.DefaultCellStyle.ForeColor = Colors.TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Colors.GridSelection;
        grid.DefaultCellStyle.SelectionForeColor = Colors.TextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(10, 5, 6, 5);
        grid.DefaultCellStyle.Font = Fonts.Body;

        grid.AlternatingRowsDefaultCellStyle.BackColor = Colors.GridAltRow;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Colors.GridSelection;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Colors.TextPrimary;

        grid.RowsDefaultCellStyle.SelectionBackColor = Colors.GridSelection;
        grid.RowsDefaultCellStyle.SelectionForeColor = Colors.TextPrimary;
        ApplyRoundedRegion(grid, 12);
    }

    public static void ApplyRoundedRegion(Control control, int radius)
    {
        void updateRegion()
        {
            if (control.Width <= 0 || control.Height <= 0 || control.IsDisposed)
                return;

            using GraphicsPath path = RoundedRect(
                new Rectangle(0, 0, control.Width, control.Height),
                radius);
            Region next = new(path);
            Region? previous = control.Region;
            control.Region = next;
            previous?.Dispose();
        }

        updateRegion();
        control.SizeChanged += (_, _) => updateRegion();
    }

    public static Color Blend(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);

        return Color.FromArgb(
            (int)Math.Round(from.A + ((to.A - from.A) * amount)),
            (int)Math.Round(from.R + ((to.R - from.R) * amount)),
            (int)Math.Round(from.G + ((to.G - from.G) * amount)),
            (int)Math.Round(from.B + ((to.B - from.B) * amount)));
    }

    public static bool IsClose(Color left, Color right, int tolerance = 2)
    {
        return Math.Abs(left.A - right.A) <= tolerance &&
               Math.Abs(left.R - right.R) <= tolerance &&
               Math.Abs(left.G - right.G) <= tolerance &&
               Math.Abs(left.B - right.B) <= tolerance;
    }

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        return RoundedRect((RectangleF)bounds, radius);
    }

    public static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        GraphicsPath path = new();

        if (bounds.Width <= 0 || bounds.Height <= 0)
            return path;

        // Bán kính không được vượt quá nửa cạnh ngắn: nếu vượt, hai cung tròn
        // đối diện chồng lên nhau và GDI+ vẽ ra hình méo có gai ở chỗ giao.
        float diameter = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2f) * 2f;

        if (diameter <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        path.StartFigure();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    public static void PrepareSmoothing(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    // Tô khối bo góc. Nhận RectangleF full-size (0,0,Width,Height) chứ không
    // phải Width-1/Height-1: trừ 1px sẽ chừa lại một sọc chưa tô ở cạnh phải và
    // cạnh dưới, nhìn như cạnh bị rách.
    public static void FillRounded(Graphics g, RectangleF bounds, float radius, Color color)
    {
        if (color.A == 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        using GraphicsPath path = RoundedRect(bounds, radius);
        using SolidBrush brush = new(color);
        g.FillPath(brush, path);
    }

    // Vẽ viền bo góc. Hai chi tiết quyết định việc viền có bị "tua tủa" hay không:
    //  - LineJoin.Round: mặc định GDI+ nối nét kiểu Miter. Ở 4 chỗ cung tròn tiếp
    //    giáp đoạn thẳng, sai số số thực làm góc nối lệch khỏi 180° một chút và
    //    Miter kéo dài mối nối thành gai nhọn chĩa ra ngoài.
    //  - Thụt vào nửa độ dày nét: nét vẽ nằm giữa đường path, không thụt thì một
    //    nửa nét tràn ra ngoài control rồi bị cắt cụt, để lại cạnh lởm chởm.
    public static void DrawRoundedBorder(Graphics g, RectangleF bounds, float radius, Color color, float thickness)
    {
        if (color.A == 0 || thickness <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        float inset = thickness / 2f;
        RectangleF stroke = RectangleF.Inflate(bounds, -inset, -inset);

        if (stroke.Width <= 0 || stroke.Height <= 0)
            return;

        using GraphicsPath path = RoundedRect(stroke, Math.Max(0f, radius - inset));
        using Pen pen = new(color, thickness)
        {
            LineJoin = LineJoin.Round,
            Alignment = PenAlignment.Center
        };

        g.DrawPath(pen, path);
    }

    // Vẽ đúng phần nền mà control cha đang hiển thị ở vị trí control này chiếm chỗ.
    //
    // Các control tự vẽ chỉ tô phần bên trong đường bo tròn, nên 4 góc nằm ngoài
    // đường bo vẫn phải hiện thứ ở phía sau. Trước đây chỗ đó được tô bằng một màu
    // phẳng đoán trước (ContainerColor); chỉ cần đoán lệch là thấy ngay một ô vuông
    // khác màu bao quanh khối bo tròn. Ở đây ta bảo chính control cha vẽ lại lát
    // cắt đó nên màu luôn khớp tuyệt đối — kể cả khi cha cũng là control tự vẽ
    // hoặc đang trong lúc đổi màu hover.
    public static void PaintContainerBackground(Control control, PaintEventArgs e, Color fallback)
    {
        if (control.Parent is not { } parent)
        {
            e.Graphics.Clear(fallback);
            return;
        }

        GraphicsState state = e.Graphics.Save();

        try
        {
            e.Graphics.TranslateTransform(-control.Left, -control.Top);

            Rectangle slice = new(control.Left, control.Top, control.Width, control.Height);
            using PaintEventArgs args = new(e.Graphics, slice);

            PaintProxy.Shared.PaintInto(parent, args);
        }
        finally
        {
            e.Graphics.Restore(state);
        }
    }

    // InvokePaintBackground/InvokePaint là protected trên Control nên chỉ gọi được
    // từ bên trong một lớp con của Control. Lớp rỗng này tồn tại chỉ để "mượn"
    // quyền gọi đó cho PaintContainerBackground, thay vì phải chép lại đoạn
    // compose nền ở từng control tự vẽ.
    private sealed class PaintProxy : Control
    {
        public static readonly PaintProxy Shared = new();

        public void PaintInto(Control source, PaintEventArgs e)
        {
            InvokePaintBackground(source, e);
            InvokePaint(source, e);
        }
    }
}
