using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace KiotVietLabelPrinter.UI;

// Một chấm cọ, tọa độ theo ẢNH GỐC (không phải màn hình) nên người nhận không
// cần biết gì về zoom/pan đang ở đâu.
public sealed class BrushPaintEventArgs : EventArgs
{
    public BrushPaintEventArgs(float x, float y, float radius, bool additive)
    {
        X = x;
        Y = y;
        Radius = radius;
        Additive = additive;
    }

    public float X { get; }
    public float Y { get; }
    public float Radius { get; }

    // true = tô giữ lại (foreground), false = tẩy về nền.
    public bool Additive { get; }
}

// Khung xem ảnh có ZOOM + PAN, phục vụ soi biên mask khi kiểm tra chất lượng
// xóa nền.
//
//   - Fit: vừa khung (không phóng quá 100%). Hoặc phóng cố định 100/200/400%
//     bằng nút, hoặc phóng tự do bằng con lăn chuột (giữ điểm dưới con trỏ).
//   - Kéo chuột trái để pan khi ảnh lớn hơn khung. Nháy đúp: đổi nhanh Fit ↔ 100%.
//   - Nền caro (checkerboard) tùy chọn: vẽ ở tọa độ MÀN HÌNH, ô không đổi kích
//     thước khi zoom — giống Photoshop, để lộ rõ vùng bán trong suốt / halo.
//
// KHÔNG sở hữu vòng đời của Image — người gọi tự dispose.
public sealed class ZoomPanImageView : Control
{
    private Image? _image;
    private bool _checkerboard;

    private bool _fit = true;
    private float _zoom = 1f;
    private float _originX;
    private float _originY;

    private bool _dragging;
    private Point _dragAnchor;
    private float _dragOriginX;
    private float _dragOriginY;

    private WheelMessageFilter? _wheelFilter;

    private const float MinZoom = 0.05f;
    private const float MaxZoom = 16f;
    private const int CheckerTile = 9;

    private static readonly Color FrameColor = Color.FromArgb(150, 150, 150);
    private static readonly Color CheckerLight = Color.FromArgb(255, 255, 255);
    private static readonly Color CheckerDark = Color.FromArgb(198, 198, 198);

    // Bắn sau mọi thay đổi zoom/pan để chủ sở hữu cập nhật nhãn "%" và nút preset.
    public event EventHandler? ViewChanged;

    // ---- chế độ cọ sửa mask ----

    // Khi bật: chuột TRÁI tô cọ thay vì pan; pan chuyển sang chuột PHẢI (và vẫn
    // pan được bằng trái khi tắt cọ). Con trỏ hiện vòng tròn bằng đúng cỡ cọ.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool BrushEnabled
    {
        get => _brushEnabled;
        set
        {
            if (_brushEnabled == value)
                return;
            _brushEnabled = value;
            _painting = false;
            Cursor = value ? Cursors.Cross : Cursors.Default;
            Invalidate();
        }
    }

    // Bán kính cọ theo TỌA ĐỘ ẢNH (không đổi khi zoom — zoom to thì vòng tròn
    // trên màn hình to theo, đúng trực giác của người sửa ảnh).
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public float BrushRadius { get; set; } = 24f;

    // true = tô giữ lại (foreground), false = tẩy về nền.
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool BrushAdditive { get; set; }

    public event EventHandler? BrushStrokeStarted;
    public event EventHandler<BrushPaintEventArgs>? BrushPainted;
    public event EventHandler? BrushStrokeEnded;

    private bool _brushEnabled;
    private bool _painting;
    private PointF? _lastPaintImagePoint;
    private Point _cursorClient;
    private bool _cursorInside;

