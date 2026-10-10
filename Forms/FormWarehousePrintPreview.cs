using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

/// <summary>
/// Cửa sổ Xem trước bản in phiếu xuất kho. Mỗi trang hiển thị là một trang
/// của file PDF do CHÍNH Excel xuất từ file phiếu (cùng bộ máy phân trang với
/// lệnh in), nên khổ giấy, lề, tỷ lệ, font, độ rộng cột, chiều cao dòng, viền
/// và chỗ ngắt trang đúng như khi in. Tổng số trang là số trang thực tế của
/// bản xem trước. Cửa sổ chỉ hiển thị: lệnh in do màn hình phiếu thực hiện
/// (qua <c>printAction</c>) sau khi người dùng xác nhận.
/// </summary>
public sealed class FormWarehousePrintPreview : Form
{
    // Độ phân giải dựng trang: đủ nét khi phóng tới ~300%, ảnh Letter ~ 1870×2420 px.
    private const float PageDpi = 220f;
    private const int ThumbnailWidth = 210;
    private static readonly int[] ZoomSteps = [25, 50, 75, 100, 125, 150, 200, 300, 400];

    private readonly WarehousePrintPreview _preview;
    private readonly Func<IWin32Window, Task<bool>>? _printAction;
    private readonly string _printHint;
    private readonly PdfPageRenderer _renderer;
    private readonly WarehousePreviewNavigator _navigator;

    private Bitmap? _pageImage;
    private int _renderVersion;
    private bool _multiPage;
    private bool _printing;
    private readonly List<(Panel Frame, PictureBox Picture)> _thumbnails = new();

    private readonly Label lblTitle = new();
    private readonly Label lblSubtitle = new();
    private readonly FlowLayoutPanel pnlToolbar = new();
    private readonly RoundedButton btnFirst = new();
    private readonly RoundedButton btnPrevious = new();
    private readonly Label lblPage = new();
    private readonly RoundedButton btnNext = new();
    private readonly RoundedButton btnLast = new();
    private readonly RoundedButton btnZoomOut = new();
    private readonly Label lblZoom = new();
    private readonly RoundedButton btnZoomIn = new();
    private readonly RoundedButton btnFit = new();
    private readonly RoundedButton btnActualSize = new();
    private readonly RoundedButton btnSingle = new();
    private readonly RoundedButton btnMulti = new();

    private readonly ZoomPanImageView viewPage = new();
    private readonly FlowLayoutPanel pnlThumbnails = new();

    private readonly Panel pnlSide = new();
    private readonly Label lblInfo = new();
    private readonly Label lblPagesTitle = new();
    private readonly ListBox lstPages = new();
    private readonly Label lblIssuesTitle = new();
    private readonly ListBox lstIssues = new();

    private readonly Label lblHint = new();
    private readonly RoundedButton btnPrint = new();
    private readonly RoundedButton btnClose = new();

    public FormWarehousePrintPreview(WarehousePrintPreview preview, Func<IWin32Window, Task<bool>>? printAction, string printHint)
    {
        _preview = preview;
        _printAction = printAction;
        _printHint = printHint;
        _renderer = PdfPageRenderer.Open(preview.PdfPath);
        if (_renderer.PageCount < 1)
        {
            // Không giả làm "Trang 1 / 1" cho một tài liệu rỗng.
            _renderer.Dispose();
            throw new WarehouseException(WarehouseErrorKind.PreviewFailed,
                "Bản xem trước không có trang nào — hãy xuất lại phiếu rồi xem trước lại.");
        }
        _navigator = new WarehousePreviewNavigator(_renderer.PageCount);

        Text = $"Xem trước bản in — {Path.GetFileName(preview.WorkbookPath)}";
        Width = 1360;
        Height = 900;
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        KeyPreview = true;
        DoubleBuffered = true;
        AppTheme.StyleForm(this);

        BuildUi();
        FillSidePanel();
        UpdateNavigation();

        Shown += async (_, _) =>
        {
            viewPage.ZoomFit();
            await ShowCurrentPageAsync(resetView: true);
        };
        Resize += (_, _) => RelayoutAll();
        KeyDown += OnKeyDown;
    }

    #region Dựng giao diện
    private void BuildUi()
    {
        lblTitle.Text = "XEM TRƯỚC BẢN IN";
        lblTitle.Font = AppTheme.Fonts.SectionTitle;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblTitle);

