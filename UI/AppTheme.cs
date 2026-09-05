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
        // iOS / macOS inspired light-blue palette: airy background, near-white
        // cards, one accent blue, thin low-contrast borders.
        public static readonly Color Primary = Color.FromArgb(47, 107, 255);
        public static readonly Color PrimaryHover = Color.FromArgb(36, 95, 229);
        public static readonly Color PrimaryPressed = Color.FromArgb(30, 84, 206);
        public static readonly Color PrimaryLight = Color.FromArgb(243, 247, 255);
        public static readonly Color PrimarySoft = Color.FromArgb(233, 240, 255);
        public static readonly Color FocusRing = Color.FromArgb(163, 194, 255);

        public static readonly Color Danger = Color.FromArgb(224, 71, 71);
        public static readonly Color DangerHover = Color.FromArgb(204, 60, 60);
        public static readonly Color DangerPressed = Color.FromArgb(180, 49, 49);
        public static readonly Color DangerLight = Color.FromArgb(253, 236, 236);

        public static readonly Color Success = Color.FromArgb(52, 150, 90);

        public static readonly Color Background = Color.FromArgb(246, 248, 252);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceElevated = Color.White;
        public static readonly Color SurfaceHover = Color.FromArgb(247, 249, 253);
        public static readonly Color SurfacePressed = Color.FromArgb(239, 243, 250);
        public static readonly Color InputBackground = Color.White;

        public static readonly Color Border = Color.FromArgb(227, 232, 240);
        public static readonly Color BorderStrong = Color.FromArgb(203, 213, 225);

        // Resting border for Outline action buttons: a soft gray-blue, much
        // quieter than full Primary — Primary itself is reserved for hover/press
        // so the border only reads as "active blue" on interaction.
        public static readonly Color BorderOutlineRest = Color.FromArgb(214, 223, 240);

        public static readonly Color TextPrimary = Color.FromArgb(15, 23, 42);
        public static readonly Color TextSecondary = Color.FromArgb(102, 112, 133);
        public static readonly Color TextMuted = Color.FromArgb(152, 162, 179);

        public static readonly Color Disabled = Color.FromArgb(237, 240, 245);
        public static readonly Color DisabledText = Color.FromArgb(163, 170, 181);

        public static readonly Color GridHeaderBack = Color.FromArgb(242, 245, 250);
        public static readonly Color GridAltRow = Color.FromArgb(249, 251, 253);
        public static readonly Color GridSelection = Color.FromArgb(224, 235, 255);

        // Very soft, near-transparent black used for layered card drop shadows.
        public static readonly Color ShadowInk = Color.FromArgb(15, 23, 42);
    }

    public static class Fonts
    {
        private static readonly string Family = ResolveFontFamily("Segoe UI Variable Text", "Segoe UI");
        private static readonly string DisplayFamily = ResolveFontFamily("Segoe UI Variable Display", Family);

        public static readonly Font Title = new(DisplayFamily, 24f, FontStyle.Bold);
        public static readonly Font Subtitle = new(Family, 10.5f, FontStyle.Regular);
        public static readonly Font SectionTitle = new(Family, 15f, FontStyle.Bold);
        public static readonly Font Overline = new(Family, 9.5f, FontStyle.Bold);
        public static readonly Font Body = new(Family, 10f, FontStyle.Regular);
        public static readonly Font BodyBold = new(Family, 10f, FontStyle.Bold);
        public static readonly Font Hint = new(Family, 9f, FontStyle.Regular);
        public static readonly Font Button = new(Family, 10f, FontStyle.Bold);
        public static readonly Font ButtonRegular = new(Family, 10f, FontStyle.Regular);
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

    // Vẽ viền bo góc bằng CÁCH TÔ (fill), không dùng Pen.DrawPath: viền là
    // Region của path ngoài (RoundedRect tại bounds) TRỪ ĐI Region của path
    // trong (RoundedRect thu nhỏ bounds đi đúng thickness).
    //
    // Trước đây viền được vẽ bằng Pen.DrawPath trên path nối từ 4 AddArc. Dù đã
    // set LineJoin.Round, GDI+ vẫn có thể sinh gai nhọn ("tua tủa") tại chỗ nối
    // giữa cung tròn và đoạn thẳng do sai số dấu phẩy động — lỗi này từng lộ ra
    // thành các sọc/pixel xanh thừa ở góc và cạnh nút.
    //
    // Bản trước của hàm này gộp 2 path vào 1 GraphicsPath rồi tô bằng
    // FillMode.Alternate — nhưng vẫn để lọt một hình nêm đặc ở góc dưới-phải tại
    // vài kích thước nút cụ thể (rasterizer của FillPath dựng hình sai khi 2
    // đường cong có độ cong khác nhau chồng lên nhau ở cùng một góc). Region.
    // Exclude dùng thuật toán tổ hợp vùng khác hẳn (không phải rasterize path
    // trực tiếp) nên không còn gặp lỗi dựng hình đó.
    public static void DrawRoundedBorder(Graphics g, RectangleF bounds, float radius, Color color, float thickness)
    {
        if (color.A == 0 || thickness <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            return;

        RectangleF inner = RectangleF.Inflate(bounds, -thickness, -thickness);

        using GraphicsPath outerPath = RoundedRect(bounds, radius);
        using Region ring = new(outerPath);

        if (inner.Width > 0f && inner.Height > 0f)
        {
            using GraphicsPath innerPath = RoundedRect(inner, Math.Max(0f, radius - thickness));
            ring.Exclude(innerPath);
        }

        using SolidBrush brush = new(color);
        g.FillRegion(brush, ring);
    }

    // Tô phần nằm ngoài đường bo bằng đúng màu bề mặt hiện tại của control cha.
    // Không gọi lại toàn bộ OnPaint của cha: cách đó có thể sao chép chữ/control con
    // vào background khi nhiều lớp RoundedPanel lồng nhau, tạo vệt chữ và góc đen.
    public static void PaintContainerBackground(Control control, PaintEventArgs e, Color fallback)
    {
        Color background = control.Parent switch
        {
            RoundedPanel roundedParent => roundedParent.DisplayFillColor,
            { BackColor.A: > 0 } parent => parent.BackColor,
            _ => fallback
        };

        e.Graphics.Clear(background);
    }
}