    public ZoomPanImageView()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);

        BackColor = FrameColor;
        TabStop = false;
    }

    public bool IsFit => _fit;

    // Zoom thực tế đang hiển thị (đã tính cả chế độ Fit).
    public float DisplayZoom => _image == null ? 1f : (_fit ? FitZoom() : _zoom);

    public void SetImage(Image? image, bool checkerboard, bool resetView)
    {
        _image = image;
        _checkerboard = checkerboard;
        _dragging = false;

        if (resetView)
            _fit = true;

        if (!_fit)
            ClampOrigin();

        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCheckerboard(bool on)
    {
        if (_checkerboard == on)
            return;
        _checkerboard = on;
        Invalidate();
    }

    public void ZoomFit()
    {
        _fit = true;
        _dragging = false;
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    // Phóng cố định về `zoom` (1 = 100%), giữ tâm khung.
    public void ZoomToPreset(float zoom) => ZoomAbout(zoom, new PointF(Width / 2f, Height / 2f));

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_wheelFilter == null)
        {
            _wheelFilter = new WheelMessageFilter(this);
            Application.AddMessageFilter(_wheelFilter);
        }
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DetachWheelFilter();
        base.OnHandleDestroyed(e);
    }

    private void DetachWheelFilter()
    {
        if (_wheelFilter == null)
            return;
        Application.RemoveMessageFilter(_wheelFilter);
        _wheelFilter = null;
    }

    // ---- bố cục ----

    private float FitZoom()
    {
        if (_image == null || _image.Width <= 0 || _image.Height <= 0 || Width <= 0 || Height <= 0)
            return 1f;

        float z = Math.Min((float)Width / _image.Width, (float)Height / _image.Height);
        return Math.Clamp(z, MinZoom, 1f);
    }

    private (float originX, float originY, float zoom) CurrentLayout()
    {
        if (_image == null)
            return (0f, 0f, 1f);

        if (_fit)
        {
            float z = FitZoom();
            return ((Width - (_image.Width * z)) / 2f, (Height - (_image.Height * z)) / 2f, z);
        }

        return (_originX, _originY, _zoom);
    }

    private void ClampOrigin()
    {
        if (_image == null)
            return;

        float iw = _image.Width * _zoom;
        float ih = _image.Height * _zoom;

        _originX = iw <= Width ? (Width - iw) / 2f : Math.Clamp(_originX, Width - iw, 0f);
        _originY = ih <= Height ? (Height - ih) / 2f : Math.Clamp(_originY, Height - ih, 0f);
    }

    private void ZoomAbout(float targetZoom, PointF pivot)
    {
        if (_image == null)
            return;

        (float ox, float oy, float z) = CurrentLayout();
        targetZoom = Math.Clamp(targetZoom, MinZoom, MaxZoom);

        // Giữ nguyên điểm-ảnh nằm dưới `pivot`.
        float imageX = (pivot.X - ox) / z;
        float imageY = (pivot.Y - oy) / z;

        _zoom = targetZoom;
        _originX = pivot.X - (imageX * _zoom);
        _originY = pivot.Y - (imageY * _zoom);
        _fit = false;

        ClampOrigin();
        Invalidate();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void HandleExternalWheel(int delta, Point clientPoint)
    {
        if (_image == null || !Visible)
            return;

        float factor = delta > 0 ? 1.2f : 1f / 1.2f;
        ZoomAbout(DisplayZoom * factor, clientPoint);
    }

    private bool CanPan()
    {
        if (_image == null)
            return false;

        float z = DisplayZoom;
        return (_image.Width * z) > Width + 0.5f || (_image.Height * z) > Height + 0.5f;
    }

    // ---- chuột ----

    // Đổi điểm màn hình -> điểm trên ảnh gốc.
    private PointF ToImagePoint(Point client)
    {
        (float ox, float oy, float z) = CurrentLayout();
        return new PointF((client.X - ox) / z, (client.Y - oy) / z);
    }

    private void StartPan(Point anchor)
    {
        (float ox, float oy, float z) = CurrentLayout();
        _originX = ox;
        _originY = oy;
        _zoom = z;
        _fit = false;

        _dragging = true;
        _dragAnchor = anchor;
        _dragOriginX = _originX;
        _dragOriginY = _originY;
        Cursor = Cursors.SizeAll;
    }

    // Tô liên tục dọc đoạn thẳng giữa 2 lần MouseMove — kéo nhanh không để lại
    // chuỗi chấm rời rạc.
    private void PaintTo(PointF target)
    {
        PointF from = _lastPaintImagePoint ?? target;
        float dx = target.X - from.X;
        float dy = target.Y - from.Y;
        float dist = (float)Math.Sqrt((dx * dx) + (dy * dy));

        float step = Math.Max(1f, BrushRadius * 0.35f);
        int steps = Math.Max(1, (int)Math.Ceiling(dist / step));

        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / steps;
            BrushPainted?.Invoke(this, new BrushPaintEventArgs(
                from.X + (dx * t), from.Y + (dy * t), BrushRadius, BrushAdditive));
        }

        _lastPaintImagePoint = target;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (_image == null)
            return;

        if (BrushEnabled && e.Button == MouseButtons.Left)
        {
            _painting = true;
            _lastPaintImagePoint = null;
            BrushStrokeStarted?.Invoke(this, EventArgs.Empty);
            PaintTo(ToImagePoint(e.Location));
            return;
        }

        // Cọ đang bật thì pan bằng chuột phải; tắt cọ thì trái pan như cũ.
        bool panButton = BrushEnabled ? e.Button == MouseButtons.Right : e.Button == MouseButtons.Left;
        if (panButton)
            StartPan(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        _cursorClient = e.Location;
        _cursorInside = true;

        if (_painting)
        {
            PaintTo(ToImagePoint(e.Location));
            return;
        }

        if (_dragging)
        {
            _originX = _dragOriginX + (e.X - _dragAnchor.X);
            _originY = _dragOriginY + (e.Y - _dragAnchor.Y);
            ClampOrigin();
            Invalidate();
            ViewChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (BrushEnabled)
        {
            Cursor = Cursors.Cross;
            Invalidate();   // vẽ lại vòng tròn cọ theo con trỏ
            return;
        }

        Cursor = CanPan() ? Cursors.SizeAll : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (_painting && e.Button == MouseButtons.Left)
        {
            _painting = false;
            _lastPaintImagePoint = null;
            BrushStrokeEnded?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (_dragging)
        {
            _dragging = false;
            Cursor = BrushEnabled ? Cursors.Cross : (CanPan() ? Cursors.SizeAll : Cursors.Default);
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _cursorInside = false;
        if (BrushEnabled)
            Invalidate();
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        // Đang cầm cọ thì nháy đúp là tô hai nhát, không phải lệnh đổi zoom.
        if (_image == null || BrushEnabled)
            return;

        if (_fit)
            ZoomAbout(1f, e.Location);
        else
            ZoomFit();
    }

    // ---- vẽ ----

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(FrameColor);

        if (_image == null)
            return;

        (float ox, float oy, float z) = CurrentLayout();
        RectangleF dest = new(ox, oy, _image.Width * z, _image.Height * z);

        if (_checkerboard)
            DrawChecker(g, dest);

        bool crisp = z >= 1f;
        g.InterpolationMode = crisp ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = crisp ? PixelOffsetMode.Half : PixelOffsetMode.HighQuality;
        g.CompositingMode = CompositingMode.SourceOver;

        RectangleF src = new(0f, 0f, _image.Width, _image.Height);
        g.DrawImage(_image, dest, src, GraphicsUnit.Pixel);

        if (BrushEnabled && _cursorInside)
            DrawBrushCursor(g, z);
    }

    // Vòng tròn cỡ cọ: viền đôi trắng/đen để nhìn rõ trên cả mask đen lẫn mask
    // trắng. Xanh = tô giữ lại, đỏ = tẩy về nền.
    private void DrawBrushCursor(Graphics g, float zoom)
    {
        float r = Math.Max(2f, BrushRadius * zoom);
        RectangleF circle = new(_cursorClient.X - r, _cursorClient.Y - r, r * 2f, r * 2f);

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using Pen halo = new(Color.FromArgb(160, 0, 0, 0), 3f);
        g.DrawEllipse(halo, circle);

        Color tint = BrushAdditive ? Color.FromArgb(90, 190, 255) : Color.FromArgb(255, 110, 110);
        using Pen pen = new(tint, 1.6f);
        g.DrawEllipse(pen, circle);

        g.SmoothingMode = SmoothingMode.Default;
    }

    private static void DrawChecker(Graphics g, RectangleF area)
    {
        g.SetClip(area);

        int x0 = (int)Math.Floor(area.Left / CheckerTile) * CheckerTile;
        int y0 = (int)Math.Floor(area.Top / CheckerTile) * CheckerTile;

        using SolidBrush light = new(CheckerLight);
        using SolidBrush dark = new(CheckerDark);

        for (int y = y0; y < area.Bottom; y += CheckerTile)
        {
            int cy = y / CheckerTile;
            for (int x = x0; x < area.Right; x += CheckerTile)
            {
                bool even = (((x / CheckerTile) + cy) & 1) == 0;
                g.FillRectangle(even ? light : dark, x, y, CheckerTile, CheckerTile);
            }
        }

        g.ResetClip();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            DetachWheelFilter();
        base.Dispose(disposing);
    }

    // Cho phép cuộn chuột để zoom KHI ĐANG DI CHUỘT TRÊN control mà không cần
    // control giữ focus (nếu không, việc focus theo hover sẽ cướp focus của
    // vùng cuộn cha). Chỉ chặn message khi con trỏ nằm trong control và form
    // chứa nó đang hoạt động.
    private sealed class WheelMessageFilter : IMessageFilter
    {
        private const int WmMouseWheel = 0x020A;
        private readonly ZoomPanImageView _owner;

        public WheelMessageFilter(ZoomPanImageView owner) => _owner = owner;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != WmMouseWheel)
                return false;

            if (_owner.IsDisposed || !_owner.IsHandleCreated || !_owner.Visible || _owner._image == null)
                return false;

            long lParam = m.LParam.ToInt64();
            Point screen = new((short)(lParam & 0xFFFF), (short)((lParam >> 16) & 0xFFFF));

            if (!_owner.RectangleToScreen(_owner.ClientRectangle).Contains(screen))
                return false;

            Form? form = _owner.FindForm();
            if (form == null || Form.ActiveForm != form)
                return false;

            int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            _owner.HandleExternalWheel(delta, _owner.PointToClient(screen));
            return true;
        }
    }
}