        lblSubtitle.Text = $"{Path.GetFileName(_preview.WorkbookPath)} · sheet \"{_preview.SheetName}\" · " +
                           "mỗi trang là đúng một trang Excel sẽ in (Excel phân trang theo máy in đã chọn).";
        lblSubtitle.Font = AppTheme.Fonts.Hint;
        lblSubtitle.ForeColor = AppTheme.Colors.TextSecondary;
        lblSubtitle.AutoEllipsis = true;
        Controls.Add(lblSubtitle);

        pnlToolbar.FlowDirection = FlowDirection.LeftToRight;
        pnlToolbar.WrapContents = false;
        pnlToolbar.BackColor = AppTheme.Colors.Background;
        Controls.Add(pnlToolbar);

        AddToolButton(btnFirst, "TRANG ĐẦU", null, 116, () => Navigate(_navigator.First()));
        AddToolButton(btnPrevious, "TRANG TRƯỚC", IconGlyphs.Kind.ArrowLeft, 140, () => Navigate(_navigator.Previous()));

        lblPage.Font = AppTheme.Fonts.BodyBold;
        lblPage.ForeColor = AppTheme.Colors.TextPrimary;
        lblPage.TextAlign = ContentAlignment.MiddleCenter;
        lblPage.Size = new Size(130, 40);
        lblPage.Margin = new Padding(4, 0, 4, 0);
        pnlToolbar.Controls.Add(lblPage);

        AddToolButton(btnNext, "TRANG SAU", IconGlyphs.Kind.ArrowRight, 124, () => Navigate(_navigator.Next()));
        AddToolButton(btnLast, "TRANG CUỐI", null, 116, () => Navigate(_navigator.Last()));
        AddSeparator();
        AddToolButton(btnZoomOut, "−", null, 44, () => StepZoom(-1));

        lblZoom.Font = AppTheme.Fonts.Body;
        lblZoom.ForeColor = AppTheme.Colors.TextSecondary;
        lblZoom.TextAlign = ContentAlignment.MiddleCenter;
        lblZoom.Size = new Size(64, 40);
        pnlToolbar.Controls.Add(lblZoom);

        AddToolButton(btnZoomIn, "+", null, 44, () => StepZoom(+1));
        AddToolButton(btnFit, "VỪA TRANG", null, 112, () => viewPage.ZoomFit());
        AddToolButton(btnActualSize, "100%", null, 70, () => SetPaperZoom(100));
        AddSeparator();
        AddToolButton(btnSingle, "MỘT TRANG", null, 114, () => SetMultiPage(false));
        AddToolButton(btnMulti, "NHIỀU TRANG", null, 124, () => SetMultiPage(true));

        viewPage.ViewChanged += (_, _) => UpdateZoomLabel();
        Controls.Add(viewPage);

        pnlThumbnails.AutoScroll = true;
        pnlThumbnails.WrapContents = true;
        pnlThumbnails.BackColor = Color.FromArgb(150, 150, 150);
        pnlThumbnails.Padding = new Padding(16);
        pnlThumbnails.Visible = false;
        Controls.Add(pnlThumbnails);

        BuildSidePanel();

        lblHint.Font = AppTheme.Fonts.Hint;
        lblHint.ForeColor = AppTheme.Colors.TextSecondary;
        lblHint.AutoEllipsis = true;
        lblHint.Text = _printHint;
        Controls.Add(lblHint);

        btnPrint.Text = "IN";
        btnPrint.Icon = IconGlyphs.Kind.Printer;
        btnPrint.Variant = ButtonVariant.Primary;
        btnPrint.Font = AppTheme.Fonts.ButtonLarge;
        btnPrint.ContainerColor = AppTheme.Colors.Background;
        btnPrint.Enabled = _printAction != null;
        btnPrint.Click += async (_, _) => await PrintAsync();
        Controls.Add(btnPrint);

