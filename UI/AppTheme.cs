using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.UI;

// Bảng màu / font / helper style dùng chung cho toàn bộ giao diện,
// để các form không tự định nghĩa màu sắc rời rạc từng nơi.
public static class AppTheme
{
    public static class Colors
    {
        public static readonly Color Primary = Color.FromArgb(47, 111, 237);
        public static readonly Color PrimaryHover = Color.FromArgb(38, 96, 216);
        public static readonly Color PrimaryPressed = Color.FromArgb(29, 80, 189);
        public static readonly Color PrimaryLight = Color.FromArgb(232, 240, 254);

        public static readonly Color Danger = Color.FromArgb(220, 53, 69);
        public static readonly Color DangerHover = Color.FromArgb(196, 45, 60);
        public static readonly Color DangerPressed = Color.FromArgb(172, 38, 52);

        public static readonly Color Success = Color.FromArgb(34, 160, 90);

        public static readonly Color Background = Color.FromArgb(244, 246, 251);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceHover = Color.FromArgb(247, 249, 253);
        public static readonly Color SurfacePressed = Color.FromArgb(238, 241, 248);

        public static readonly Color Border = Color.FromArgb(225, 229, 238);
        public static readonly Color BorderStrong = Color.FromArgb(203, 210, 224);

        public static readonly Color TextPrimary = Color.FromArgb(28, 32, 42);
        public static readonly Color TextSecondary = Color.FromArgb(110, 118, 135);
        public static readonly Color TextMuted = Color.FromArgb(150, 157, 171);

        public static readonly Color Disabled = Color.FromArgb(233, 236, 241);
        public static readonly Color DisabledText = Color.FromArgb(170, 176, 187);

        public static readonly Color GridHeaderBack = Color.FromArgb(240, 243, 249);
        public static readonly Color GridAltRow = Color.FromArgb(249, 250, 253);
        public static readonly Color GridSelection = Color.FromArgb(228, 236, 253);
    }

    public static class Fonts
    {
        private const string Family = "Segoe UI";

        public static readonly Font Title = new(Family, 19f, FontStyle.Bold);
        public static readonly Font Subtitle = new(Family, 10f, FontStyle.Regular);
        public static readonly Font SectionTitle = new(Family, 12.5f, FontStyle.Bold);
        public static readonly Font Body = new(Family, 9.5f, FontStyle.Regular);
        public static readonly Font BodyBold = new(Family, 9.5f, FontStyle.Bold);
        public static readonly Font Hint = new(Family, 8.75f, FontStyle.Regular);
        public static readonly Font Button = new(Family, 9.75f, FontStyle.Bold);
        public static readonly Font ButtonRegular = new(Family, 9.5f, FontStyle.Regular);
        public static readonly Font Icon = new("Segoe UI Emoji", 20f, FontStyle.Regular);
        public static readonly Font IconSmall = new("Segoe UI Emoji", 14f, FontStyle.Regular);
    }

    // Nền form: xám rất nhạt, giúp các khối card nổi bật hơn so với nền trắng phẳng.
    // Lưu ý: DoubleBuffered là protected trên Control nên phải set ngay trong
    // constructor của từng Form (DoubleBuffered = true;), không set được từ đây.
    public static void StyleForm(Form form)
    {
        form.BackColor = Colors.Background;
        form.Font = Fonts.Body;
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
        grid.ColumnHeadersHeight = 38;
        grid.RowTemplate.Height = 32;
        grid.Font = Fonts.Body;

        grid.ColumnHeadersDefaultCellStyle.BackColor = Colors.GridHeaderBack;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Colors.TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = Fonts.BodyBold;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(8, 0, 0, 0);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

        grid.DefaultCellStyle.BackColor = Colors.Surface;
        grid.DefaultCellStyle.ForeColor = Colors.TextPrimary;
        grid.DefaultCellStyle.SelectionBackColor = Colors.GridSelection;
        grid.DefaultCellStyle.SelectionForeColor = Colors.TextPrimary;
        grid.DefaultCellStyle.Padding = new Padding(8, 4, 4, 4);
        grid.DefaultCellStyle.Font = Fonts.Body;

        grid.AlternatingRowsDefaultCellStyle.BackColor = Colors.GridAltRow;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Colors.GridSelection;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Colors.TextPrimary;

        grid.RowsDefaultCellStyle.SelectionBackColor = Colors.GridSelection;
    }

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        GraphicsPath path = new();

        if (radius <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        int d = radius * 2;
        d = Math.Min(d, Math.Min(bounds.Width, bounds.Height));

        path.StartFigure();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        return path;
    }
}