        btnClose.Text = "ĐÓNG";
        btnClose.Variant = ButtonVariant.Secondary;
        btnClose.ContainerColor = AppTheme.Colors.Background;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        RelayoutAll();
    }

    private void BuildSidePanel()
    {
        pnlSide.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlSide.Padding = new Padding(16);
        Controls.Add(pnlSide);

        lblInfo.Font = AppTheme.Fonts.Body;
        lblInfo.ForeColor = AppTheme.Colors.TextPrimary;
        pnlSide.Controls.Add(lblInfo);

        lblPagesTitle.Text = "CÁC TRANG";
        lblPagesTitle.Font = AppTheme.Fonts.Overline;
        lblPagesTitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlSide.Controls.Add(lblPagesTitle);

        lstPages.Font = AppTheme.Fonts.Hint;
        lstPages.BorderStyle = BorderStyle.FixedSingle;
        lstPages.IntegralHeight = false;
        lstPages.DrawMode = DrawMode.OwnerDrawVariable;
        lstPages.MeasureItem += (_, e) => e.ItemHeight = MeasureWrapped(lstPages, lstPages.Items[e.Index]?.ToString() ?? "");
        lstPages.DrawItem += (_, e) =>
        {
            if (e.Index >= 0)
                DrawWrapped(lstPages, e, lstPages.Items[e.Index]?.ToString() ?? "", AppTheme.Colors.SurfaceElevated, AppTheme.Colors.TextPrimary);
        };
        lstPages.SelectedIndexChanged += (_, _) =>
        {
            if (lstPages.Focused && lstPages.SelectedIndex >= 0)
                Navigate(_navigator.GoTo(lstPages.SelectedIndex + 1));
        };
        pnlSide.Controls.Add(lstPages);

        lblIssuesTitle.Font = AppTheme.Fonts.Overline;
        lblIssuesTitle.AutoEllipsis = true;
        lblIssuesTitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlSide.Controls.Add(lblIssuesTitle);

        lstIssues.Font = AppTheme.Fonts.Hint;
        lstIssues.BorderStyle = BorderStyle.FixedSingle;
        lstIssues.IntegralHeight = false;
        lstIssues.DrawMode = DrawMode.OwnerDrawVariable;
        lstIssues.MeasureItem += MeasureIssue;
        lstIssues.DrawItem += DrawIssue;
        lstIssues.Click += (_, _) =>
        {
            if (lstIssues.SelectedItem is WarehousePreviewIssue { Page: int page })
            {
                SetMultiPage(false);
                Navigate(_navigator.GoTo(page));
            }
        };
        pnlSide.Controls.Add(lstIssues);
    }

    private void AddToolButton(RoundedButton button, string text, IconGlyphs.Kind? icon, int width, Action onClick)
    {
        button.Text = text;
        button.Icon = icon;
        button.Variant = ButtonVariant.Outline;
        button.ContainerColor = AppTheme.Colors.Background;
        button.Size = new Size(width, 40);
        button.Margin = new Padding(0, 0, 6, 0);
        button.Click += (_, _) => onClick();
        pnlToolbar.Controls.Add(button);
    }

    private void AddSeparator()
    {
        pnlToolbar.Controls.Add(new Panel
        {
            Size = new Size(1, 32),
            Margin = new Padding(8, 4, 12, 4),
            BackColor = AppTheme.Colors.BorderStrong
        });
    }

    private void RelayoutAll()
    {
        const int pad = 20;
        const int sideWidth = 360;
        int width = ClientSize.Width;
        int height = ClientSize.Height;

        lblTitle.SetBounds(pad, 12, 400, 28);
        lblSubtitle.SetBounds(pad, 40, width - pad * 2, 18);
        pnlToolbar.SetBounds(pad, 66, width - pad * 2, 44);

        int contentTop = 118;
        int contentBottom = height - 70;
        int viewWidth = width - pad * 2 - sideWidth - 12;
        viewPage.SetBounds(pad, contentTop, viewWidth, contentBottom - contentTop);
        pnlThumbnails.SetBounds(pad, contentTop, viewWidth, contentBottom - contentTop);
        pnlSide.SetBounds(width - pad - sideWidth, contentTop, sideWidth, contentBottom - contentTop);
        LayoutSidePanel();

        btnClose.SetBounds(width - pad - 120, height - 58, 120, 44);
        btnPrint.SetBounds(width - pad - 120 - 12 - 160, height - 58, 160, 44);
        lblHint.SetBounds(pad, height - 46, Math.Max(100, btnPrint.Left - pad * 2), 20);
    }

    private void LayoutSidePanel()
    {
        int inner = pnlSide.ClientSize.Width - 32;
        int top = 16;
        Size infoSize = TextRenderer.MeasureText(lblInfo.Text, lblInfo.Font, new Size(inner, int.MaxValue), TextFormatFlags.WordBreak);
        lblInfo.SetBounds(16, top, inner, infoSize.Height + 4);
        top = lblInfo.Bottom + 12;

        int remaining = pnlSide.ClientSize.Height - top - 16;
        int pagesHeight = Math.Max(70, Math.Min(150, remaining / 3));
        lblPagesTitle.SetBounds(16, top, inner, 16);
        lstPages.SetBounds(16, top + 20, inner, pagesHeight);
        top = lstPages.Bottom + 12;

        lblIssuesTitle.SetBounds(16, top, inner, 16);
        lstIssues.SetBounds(16, top + 20, inner, Math.Max(60, pnlSide.ClientSize.Height - top - 36));
    }
    #endregion

    #region Thông tin trang + cảnh báo
    private void FillSidePanel()
    {
        WarehousePageSetup page = _preview.Page;
        string pagesLine = $"Tổng số trang: {_renderer.PageCount}" +
                           (_preview.TemplatePageCount is int t ? $"  (file mẫu: {t} trang)" : "");
        lblInfo.Text = string.Join(Environment.NewLine,
            pagesLine,
            $"Khổ giấy (đặt trong file): {page.PaperName}",
            $"Trang in thực tế: {_preview.PrintedPaper}",
            $"Hướng giấy: {page.OrientationName}",
            $"Tỷ lệ in: {page.ScaleDescription}",
            $"Vùng in: {page.PrintArea ?? "không đặt (in toàn bộ vùng có dữ liệu)"}",
            $"Lề: {page.Margins.Describe()}",
            $"Font: {_preview.FontDescription}",
            $"Máy in phân trang: {_preview.PaginationPrinter}");

        lstPages.Items.Clear();
        if (_preview.Pages.Count == _renderer.PageCount)
        {
            foreach (WarehousePreviewPage p in _preview.Pages)
                lstPages.Items.Add($"Trang {p.Number} · {p.Content}");
        }
        else
        {
            for (int i = 1; i <= _renderer.PageCount; i++)
                lstPages.Items.Add($"Trang {i}");
        }

        lstIssues.Items.Clear();
        foreach (WarehousePreviewIssue issue in _preview.Issues)
            lstIssues.Items.Add(issue);
        if (_preview.Issues.Count == 0)
            lstIssues.Items.Add(new WarehousePreviewIssue(WarehousePreviewIssueLevel.Info,
                "Không phát hiện vấn đề bố cục. Vẫn nên xem lướt từng trang trước khi in."));

        lblIssuesTitle.Text = _preview.WarningCount > 0
            ? $"KIỂM TRA BỐ CỤC — {_preview.WarningCount} CẢNH BÁO"
            : "KIỂM TRA BỐ CỤC";
        lblIssuesTitle.ForeColor = _preview.WarningCount > 0 ? AppTheme.Colors.Danger : AppTheme.Colors.TextMuted;
        LayoutSidePanel();
    }

    private static string IssueText(WarehousePreviewIssue issue) =>
        (issue.Level == WarehousePreviewIssueLevel.Warning ? "⚠ " : "ℹ ") +
        (issue.Page is int page ? $"[Trang {page}] " : "") + issue.Message;

    private void MeasureIssue(object? sender, MeasureItemEventArgs e)
    {
        if (e.Index >= 0 && lstIssues.Items[e.Index] is WarehousePreviewIssue issue)
            e.ItemHeight = MeasureWrapped(lstIssues, IssueText(issue));
    }

    private void DrawIssue(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || lstIssues.Items[e.Index] is not WarehousePreviewIssue issue)
            return;

        bool warning = issue.Level == WarehousePreviewIssueLevel.Warning;
        DrawWrapped(lstIssues, e, IssueText(issue),
            warning ? Color.FromArgb(255, 247, 230) : AppTheme.Colors.SurfaceElevated,
            warning ? Color.FromArgb(160, 98, 0) : AppTheme.Colors.TextSecondary);
    }

    // ListBox mặc định cắt chữ trên một dòng — mô tả trang / cảnh báo dài được xuống dòng đủ.
    private static int MeasureWrapped(ListBox list, string text)
    {
        Size size = TextRenderer.MeasureText(text, list.Font, new Size(Math.Max(50, list.ClientSize.Width - 12), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        return Math.Min(255, size.Height + 10);
    }

    private static void DrawWrapped(ListBox list, DrawItemEventArgs e, string text, Color back, Color fore)
    {
        if ((e.State & DrawItemState.Selected) != 0)
            back = AppTheme.Colors.GridSelection;

        using (SolidBrush brush = new(back))
            e.Graphics.FillRectangle(brush, e.Bounds);
        Rectangle textBounds = new(e.Bounds.X + 6, e.Bounds.Y + 5, e.Bounds.Width - 12, e.Bounds.Height - 6);
        TextRenderer.DrawText(e.Graphics, text, list.Font, textBounds, fore, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        using Pen line = new(AppTheme.Colors.Border);
        e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
    }
    #endregion

    #region Chuyển trang + phóng to
    private void Navigate(bool changed)
    {
        UpdateNavigation();
        if (changed)
            _ = ShowCurrentPageAsync(resetView: false);
    }

    private void UpdateNavigation()
    {
        lblPage.Text = _navigator.Label;
        bool idle = !_printing;
        btnFirst.Enabled = idle && _navigator.CanGoBack;
        btnPrevious.Enabled = idle && _navigator.CanGoBack;
        btnNext.Enabled = idle && _navigator.CanGoForward;
        btnLast.Enabled = idle && _navigator.CanGoForward;
        btnZoomIn.Enabled = idle && !_multiPage;
        btnZoomOut.Enabled = idle && !_multiPage;
        btnFit.Enabled = idle && !_multiPage;
        btnActualSize.Enabled = idle && !_multiPage;
        btnSingle.Variant = _multiPage ? ButtonVariant.Outline : ButtonVariant.Primary;
        btnMulti.Variant = _multiPage ? ButtonVariant.Primary : ButtonVariant.Outline;
        btnSingle.Invalidate();
        btnMulti.Invalidate();
        btnPrint.Enabled = idle && _printAction != null;
        btnClose.Enabled = idle;

        if (lstPages.SelectedIndex != _navigator.Current - 1 && _navigator.Current - 1 < lstPages.Items.Count)
            lstPages.SelectedIndex = _navigator.Current - 1;

        foreach ((Panel frame, _) in _thumbnails)
            frame.BackColor = (int)frame.Tag! == _navigator.Current ? AppTheme.Colors.Primary : Color.Transparent;
    }

    /// <summary>Dựng ảnh trang hiện tại ở luồng nền; bấm chuyển trang nhanh thì chỉ hiện ảnh của lần bấm cuối.</summary>
    private async Task ShowCurrentPageAsync(bool resetView)
    {
        int version = ++_renderVersion;
        int pageIndex = _navigator.Current - 1;
        Bitmap image;
        try
        {
            image = await Task.Run(() => _renderer.Render(pageIndex, PageDpi));
        }
        catch (ObjectDisposedException) when (IsDisposed || Disposing)
        {
            // Cửa sổ đã đóng trong lúc đang dựng trang: không còn gì để hiển thị.
            return;
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Dựng trang {pageIndex + 1} của {_preview.PdfPath}", ex);
            if (!IsDisposed && version == _renderVersion)
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (version != _renderVersion || IsDisposed)
        {
            image.Dispose();
            return;
        }

        Bitmap? old = _pageImage;
        _pageImage = image;
        viewPage.SetImage(image, checkerboard: false, resetView: resetView);
        old?.Dispose();
    }

    /// <summary>Tỷ lệ so với khổ giấy thật (100% = đúng kích thước tờ giấy trên màn hình).</summary>
    private float PaperZoomPercent => viewPage.DisplayZoom * PageDpi / DeviceDpi * 100f;

    private void SetPaperZoom(int percent) => viewPage.ZoomToPreset(percent / 100f * DeviceDpi / PageDpi);

    private void StepZoom(int direction)
    {
        float current = PaperZoomPercent;
        int target = direction > 0
            ? ZoomSteps.FirstOrDefault(z => z > current + 1, ZoomSteps[^1])
            : ZoomSteps.LastOrDefault(z => z < current - 1, ZoomSteps[0]);
        SetPaperZoom(target);
    }

    private void UpdateZoomLabel() => lblZoom.Text = $"{PaperZoomPercent:0}%";

    private void SetMultiPage(bool multi)
    {
        if (_multiPage == multi)
            return;

        _multiPage = multi;
        viewPage.Visible = !multi;
        pnlThumbnails.Visible = multi;
        if (multi && _thumbnails.Count == 0)
            _ = BuildThumbnailsAsync();
        UpdateNavigation();
    }

    /// <summary>Chế độ nhiều trang: ảnh thu nhỏ của mọi trang, bấm một trang để mở trang đó.</summary>
    private async Task BuildThumbnailsAsync()
    {
        for (int i = 0; i < _renderer.PageCount; i++)
        {
            int pageNumber = i + 1;
            SizeF size = _renderer.GetPageSizePoints(i);
            float dpi = ThumbnailWidth / (size.Width / 72f);

            Panel frame = new() { Padding = new Padding(3), Margin = new Padding(10), Tag = pageNumber };
            PictureBox picture = new()
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.White,
                Dock = DockStyle.Fill,
                Cursor = Cursors.Hand
            };
            Label caption = new()
            {
                Text = $"Trang {pageNumber}",
                Dock = DockStyle.Bottom,
                Height = 22,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.White,
                Font = AppTheme.Fonts.BodyBold
            };
            frame.Size = new Size(ThumbnailWidth + 6, (int)(ThumbnailWidth * size.Height / size.Width) + 6 + caption.Height);
            frame.Controls.Add(picture);
            frame.Controls.Add(caption);
            picture.Click += (_, _) =>
            {
                SetMultiPage(false);
                Navigate(_navigator.GoTo(pageNumber));
            };
            pnlThumbnails.Controls.Add(frame);
            _thumbnails.Add((frame, picture));
            UpdateNavigation();

            int index = i;
            Bitmap thumb;
            try
            {
                thumb = await Task.Run(() => _renderer.Render(index, dpi));
            }
            catch (ObjectDisposedException) when (IsDisposed || Disposing)
            {
                return;
            }
            catch (WarehouseException ex)
            {
                // Một trang thu nhỏ lỗi: ghi log, để khung trống kèm chữ báo, vẫn dựng các trang khác.
                WarehouseLog.Error($"Dựng ảnh thu nhỏ trang {pageNumber} của {_preview.PdfPath}", ex);
                caption.Text = $"Trang {pageNumber} — lỗi hiển thị";
                continue;
            }

            if (IsDisposed)
            {
                thumb.Dispose();
                return;
            }
            picture.Image = thumb;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_printing)
            return;

        switch (e.KeyCode)
        {
            case Keys.PageDown or Keys.Right when !e.Control:
                Navigate(_navigator.Next());
                break;
            case Keys.PageUp or Keys.Left when !e.Control:
                Navigate(_navigator.Previous());
                break;
            case Keys.Home:
                Navigate(_navigator.First());
                break;
            case Keys.End:
                Navigate(_navigator.Last());
                break;
            case Keys.Oemplus or Keys.Add when e.Control:
                StepZoom(+1);
                break;
            case Keys.OemMinus or Keys.Subtract when e.Control:
                StepZoom(-1);
                break;
            case Keys.Escape:
                Close();
                break;
            default:
                return;
        }

        e.Handled = true;
    }
    #endregion

    #region In
    private async Task PrintAsync()
    {
        if (_printAction == null)
            return;

        _printing = true;
        UpdateNavigation();
        UseWaitCursor = true;
        try
        {
            if (await _printAction(this))
                lblHint.Text = "Đã gửi lệnh in. Có thể đóng cửa sổ xem trước.";
        }
        finally
        {
            _printing = false;
            UseWaitCursor = false;
            UpdateNavigation();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Đang gửi lệnh in qua Excel: chờ xong (có giới hạn thời gian) rồi mới đóng.
        if (_printing)
            e.Cancel = true;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderVersion++;
            viewPage.SetImage(null, checkerboard: false, resetView: true);
            _pageImage?.Dispose();
            foreach ((_, PictureBox picture) in _thumbnails)
                picture.Image?.Dispose();
            _renderer.Dispose();
        }

        base.Dispose(disposing);
    }
    #endregion
}
