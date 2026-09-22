using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.Services.BackgroundRemoval;
using KiotVietLabelPrinter.UI;
using SkiaSharp;

namespace KiotVietLabelPrinter.Views;

// Màn hình "Xóa nền ảnh" — nhúng trực tiếp vào FormMain giống cách pnlWorkspace
// hiện có được show/hide (không phải Form/dialog riêng), dùng đúng cơ chế
// navigation sẵn có của app (xem FormMain.ShowHome/ShowBackgroundRemover).
//
// UI chỉ gọi BackgroundRemovalService — mọi logic ONNX/pixel nằm trong service.
public class BackgroundRemoverView : UserControl
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private readonly BackgroundRemovalService _service = new();
    private readonly List<BackgroundRemovalItem> _items = new();

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    // Card nền (giống pnlWorkspace) — chứa toàn bộ nội dung màn hình.
    private readonly RoundedPanel pnlCard = new();

    private readonly RoundedPanel pnlIconBadge = new();
    private readonly IconGlyph iconHeader = new();
    private readonly Label lblHeaderTitle = new();
    private readonly Label lblHeaderSubtitle = new();

    private readonly RoundedPanel pnlDropZone = new();
    private readonly Label lblDropHint = new();
    private readonly RoundedButton btnChooseFiles = new();
    private readonly RoundedButton btnChooseFolder = new();

    private readonly RoundedTextBox txtOutputFolder = new();
    private readonly RoundedButton btnChooseOutput = new();
    private readonly ComboBox cboDevice = new();
    private readonly ComboBox cboQuality = new();
    private readonly ToggleSwitch chkDebug = new();

    private readonly RoundedButton btnStart = new();
    private readonly RoundedButton btnStop = new();
    private readonly RoundedButton btnClearList = new();
    private readonly RoundedButton btnOpenOutput = new();

    // Bề rộng "tự nhiên" của 4 nút thao tác khi xếp 1 hàng (màn đủ rộng).
    private const int WStart = 150, WStop = 120, WClear = 178, WOpen = 210;

    // Chiều cao thực của vùng 4 nút: 1 hàng = 44, lưới 2×2 = 44 + 12 + 44 = 100.
    // Tính theo bố cục ĐANG dùng thay vì chốt cứng 108 cho cả hai — chốt cứng làm
    // màn rộng mất không 64 px, mà chỗ đó chính là chiều cao khung soi mask.
    private int ActionBlockHeight => ActionsFitOneRow() ? 44 : 100;

    private readonly ProgressBar progressBar = new();
    private readonly Label lblProgress = new();

    private readonly Panel pnlGrid = new();
    private readonly SmoothDataGridView dgvImages = new();

    // ----- Preview: 4 chế độ soi mask + zoom/pan -----
    private enum PreviewMode { Original, Mask, Checkerboard, Result }

    private readonly Panel pnlPreview = new();
    private readonly Label lblPreviewTitle = new();
    private readonly RoundedButton btnModeOriginal = new();
    private readonly RoundedButton btnModeMask = new();
    private readonly RoundedButton btnModeChecker = new();
    private readonly RoundedButton btnModeResult = new();
    private readonly RoundedButton btnZoomFit = new();
    private readonly RoundedButton btnZoom100 = new();
    private readonly RoundedButton btnZoom200 = new();
    private readonly RoundedButton btnZoom400 = new();
    private readonly Label lblZoomValue = new();
    private readonly ZoomPanImageView zoomView = new();
    private readonly Label lblPreviewEmpty = new();

    // ----- Cọ sửa mask -----
    private readonly RoundedButton btnBrush = new();
    private readonly RoundedButton btnBrushKeep = new();
    private readonly RoundedButton btnBrushErase = new();
    private readonly RoundedButton btnBrushSmaller = new();
    private readonly Label lblBrushSize = new();
    private readonly RoundedButton btnBrushBigger = new();
    private readonly RoundedButton btnMaskUndo = new();
    private readonly RoundedButton btnMaskReset = new();
    private readonly RoundedButton btnMaskApply = new();

    private static readonly float[] BrushSizes = { 6f, 12f, 24f, 48f, 96f, 160f };
    private int _brushSizeIndex = 2;

    // Phiên sửa mask của ảnh đang chọn. null khi ảnh chưa xử lý hoặc thiếu cache.
    private MaskEditSession? _editSession;

    private PreviewMode _previewMode = PreviewMode.Original;

    // Các layer preview đã giải mã cho ảnh đang chọn — nạp nền, giữ đủ 4 để đổi
    // chế độ tức thì (không đọc lại đĩa) khi so sánh biên.
    private sealed class PreviewLayers : IDisposable
    {
        public Image? Original;
        public Image? Mask;
        public Image? Cutout;
        public Image? Result;

        public void Dispose()
        {
            Original?.Dispose();
            Mask?.Dispose();
            Cutout?.Dispose();
            Result?.Dispose();
        }
    }

    private PreviewLayers? _previewLayers;
    private Guid _previewToken;
    private BackgroundRemovalItem? _previewItem;

    // Ảnh soi mask ghi tạm ra đây sau khi xử lý xong (mỗi ảnh 1 cặp file theo
    // item.Id). Dọn khi xóa danh sách / đóng màn hình.
    private readonly string _previewCacheDir = Path.Combine(
        Path.GetTempPath(), "KiotVietLabelPrinter", "bg-preview", Guid.NewGuid().ToString("N"));

    private const int PreviewMaxSide = 3000;

    public string OutputFolder => txtOutputFolder.Text.Trim();

    public BackgroundRemoverView()
    {
        Dock = DockStyle.Fill;
        BuildUi();
        WireDragDrop();

        txtOutputFolder.Text = Path.Combine(AppContext.BaseDirectory, "Output", "BackgroundRemoved");
    }

    private void BuildUi()
    {
        pnlCard.Dock = DockStyle.Fill;
        pnlCard.CornerRadius = 22;
        pnlCard.FillColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.BorderColor = AppTheme.Colors.Border;
        pnlCard.BorderThickness = 1;
        pnlCard.ShadowEnabled = true;
        pnlCard.AutoScroll = true;
        Controls.Add(pnlCard);

        BuildHeader();
        BuildDropZone();
        BuildSettingsRow();
        BuildActionButtons();
        BuildProgressRow();
        BuildSplitContent();
    }

    #region Header
    private void BuildHeader()
    {
        pnlIconBadge.Left = 32;
        pnlIconBadge.Top = 32;
        pnlIconBadge.Width = 44;
        pnlIconBadge.Height = 44;
        pnlIconBadge.CornerRadius = 13;
        pnlIconBadge.FillColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.BorderThickness = 0;
        pnlIconBadge.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlIconBadge);

        iconHeader.Dock = DockStyle.Fill;
        iconHeader.Kind = IconGlyphs.Kind.Image;
        iconHeader.IconColor = AppTheme.Colors.Primary;
        iconHeader.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.Controls.Add(iconHeader);

        lblHeaderTitle.Text = "XÓA NỀN ẢNH";
        lblHeaderTitle.Left = 88;
        lblHeaderTitle.Top = 32;
        lblHeaderTitle.Width = 500;
        lblHeaderTitle.Height = 26;
        lblHeaderTitle.Font = AppTheme.Fonts.SectionTitle;
        lblHeaderTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblHeaderTitle);

        lblHeaderSubtitle.Text = "Xóa nền tự động và thay bằng nền trắng.";
        lblHeaderSubtitle.Left = 88;
        lblHeaderSubtitle.Top = 58;
        lblHeaderSubtitle.Width = 500;
        lblHeaderSubtitle.Height = 18;
        lblHeaderSubtitle.Font = AppTheme.Fonts.Hint;
        lblHeaderSubtitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlCard.Controls.Add(lblHeaderSubtitle);

        Panel divider = new()
        {
            Left = 32,
            Top = 96,
            Width = Math.Max(0, pnlCard.Width - 64),
            Height = 1,
            BackColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlCard.Controls.Add(divider);
    }
    #endregion

    #region Drop zone
    private void BuildDropZone()
    {
        pnlDropZone.Left = 32;
        pnlDropZone.Top = 116;
        pnlDropZone.Width = Math.Max(0, pnlCard.Width - 64);
        pnlDropZone.Height = 108;
        pnlDropZone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlDropZone.CornerRadius = 16;
        pnlDropZone.FillColor = AppTheme.Colors.PrimaryLight;
        pnlDropZone.BorderColor = AppTheme.Colors.BorderOutlineRest;
        pnlDropZone.BorderThickness = 1;
        pnlDropZone.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlDropZone);

        lblDropHint.Text = "Kéo ảnh vào đây  •  hỗ trợ JPG, PNG, WEBP";
        lblDropHint.Left = 0;
        lblDropHint.Top = 16;
        lblDropHint.Width = pnlDropZone.Width;
        lblDropHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblDropHint.TextAlign = ContentAlignment.MiddleCenter;
        lblDropHint.Font = AppTheme.Fonts.BodyBold;
        lblDropHint.ForeColor = AppTheme.Colors.TextSecondary;
        pnlDropZone.Controls.Add(lblDropHint);

        btnChooseFiles.Text = "Chọn ảnh";
        btnChooseFiles.Icon = IconGlyphs.Kind.Image;
        btnChooseFiles.Size = new Size(150, 40);
        btnChooseFiles.Top = 54;
        btnChooseFiles.Variant = ButtonVariant.Outline;
        btnChooseFiles.ContainerColor = AppTheme.Colors.PrimaryLight;
        btnChooseFiles.Click += (_, _) => ChooseFiles();
        pnlDropZone.Controls.Add(btnChooseFiles);

        btnChooseFolder.Text = "Chọn thư mục";
        btnChooseFolder.Icon = IconGlyphs.Kind.Folder;
        btnChooseFolder.Size = new Size(160, 40);
        btnChooseFolder.Top = 54;
        btnChooseFolder.Variant = ButtonVariant.Outline;
        btnChooseFolder.ContainerColor = AppTheme.Colors.PrimaryLight;
        btnChooseFolder.Click += (_, _) => ChooseFolder();
        pnlDropZone.Controls.Add(btnChooseFolder);

        void CenterButtons(object? s, EventArgs e)
        {
            int gap = 12;
            int totalWidth = btnChooseFiles.Width + gap + btnChooseFolder.Width;
            int startX = Math.Max(0, (pnlDropZone.Width - totalWidth) / 2);
            btnChooseFiles.Left = startX;
            btnChooseFolder.Left = startX + btnChooseFiles.Width + gap;
        }

        CenterButtons(null, EventArgs.Empty);
        pnlDropZone.SizeChanged += CenterButtons;
    }

    private void WireDragDrop()
    {
        pnlDropZone.AllowDrop = true;
        pnlDropZone.DragEnter += (_, e) =>
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        };
        pnlDropZone.DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
                AddPaths(paths);
        };
    }
    #endregion

    #region Settings row
    private void BuildSettingsRow()
    {
        int top = pnlDropZone.Bottom + 16;

        Label lblOutput = new()
        {
            Text = "Thư mục lưu",
            Left = 32,
            Top = top + 10,
            Width = 100,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlCard.Controls.Add(lblOutput);

        // Left/Width của btnChooseOutput + txtOutputFolder KHÔNG dùng Anchor tự
        // co giãn (cùng lý do đã nêu ở RelayoutSplit): pnlCard còn kích thước mặc
        // định rất nhỏ tại thời điểm BuildUi() chạy, nên phải tự tính lại từ đầu
        // mỗi khi pnlCard đổi kích thước thật — xem LayoutSettingsRow().
        btnChooseOutput.Text = "Chọn";
        btnChooseOutput.Icon = IconGlyphs.Kind.Folder;
        btnChooseOutput.Width = 110;
        btnChooseOutput.Height = 40;
        btnChooseOutput.Top = top;
        btnChooseOutput.Variant = ButtonVariant.Outline;
        btnChooseOutput.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnChooseOutput.Click += (_, _) => ChooseOutputFolder();
        pnlCard.Controls.Add(btnChooseOutput);

        txtOutputFolder.Left = 138;
        txtOutputFolder.Top = top;
        txtOutputFolder.Height = 40;
        txtOutputFolder.ContainerColor = AppTheme.Colors.SurfaceElevated;
        txtOutputFolder.ReadOnly = true;
        pnlCard.Controls.Add(txtOutputFolder);

        Label lblDevice = new()
        {
            Text = "Thiết bị",
            Left = 32,
            Top = top + 48,
            Width = 100,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlCard.Controls.Add(lblDevice);

        cboDevice.Left = 138;
        cboDevice.Top = top + 40;
        cboDevice.Width = 160;
        cboDevice.DropDownStyle = ComboBoxStyle.DropDownList;
        cboDevice.Items.AddRange(new object[] { "Auto", "CPU", "GPU" });
        cboDevice.SelectedIndex = 0;
        AppTheme.StyleComboBox(cboDevice);
        pnlCard.Controls.Add(cboDevice);

        // Provider ONNX Runtime thực tế đang chạy + model đang dùng — cập nhật khi
        // bắt đầu xử lý (StartProcessing gọi EnsureLoaded trước batch), không phải
        // chỉ "không crash". Log chi tiết ở BackgroundRemovalDiagnosticsLog.LogFolder.
        lblProvider.Left = 310;
        lblProvider.Top = top + 48;
        lblProvider.Width = 360;
        lblProvider.Font = AppTheme.Fonts.Hint;
        lblProvider.ForeColor = AppTheme.Colors.TextMuted;
        lblProvider.Text = "Model / Provider: chưa tải";
        pnlCard.Controls.Add(lblProvider);

        Label lblQuality = new()
        {
            Text = "Chất lượng",
            Left = 32,
            Top = top + 96,
            Width = 100,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlCard.Controls.Add(lblQuality);

        cboQuality.Left = 138;
        cboQuality.Top = top + 88;
        cboQuality.Width = 210;
        cboQuality.DropDownStyle = ComboBoxStyle.DropDownList;
        cboQuality.Items.AddRange(new object[]
        {
            "Nhanh — segmentation + composite",
            "Chất lượng cao — + tinh chỉnh biên",
            "Tối đa — + matting AI vùng biên"
        });
        // Mặc định Ultra: chỉ pass matting mới giữ được sợi lông/tóc ở biên —
        // HighQuality cắt phẳng thành khối (đo trên ảnh mũ: dải bán trong suốt
        // 4,69% so với 7,87% của Ultra). Đổi lại chậm hơn ~8× trên CPU.
        cboQuality.SelectedIndex = 2;
        AppTheme.StyleComboBox(cboQuality);
        pnlCard.Controls.Add(cboQuality);

        chkDebug.Text = "Lưu mask debug";
        chkDebug.Left = 372;
        chkDebug.Top = top + 90;
        chkDebug.Width = 200;
        chkDebug.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(chkDebug);

        pnlCard.SizeChanged += (_, _) => LayoutSettingsRow();
        LayoutSettingsRow();

        RepositionAfterSettings(top + 132);
    }

    public QualityMode SelectedQualityMode => cboQuality.SelectedIndex switch
    {
        0 => QualityMode.Fast,
        2 => QualityMode.Ultra,
        _ => QualityMode.HighQuality
    };

    private readonly Label lblProvider = new();

    private void LayoutSettingsRow()
    {
        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);

        btnChooseOutput.Left = Math.Max(txtOutputFolder.Left, cardWidth - 32 - btnChooseOutput.Width);
        txtOutputFolder.Width = Math.Max(1, btnChooseOutput.Left - 12 - txtOutputFolder.Left);
    }

    private int _afterSettingsTop;

    private void RepositionAfterSettings(int top)
    {
        _afterSettingsTop = top;
    }

    private void ChooseOutputFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            SelectedPath = Directory.Exists(txtOutputFolder.Text) ? txtOutputFolder.Text : ""
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            txtOutputFolder.Text = dialog.SelectedPath;
    }
    #endregion

    #region Action buttons + progress
    // 4 nút thao tác gắn thẳng vào pnlCard và tự xếp lại mỗi khi pnlCard đổi kích
    // thước thật (KHÔNG dùng FlowLayoutPanel + Anchor — cùng lý do đã nêu ở
    // LayoutSettingsRow/RelayoutSplit: baseline co giãn bị chốt sai lúc dựng UI).
    private void BuildActionButtons()
    {
        btnStart.Text = "XÓA NỀN";
        btnStart.Icon = IconGlyphs.Kind.MagicWand;
        btnStart.Variant = ButtonVariant.Primary;
        btnStart.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnStart.Click += (_, _) => StartProcessing();
        pnlCard.Controls.Add(btnStart);

        btnStop.Text = "DỪNG";
        btnStop.Icon = IconGlyphs.Kind.Stop;
        btnStop.Variant = ButtonVariant.Outline;
        btnStop.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnStop.Enabled = false;
        btnStop.Click += (_, _) => _cts?.Cancel();
        pnlCard.Controls.Add(btnStop);

        btnClearList.Text = "XÓA DANH SÁCH";
        btnClearList.Icon = IconGlyphs.Kind.Trash;
        btnClearList.Variant = ButtonVariant.Outline;
        btnClearList.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnClearList.Click += (_, _) => ClearList();
        pnlCard.Controls.Add(btnClearList);

        btnOpenOutput.Text = "MỞ THƯ MỤC OUTPUT";
        btnOpenOutput.Icon = IconGlyphs.Kind.Folder;
        btnOpenOutput.Variant = ButtonVariant.Outline;
        btnOpenOutput.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnOpenOutput.Click += (_, _) => OpenOutputFolder();
        pnlCard.Controls.Add(btnOpenOutput);

        pnlCard.SizeChanged += (_, _) => LayoutActionButtons();
        LayoutActionButtons();
    }

    // Màn đủ rộng: 1 hàng [XÓA NỀN] [DỪNG] [XÓA DANH SÁCH] [MỞ THƯ MỤC OUTPUT].
    // Màn hẹp: lưới 2×2, mỗi nút giãn bằng nửa bề rộng khả dụng.
    private bool ActionsFitOneRow()
    {
        int avail = Math.Max(1, pnlCard.ClientSize.Width - 32 - 32);
        return avail >= WStart + WStop + WClear + WOpen + (12 * 3);
    }

    private void LayoutActionButtons()
    {
        const int left = 32;
        const int gap = 12;
        const int rowH = 44;

        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);
        int avail = Math.Max(1, cardWidth - left - 32);
        int top = _afterSettingsTop;

        if (ActionsFitOneRow())
        {
            int x = left;
            btnStart.SetBounds(x, top, WStart, rowH); x += WStart + gap;
            btnStop.SetBounds(x, top, WStop, rowH); x += WStop + gap;
            btnClearList.SetBounds(x, top, WClear, rowH); x += WClear + gap;
            btnOpenOutput.SetBounds(x, top, WOpen, rowH);
        }
        else
        {
            int colW = Math.Max(140, (avail - gap) / 2);
            int col2 = left + colW + gap;
            int row2 = top + rowH + gap;
            btnStart.SetBounds(left, top, colW, rowH);
            btnStop.SetBounds(col2, top, colW, rowH);
            btnClearList.SetBounds(left, row2, colW, rowH);
            btnOpenOutput.SetBounds(col2, row2, colW, rowH);
        }
    }

    private void BuildProgressRow()
    {
        lblProgress.Text = "Sẵn sàng.";
        lblProgress.Font = AppTheme.Fonts.Hint;
        lblProgress.ForeColor = AppTheme.Colors.TextSecondary;
        pnlCard.Controls.Add(lblProgress);

        progressBar.Height = 10;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        pnlCard.Controls.Add(progressBar);

        LayoutProgressRow();
    }

    // Vị trí hàng tiến trình phụ thuộc bố cục nút (1 hàng hay 2×2) nên phải tính
    // lại mỗi lần pnlCard đổi kích thước — xem RelayoutSplit.
    private void LayoutProgressRow()
    {
        int top = _afterSettingsTop + ActionBlockHeight + 14;
        int width = Math.Max(1, pnlCard.ClientSize.Width - 64);

        lblProgress.SetBounds(32, top, width, 18);
        progressBar.SetBounds(32, top + 20, width, 10);
    }
    #endregion

    #region Split content: list + preview
    private const int SplitGap = 16;
    private int _splitTop;

    // pnlGrid/pnlPreview cố tình KHÔNG dùng Anchor để tự co giãn: UserControl
    // này được tạo (và BuildUi chạy) trước khi FormMain gắn nó vào Controls và
    // đặt Width/Height thật — tại thời điểm dựng UI, pnlCard vẫn còn kích thước
    // mặc định rất nhỏ của UserControl. Nếu để Anchor tự tính, WinForms sẽ chốt
    // baseline co giãn dựa trên kích thước sai đó (qua các Math.Max phòng vệ)
    // và không tự sửa lại đúng khi form thật được resize. Vì vậy toàn bộ vùng
    // split được tính lại từ đầu mỗi khi pnlCard đổi kích thước thật.
    private void BuildSplitContent()
    {
        pnlPreview.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlPreview);

        pnlGrid.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlGrid);

        BuildImageGrid();
        BuildPreviewPanel();

        pnlCard.SizeChanged += (_, _) => RelayoutSplit();
        RelayoutSplit();
    }

    // Sàn chiều cao khu vực danh sách/preview: soi biên cần zoom 200–400%, khung
    // xem cao vài chục px thì vô dụng. Cửa sổ thấp hơn thì pnlCard (AutoScroll)
    // cuộn, còn hơn bóp bẹp khung xem. Đặt dưới mức khả dụng của cửa sổ maximize
    // 1080p để trường hợp thường gặp không mọc thanh cuộn.
    private const int MinSplitHeight = 260;

    private void RelayoutSplit()
    {
        LayoutProgressRow();
        _splitTop = progressBar.Bottom + 20;

        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);
        int cardHeight = Math.Max(1, pnlCard.ClientSize.Height);
        int height = Math.Max(MinSplitHeight, cardHeight - _splitTop - 24);

        // Preview chiếm ~46% để có đủ chỗ soi biên khi zoom; luôn chừa cho danh
        // sách tối thiểu ~300px.
        int previewWidth = Math.Clamp((int)(cardWidth * 0.46), 440, 760);
        if (previewWidth > cardWidth - 300)
            previewWidth = Math.Max(200, cardWidth - 300);

        pnlPreview.SetBounds(cardWidth - 32 - previewWidth, _splitTop, previewWidth, height);

        int gridWidth = Math.Max(1, pnlPreview.Left - SplitGap - 32);
        pnlGrid.SetBounds(32, _splitTop, gridWidth, height);

        LayoutImageGrid();
        LayoutPreviewPanel();
    }

    private readonly Label lblListTitle = new();

    private void BuildImageGrid()
    {
        lblListTitle.Text = "DANH SÁCH ẢNH";
        lblListTitle.Left = 0;
        lblListTitle.Top = 0;
        lblListTitle.Font = AppTheme.Fonts.Overline;
        lblListTitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlGrid.Controls.Add(lblListTitle);

        dgvImages.Top = 24;
        dgvImages.Left = 0;
        dgvImages.AutoGenerateColumns = false;
        dgvImages.AllowUserToAddRows = false;
        dgvImages.AllowUserToDeleteRows = false;
        dgvImages.AllowUserToResizeRows = false;
        dgvImages.ReadOnly = true;
        dgvImages.MultiSelect = false;
        dgvImages.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        AppTheme.StyleGrid(dgvImages);
        dgvImages.RowTemplate.Height = 56;

        DataGridViewImageColumn colThumb = new()
        {
            HeaderText = "",
            Width = 60,
            ImageLayout = DataGridViewImageCellLayout.Zoom
        };
        // Mặc định DataGridViewImageColumn vẽ icon "no image" (dấu X đỏ) cho ô
        // Image có giá trị null — tắt đi vì thumbnail được nạp bất đồng bộ, ô sẽ
        // trống một nhịp thay vì hiện dấu X trước khi có ảnh thật.
        colThumb.DefaultCellStyle.NullValue = null;

        DataGridViewTextBoxColumn colName = new() { HeaderText = "Tên file", Width = 160, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
        DataGridViewTextBoxColumn colResolution = new() { HeaderText = "Resolution", Width = 110 };
        DataGridViewTextBoxColumn colStatus = new() { HeaderText = "Status", Width = 100 };

        dgvImages.Columns.AddRange(colThumb, colName, colResolution, colStatus);
        dgvImages.CellFormatting += DgvImages_CellFormatting;
        dgvImages.SelectionChanged += (_, _) => ShowPreviewForSelection();

        pnlGrid.Controls.Add(dgvImages);
    }

    private void LayoutImageGrid()
    {
        lblListTitle.Width = pnlGrid.ClientSize.Width;
        dgvImages.SetBounds(0, 24, pnlGrid.ClientSize.Width, Math.Max(1, pnlGrid.ClientSize.Height - 24));
    }

    private void DgvImages_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.ColumnIndex != 3 || e.RowIndex < 0 || e.RowIndex >= _items.Count)
            return;

        e.CellStyle!.ForeColor = _items[e.RowIndex].Status switch
        {
            ImageProcessStatus.Waiting => AppTheme.Colors.TextMuted,
            ImageProcessStatus.Processing => AppTheme.Colors.Primary,
            ImageProcessStatus.Done => AppTheme.Colors.Success,
            ImageProcessStatus.Error => AppTheme.Colors.Danger,
            _ => AppTheme.Colors.TextPrimary
        };
    }

    private void BuildPreviewPanel()
    {
        lblPreviewTitle.Text = "PREVIEW — KIỂM TRA MASK";
        lblPreviewTitle.Font = AppTheme.Fonts.Overline;
        lblPreviewTitle.ForeColor = AppTheme.Colors.TextMuted;
        lblPreviewTitle.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlPreview.Controls.Add(lblPreviewTitle);

        ConfigureModeButton(btnModeOriginal, "Ảnh gốc", PreviewMode.Original);
        ConfigureModeButton(btnModeMask, "Mask AI", PreviewMode.Mask);
        ConfigureModeButton(btnModeChecker, "Nền caro", PreviewMode.Checkerboard);
        ConfigureModeButton(btnModeResult, "Kết quả", PreviewMode.Result);

        ConfigureZoomButton(btnZoomFit, "Fit", () => zoomView.ZoomFit());
        ConfigureZoomButton(btnZoom100, "100%", () => zoomView.ZoomToPreset(1f));
        ConfigureZoomButton(btnZoom200, "200%", () => zoomView.ZoomToPreset(2f));
        ConfigureZoomButton(btnZoom400, "400%", () => zoomView.ZoomToPreset(4f));

        lblZoomValue.Font = AppTheme.Fonts.Hint;
        lblZoomValue.ForeColor = AppTheme.Colors.TextSecondary;
        lblZoomValue.BackColor = AppTheme.Colors.SurfaceElevated;
        lblZoomValue.TextAlign = ContentAlignment.MiddleRight;
        pnlPreview.Controls.Add(lblZoomValue);

        BuildBrushToolbar();

        zoomView.ViewChanged += (_, _) => UpdateZoomIndicator();
        zoomView.BrushStrokeStarted += (_, _) => _editSession?.BeginStroke();
        zoomView.BrushPainted += (_, e) =>
        {
            if (_editSession == null)
                return;
            if (_editSession.Paint(e.X, e.Y, e.Radius, e.Additive))
                RefreshAfterPaint();
        };
        zoomView.BrushStrokeEnded += (_, _) => UpdateBrushControls();
        pnlPreview.Controls.Add(zoomView);

        lblPreviewEmpty.Text = "Chọn một ảnh trong danh sách để xem.";
        lblPreviewEmpty.TextAlign = ContentAlignment.MiddleCenter;
        lblPreviewEmpty.Font = AppTheme.Fonts.Body;
        lblPreviewEmpty.ForeColor = AppTheme.Colors.TextMuted;
        lblPreviewEmpty.BackColor = AppTheme.Colors.Surface;
        lblPreviewEmpty.BorderStyle = BorderStyle.FixedSingle;
        pnlPreview.Controls.Add(lblPreviewEmpty);

        UpdateModeButtons();
        UpdateZoomIndicator();
        UpdateBrushControls();
        ApplyPreviewMode(resetView: true);
    }

    #region Brush toolbar
    private void BuildBrushToolbar()
    {
        ConfigureSmallButton(btnBrush, "Cọ sửa", ToggleBrush);
        ConfigureSmallButton(btnBrushKeep, "Giữ", () => SetBrushAdditive(true));
        ConfigureSmallButton(btnBrushErase, "Xóa", () => SetBrushAdditive(false));
        ConfigureSmallButton(btnBrushSmaller, "−", () => StepBrushSize(-1));
        ConfigureSmallButton(btnBrushBigger, "+", () => StepBrushSize(+1));
        ConfigureSmallButton(btnMaskUndo, "Hoàn tác", UndoMaskEdit);
        ConfigureSmallButton(btnMaskReset, "Đặt lại", ResetMaskEdit);
        ConfigureSmallButton(btnMaskApply, "Áp dụng", ApplyMaskEdit);

        lblBrushSize.Font = AppTheme.Fonts.Hint;
        lblBrushSize.ForeColor = AppTheme.Colors.TextSecondary;
        lblBrushSize.BackColor = AppTheme.Colors.SurfaceElevated;
        lblBrushSize.TextAlign = ContentAlignment.MiddleCenter;
        pnlPreview.Controls.Add(lblBrushSize);

        zoomView.BrushAdditive = false;   // mặc định TẨY: lỗi hay gặp là nền còn sót
        zoomView.BrushRadius = BrushSizes[_brushSizeIndex];
    }

    private void ConfigureSmallButton(RoundedButton btn, string text, Action onClick)
    {
        btn.Text = text;
        btn.Variant = ButtonVariant.Outline;
        btn.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btn.Font = AppTheme.Fonts.Hint;
        btn.CornerRadius = 9;
        btn.Click += (_, _) => onClick();
        pnlPreview.Controls.Add(btn);
    }

    private void ToggleBrush()
    {
        if (_editSession == null)
            return;

        bool on = !zoomView.BrushEnabled;
        zoomView.BrushEnabled = on;

        // Bật cọ thì chuyển sang nền caro — nền trắng không phân biệt được đâu là
        // nền sót, mà đó chính là thứ cần sửa.
        if (on && _previewMode is PreviewMode.Original or PreviewMode.Result)
        {
            _previewMode = PreviewMode.Checkerboard;
            UpdateModeButtons();
            ApplyPreviewMode(resetView: false);
        }

        UpdateBrushControls();
    }

    private void SetBrushAdditive(bool additive)
    {
        zoomView.BrushAdditive = additive;
        UpdateBrushControls();
    }

    private void StepBrushSize(int delta)
    {
        _brushSizeIndex = Math.Clamp(_brushSizeIndex + delta, 0, BrushSizes.Length - 1);
        zoomView.BrushRadius = BrushSizes[_brushSizeIndex];
        zoomView.Invalidate();
        UpdateBrushControls();
    }

    private void UndoMaskEdit()
    {
        if (_editSession?.Undo() != true)
            return;
        RefreshAfterPaint();
        UpdateBrushControls();
    }

    private void ResetMaskEdit()
    {
        if (_editSession == null)
            return;
        _editSession.Reset();
        RefreshAfterPaint();
        UpdateBrushControls();
    }

    // Ghi đè ảnh kết quả + mask cache bằng mask đã sửa. Không chạy lại AI: chỉ
    // composite lại foreground đã có với alpha mới.
    private void ApplyMaskEdit()
    {
        if (_editSession == null || _previewItem == null)
            return;

        BackgroundRemovalItem item = _previewItem;

        if (string.IsNullOrEmpty(item.ResultPath))
        {
            MessageBox.Show("Ảnh này chưa có file kết quả để ghi đè.", "Không áp dụng được");
            return;
        }

        // Mask cache bị thu nhỏ (ảnh rất lớn) thì ghi đè sẽ làm giảm độ phân giải
        // ảnh kết quả — chặn hẳn thay vì âm thầm làm hỏng file.
        if (item.Width > 0 && item.Height > 0
            && (_editSession.Width != item.Width || _editSession.Height != item.Height))
        {
            MessageBox.Show(
                $"Ảnh {item.Width}×{item.Height} lớn hơn giới hạn soi mask ({PreviewMaxSide}px), " +
                "mask xem được đã bị thu nhỏ nên không ghi đè để tránh giảm chất lượng ảnh kết quả.",
                "Không áp dụng được", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _editSession.SaveResult(item.ResultPath);

            if (!string.IsNullOrEmpty(item.MaskPreviewPath))
                _editSession.SaveMask(item.MaskPreviewPath);

            item.MaskEdited = true;
            RefreshRow(item);
            lblProgress.Text = $"Đã áp dụng mask sửa tay cho {item.FileName}.";
        }
        catch (Exception ex)
        {
            BackgroundRemovalDiagnosticsLog.Write(
                $"Ghi đè kết quả sau khi sửa mask thất bại ({item.FileName}): {ex.Message}");
            MessageBox.Show($"Không ghi được ảnh kết quả:\n{ex.Message}", "Lỗi");
        }

        UpdateBrushControls();
    }

    // Dựng lại layer + đẩy vào khung xem, GIỮ NGUYÊN zoom/pan (đang soi ở 400%
    // mà mỗi nét cọ lại nhảy về Fit thì không sửa được gì).
    private void RefreshAfterPaint()
    {
        InvalidateSessionLayers();
        ApplyPreviewMode(resetView: false);
    }

    private void UpdateBrushControls()
    {
        bool hasSession = _editSession != null;
        bool on = hasSession && zoomView.BrushEnabled;

        btnBrush.Enabled = hasSession;
        SetSegmentActive(btnBrush, on);

        btnBrushKeep.Enabled = on;
        btnBrushErase.Enabled = on;
        btnBrushSmaller.Enabled = on && _brushSizeIndex > 0;
        btnBrushBigger.Enabled = on && _brushSizeIndex < BrushSizes.Length - 1;
        SetSegmentActive(btnBrushKeep, on && zoomView.BrushAdditive);
        SetSegmentActive(btnBrushErase, on && !zoomView.BrushAdditive);

        lblBrushSize.Text = on ? $"{BrushSizes[_brushSizeIndex]:0}px" : "—";
        lblBrushSize.ForeColor = on ? AppTheme.Colors.TextSecondary : AppTheme.Colors.TextMuted;

        btnMaskUndo.Enabled = _editSession?.CanUndo == true;
        btnMaskReset.Enabled = _editSession?.IsDirty == true;
        btnMaskApply.Enabled = _editSession?.IsDirty == true;
        SetSegmentActive(btnMaskApply, _editSession?.IsDirty == true);
    }
    #endregion

    private void ConfigureModeButton(RoundedButton btn, string text, PreviewMode mode)
    {
        btn.Text = text;
        btn.Variant = ButtonVariant.Outline;
        btn.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btn.Font = AppTheme.Fonts.Hint;
        btn.CornerRadius = 9;
        btn.Click += (_, _) =>
        {
            if (_previewMode == mode)
                return;
            _previewMode = mode;
            UpdateModeButtons();
            // Giữ nguyên zoom/pan khi đổi chế độ — để so sánh cùng một vùng biên
            // giữa ảnh gốc / mask / nền caro / kết quả.
            ApplyPreviewMode(resetView: false);
        };
        pnlPreview.Controls.Add(btn);
    }

    private void ConfigureZoomButton(RoundedButton btn, string text, Action onClick)
    {
        btn.Text = text;
        btn.Variant = ButtonVariant.Outline;
        btn.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btn.Font = AppTheme.Fonts.Hint;
        btn.CornerRadius = 9;
        btn.Click += (_, _) => onClick();
        pnlPreview.Controls.Add(btn);
    }

    private void UpdateModeButtons()
    {
        SetSegmentActive(btnModeOriginal, _previewMode == PreviewMode.Original);
        SetSegmentActive(btnModeMask, _previewMode == PreviewMode.Mask);
        SetSegmentActive(btnModeChecker, _previewMode == PreviewMode.Checkerboard);
        SetSegmentActive(btnModeResult, _previewMode == PreviewMode.Result);
    }

    private void UpdateZoomIndicator()
    {
        float z = zoomView.DisplayZoom;
        bool fit = zoomView.IsFit;

        lblZoomValue.Text = fit ? $"Fit · {z * 100f:0}%" : $"{z * 100f:0}%";

        SetSegmentActive(btnZoomFit, fit);
        SetSegmentActive(btnZoom100, !fit && NearlyEqual(z, 1f));
        SetSegmentActive(btnZoom200, !fit && NearlyEqual(z, 2f));
        SetSegmentActive(btnZoom400, !fit && NearlyEqual(z, 4f));

        static bool NearlyEqual(float a, float b) => Math.Abs(a - b) < 0.005f;
    }

    private static void SetSegmentActive(RoundedButton btn, bool active)
        => btn.Variant = active ? ButtonVariant.Primary : ButtonVariant.Outline;

    private void LayoutPreviewPanel()
    {
        int w = Math.Max(1, pnlPreview.ClientSize.Width);
        int h = Math.Max(1, pnlPreview.ClientSize.Height);

        lblPreviewTitle.SetBounds(2, 0, Math.Max(1, w - 4), 16);

        const int gap = 6;
        int modeY = 22;
        const int modeH = 30;
        int mbW = Math.Max(1, (w - (gap * 3)) / 4);
        btnModeOriginal.SetBounds(0, modeY, mbW, modeH);
        btnModeMask.SetBounds(mbW + gap, modeY, mbW, modeH);
        btnModeChecker.SetBounds((mbW + gap) * 2, modeY, mbW, modeH);
        btnModeResult.SetBounds((mbW + gap) * 3, modeY, Math.Max(1, w - ((mbW + gap) * 3)), modeH);

        int zoomY = modeY + modeH + 8;
        const int zoomH = 26;
        const int zbW = 52;
        btnZoomFit.SetBounds(0, zoomY, zbW, zoomH);
        btnZoom100.SetBounds(zbW + gap, zoomY, zbW, zoomH);
        btnZoom200.SetBounds((zbW + gap) * 2, zoomY, zbW, zoomH);
        btnZoom400.SetBounds((zbW + gap) * 3, zoomY, zbW, zoomH);
        int zvX = ((zbW + gap) * 3) + zbW + gap;
        lblZoomValue.SetBounds(zvX, zoomY, Math.Max(1, w - zvX), zoomH);

        // Hàng cọ sửa mask. Màn hẹp thì bỏ bớt nhóm cỡ cọ (−/px/+) trước, vì
        // Giữ/Xóa và Áp dụng mới là thứ không thể thiếu.
        int brushY = zoomY + zoomH + 8;
        const int brushH = 26;
        int x = 0;

        int wBrush = 66, wMode = 44, wStep = 26, wSize = 42, wUndo = 66, wReset = 58, wApply = 64;
        int needFull = wBrush + wMode * 2 + wStep * 2 + wSize + wUndo + wReset + wApply + gap * 8;
        bool showSize = w >= needFull;

        btnBrush.SetBounds(x, brushY, wBrush, brushH); x += wBrush + gap;
        btnBrushKeep.SetBounds(x, brushY, wMode, brushH); x += wMode + gap;
        btnBrushErase.SetBounds(x, brushY, wMode, brushH); x += wMode + gap;

        btnBrushSmaller.Visible = showSize;
        lblBrushSize.Visible = showSize;
        btnBrushBigger.Visible = showSize;
        if (showSize)
        {
            btnBrushSmaller.SetBounds(x, brushY, wStep, brushH); x += wStep + gap;
            lblBrushSize.SetBounds(x, brushY, wSize, brushH); x += wSize + gap;
            btnBrushBigger.SetBounds(x, brushY, wStep, brushH); x += wStep + gap;
        }

        btnMaskUndo.SetBounds(x, brushY, wUndo, brushH); x += wUndo + gap;
        btnMaskReset.SetBounds(x, brushY, wReset, brushH); x += wReset + gap;
        btnMaskApply.SetBounds(x, brushY, Math.Max(wApply, w - x), brushH);

        int viewY = brushY + brushH + 10;
        int viewH = Math.Max(1, h - viewY);
        zoomView.SetBounds(0, viewY, w, viewH);
        lblPreviewEmpty.SetBounds(0, viewY, w, viewH);
    }

    // Nạp preview cho ảnh đang chọn ở luồng nền: ảnh gốc đọc từ file, còn mask /
    // nền caro / kết quả đều DỰNG TỪ MaskEditSession — nhờ vậy sau mỗi nét cọ chỉ
    // cần dựng lại 3 layer đó, không đụng đĩa và không chạy lại inference.
    private void ShowPreviewForSelection()
    {
        if (dgvImages.SelectedRows.Count == 0)
        {
            _previewItem = null;
            _previewToken = Guid.NewGuid();
            SetEditSession(null);
            SwapPreviewLayers(null);
            ApplyPreviewMode(resetView: true);
            return;
        }

        int index = dgvImages.SelectedRows[0].Index;
        if (index < 0 || index >= _items.Count)
            return;

        BackgroundRemovalItem item = _items[index];

        if (ReferenceEquals(item, _previewItem) && _editSession is { IsDirty: true })
            return;   // đang sửa dở đúng ảnh này — không nạp lại đè lên

        _previewItem = item;

        Guid token = Guid.NewGuid();
        _previewToken = token;

        lblPreviewEmpty.Text = "Đang tải preview…";
        lblPreviewEmpty.Visible = true;
        zoomView.Visible = false;

        string filePath = item.FilePath;
        string? maskPath = item.MaskPreviewPath;
        string? fgPath = item.ForegroundPreviewPath;

        Task.Run(() =>
        {
            Image? original = TryDecodeForPreview(filePath);
            MaskEditSession? session = null;

            if (!string.IsNullOrEmpty(maskPath) && !string.IsNullOrEmpty(fgPath)
                && File.Exists(maskPath) && File.Exists(fgPath))
            {
                try
                {
                    session = MaskEditSession.Load(fgPath, maskPath);
                }
                catch (Exception ex)
                {
                    BackgroundRemovalDiagnosticsLog.Write(
                        $"Không nạp được phiên sửa mask cho {Path.GetFileName(filePath)}: {ex.Message}");
                }
            }

            return (original, session);
        }).ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            (Image? original, MaskEditSession? session) = t.Result;

            if (token != _previewToken)
            {
                original?.Dispose();
                return;
            }

            SetEditSession(session);
            SwapPreviewLayers(new PreviewLayers { Original = original });
            InvalidateSessionLayers();
            ApplyPreviewMode(resetView: true);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // Invalidate 3 layer phụ thuộc mask (dispose + null hoá) — KHÔNG dựng lại
    // ngay. ApplyPreviewMode() dựng lại đúng layer đang hiển thị qua
    // EnsureLayerBuilt() khi cần. Trước đây dựng cả 3 layer (Mask/Cutout/
    // Result) mỗi lần dù chỉ 1 layer được hiển thị — đây là đường chạy dày
    // đặc nhất (mỗi nét cọ khi sửa mask), 2 layer còn lại chỉ tốn công vô ích.
    private void InvalidateSessionLayers()
    {
        if (_previewLayers == null)
            return;

        _previewLayers.Mask?.Dispose();
        _previewLayers.Cutout?.Dispose();
        _previewLayers.Result?.Dispose();
        _previewLayers.Mask = null;
        _previewLayers.Cutout = null;
        _previewLayers.Result = null;
    }

    // Dựng đúng layer cần cho preview mode nếu chưa có (mới invalidate hoặc
    // chưa từng xem qua) — cùng công thức, cùng dữ liệu nguồn (_editSession)
    // như bản dựng cả 3 layer trước đây, chỉ khác thời điểm tính.
    private void EnsureLayerBuilt(PreviewMode mode)
    {
        if (_previewLayers == null || _editSession == null)
            return;

        switch (mode)
        {
            case PreviewMode.Mask:
                if (_previewLayers.Mask == null)
                    using (SKBitmap mask = _editSession.RenderMask())
                        _previewLayers.Mask = SkiaToBitmap(mask);
                break;

            case PreviewMode.Checkerboard:
                if (_previewLayers.Cutout == null)
                    using (SKBitmap cutout = _editSession.RenderCutout())
                        _previewLayers.Cutout = SkiaToBitmap(cutout);
                break;

            case PreviewMode.Result:
                if (_previewLayers.Result == null)
                    using (SKBitmap white = _editSession.RenderOnWhite())
                        _previewLayers.Result = SkiaToBitmap(white);
                break;
        }
    }

    private void SetEditSession(MaskEditSession? session)
    {
        _editSession = session;

        if (session == null)
        {
            zoomView.BrushEnabled = false;
            btnBrush.Enabled = false;
        }

        UpdateBrushControls();
    }

    private static Image? TryDecodeForPreview(string path)
    {
        try
        {
            return DecodeForDisplay(path, PreviewMaxSide);
        }
        catch
        {
            return null;
        }
    }

    private void SwapPreviewLayers(PreviewLayers? next)
    {
        _previewLayers?.Dispose();
        _previewLayers = next;
    }

    private void ApplyPreviewMode(bool resetView)
    {
        EnsureLayerBuilt(_previewMode);

        Image? img = _previewMode switch
        {
            PreviewMode.Original => _previewLayers?.Original,
            PreviewMode.Mask => _previewLayers?.Mask,
            PreviewMode.Checkerboard => _previewLayers?.Cutout,
            PreviewMode.Result => _previewLayers?.Result,
            _ => null
        };

        bool hasItem = _previewItem != null;
        bool processed = _previewItem is { Status: ImageProcessStatus.Done };

        if (img != null)
        {
            zoomView.SetImage(img, _previewMode == PreviewMode.Checkerboard, resetView);
            zoomView.Visible = true;
            lblPreviewEmpty.Visible = false;
        }
        else
        {
            zoomView.SetImage(null, false, true);
            zoomView.Visible = false;
            lblPreviewEmpty.Visible = true;
            lblPreviewEmpty.Text = !hasItem
                ? "Chọn một ảnh trong danh sách để xem."
                : _previewMode switch
                {
                    PreviewMode.Original => "Không đọc được ảnh gốc.",
                    PreviewMode.Result => processed
                        ? "Không đọc được ảnh kết quả."
                        : "Chưa xử lý — bấm XÓA NỀN để tạo kết quả.",
                    _ => processed
                        ? "Không có dữ liệu mask cho ảnh này."
                        : "Chưa xử lý — bấm XÓA NỀN để xem mask.",
                };
        }

        UpdateZoomIndicator();
    }

    // Ghi 2 layer soi mask ra thư mục tạm theo item.Id, ngay sau khi xử lý xong:
    //   <id>_mask.png   — alpha CUỐI CÙNG (gray8) đã dùng để composite.
    //   <id>_cutout.png — foreground đã khử nhiễm biên + alpha cuối ở kênh A.
    // Ghi ra đĩa thay vì giữ SKBitmap trong RAM: batch vài trăm ảnh sẽ ngốn hàng
    // GB nếu giữ hết, còn Preview mỗi lúc chỉ cần layer của đúng 1 ảnh đang chọn.
    private void CachePreviewLayers(BackgroundRemovalItem item, BgRemovalInspection inspection)
    {
        try
        {
            Directory.CreateDirectory(_previewCacheDir);

            string maskPath = Path.Combine(_previewCacheDir, $"{item.Id:N}_mask.png");
            string fgPath = Path.Combine(_previewCacheDir, $"{item.Id:N}_fg.png");

            WritePng(inspection.FinalMask, maskPath);
            WritePng(inspection.Foreground, fgPath);

            item.MaskPreviewPath = maskPath;
            item.ForegroundPreviewPath = fgPath;
            item.MaskEdited = false;
        }
        catch (Exception ex)
        {
            // Không có layer soi mask thì Preview chỉ mất 2 chế độ Mask/Nền caro —
            // không được để hỏng cả batch vì lỗi ghi file tạm.
            item.MaskPreviewPath = null;
            item.ForegroundPreviewPath = null;
            BackgroundRemovalDiagnosticsLog.Write(
                $"Không ghi được layer preview cho {item.FileName}: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static void WritePng(SKBitmap bitmap, string path)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream fs = File.Create(path);
        data.SaveTo(fs);
    }

    private void ClearPreviewCache()
    {
        try
        {
            if (Directory.Exists(_previewCacheDir))
                Directory.Delete(_previewCacheDir, recursive: true);
        }
        catch (Exception)
        {
            // Thư mục tạm: file đang bị khóa thì bỏ qua, Windows dọn %TEMP% sau.
        }
    }
    #endregion

    #region Add / list management
    private void ChooseFiles()
    {
        using OpenFileDialog dialog = new()
        {
            Multiselect = true,
            Filter = "Ảnh (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
            AddPaths(dialog.FileNames);
    }

    private void ChooseFolder()
    {
        using FolderBrowserDialog dialog = new();

        if (dialog.ShowDialog() == DialogResult.OK)
            AddPaths(new[] { dialog.SelectedPath });
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        List<string> files = new();

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.GetFiles(path)
                    .Where(f => AllowedExtensions.Contains(Path.GetExtension(f))));
            }
            else if (File.Exists(path) && AllowedExtensions.Contains(Path.GetExtension(path)))
            {
                files.Add(path);
            }
        }

        HashSet<string> existing = new(_items.Select(i => i.FilePath), StringComparer.OrdinalIgnoreCase);
        List<BackgroundRemovalItem> added = new();

        // Chọn cả trăm/nghìn ảnh cùng lúc → thêm từng row một sẽ layout lại
        // lưới mỗi lần Add; SuspendLayout gộp lại thành 1 lần sau khi thêm hết,
        // hiển thị cuối cùng giống hệt, chỉ nhanh hơn.
        dgvImages.SuspendLayout();
        try
        {
            foreach (string file in files)
            {
                if (existing.Add(file))
                {
                    BackgroundRemovalItem item = new() { FilePath = file };
                    _items.Add(item);
                    added.Add(item);

                    dgvImages.Rows.Add(DBNull.Value, item.FileName, "…", item.StatusText);
                }
            }
        }
        finally
        {
            dgvImages.ResumeLayout();
        }

        UpdateProgressIdle();

        if (added.Count > 0)
            _ = EnrichItemsAsync(added);
    }

    // Đọc kích thước thật + tạo thumbnail trong nền để danh sách không bị đứng
    // hình khi người dùng chọn cả trăm/nghìn ảnh cùng lúc.
    private async Task EnrichItemsAsync(List<BackgroundRemovalItem> items)
    {
        foreach (BackgroundRemovalItem item in items)
        {
            try
            {
                (int width, int height, Image thumb) = await Task.Run(() =>
                {
                    using FileStream fs = File.OpenRead(item.FilePath);
                    using SKBitmap decoded = SKBitmap.Decode(fs)
                        ?? throw new InvalidDataException("Không đọc được ảnh.");

                    Image thumbnail = BuildThumbnail(decoded, 48);
                    return (decoded.Width, decoded.Height, thumbnail);
                });

                item.Width = width;
                item.Height = height;

                // await Task.Run ở trên đã quay lại đúng UI thread (SynchronizationContext
                // của WinForms) nên có thể cập nhật cell trực tiếp, không cần Invoke.
                int rowIndex = _items.IndexOf(item);
                if (rowIndex >= 0 && rowIndex < dgvImages.Rows.Count)
                {
                    dgvImages.Rows[rowIndex].Cells[0].Value = thumb;
                    dgvImages.Rows[rowIndex].Cells[2].Value = item.Resolution;
                }
            }
            catch (Exception ex)
            {
                item.Status = ImageProcessStatus.Error;
                item.ErrorMessage = ex.Message;
                RefreshRow(item);
            }
        }
    }

    private void ClearList()
    {
        if (_isRunning)
        {
            MessageBox.Show("Đang xử lý, vui lòng bấm DỪNG trước.", "Thông báo");
            return;
        }

        _items.Clear();
        dgvImages.Rows.Clear();

        // Hủy mọi lượt nạp preview đang chạy nền + trả Preview về trạng thái rỗng
        // trước khi xóa file cache (tránh nạp lại file vừa bị xóa).
        _previewItem = null;
        _previewToken = Guid.NewGuid();
        SetEditSession(null);
        SwapPreviewLayers(null);
        ApplyPreviewMode(resetView: true);
        ClearPreviewCache();

        UpdateProgressIdle();
    }

    // Mở đúng thư mục đang hiển thị ở ô "Thư mục lưu" trong Windows Explorer.
    // Chưa tồn tại thì tạo. Mọi lỗi đều hiện thông báo thân thiện, không làm sập app.
    private void OpenOutputFolder()
    {
        string folder = OutputFolder;

        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(
                "Chưa có thư mục lưu. Bấm nút \"Chọn\" để chọn thư mục output trước.",
                "Chưa chọn thư mục", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Chuẩn hoá + kiểm tra đường dẫn hợp lệ trước khi tạo/mở.
        try
        {
            folder = Path.GetFullPath(folder);
        }
        catch (Exception)
        {
            MessageBox.Show(
                $"Đường dẫn thư mục lưu không hợp lệ:\n{folder}\n\nHãy bấm \"Chọn\" để chọn lại thư mục.",
                "Đường dẫn không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không tạo được thư mục lưu:\n{folder}\n\n{ex.Message}",
                "Không mở được thư mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không mở được Windows Explorer tại:\n{folder}\n\n{ex.Message}",
                "Không mở được thư mục", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    #endregion

    #region Processing
    // Cộng dồn thời gian benchmark để hiển thị trung bình mỗi ảnh.
    private int _benchCount;
    private long _benchTotal, _benchInfer, _benchMatting, _benchRefine, _benchDecontam;

    private async void StartProcessing()
    {
        if (_isRunning)
            return;

        List<BackgroundRemovalItem> pending = _items.Where(i => i.Status == ImageProcessStatus.Waiting).ToList();

        if (pending.Count == 0)
        {
            MessageBox.Show("Không có ảnh nào đang chờ xử lý.", "Thông báo");
            return;
        }

        string outputFolder = OutputFolder;
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            MessageBox.Show("Vui lòng chọn thư mục lưu kết quả.", "Thông báo");
            return;
        }

        QualityMode mode = SelectedQualityMode;

        InferenceDevice device = cboDevice.SelectedIndex switch
        {
            1 => InferenceDevice.Cpu,
            2 => InferenceDevice.Gpu,
            _ => InferenceDevice.Auto
        };

        _cts = new CancellationTokenSource();
        _isRunning = true;
        SetRunningState(true);
        ResetBenchmark();

        int total = pending.Count;
        int done = 0;

        // 1) Tải model còn thiếu (lần đầu). Sau đó inference offline hoàn toàn.
        if (!await EnsureModelsAsync(mode))
        {
            _isRunning = false;
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
            return;
        }

        // 2) Nạp/xác nhận session TRƯỚC batch — người dùng thấy model + provider
        // thật ngay, kể cả khi batch bị Dừng trước khi xử lý xong ảnh nào.
        lblProvider.Text = "Model / Provider: đang nạp...";
        try
        {
            await Task.Run(() => _service.EnsureLoaded(mode, device), _cts.Token);
            lblProvider.Text = $"Model: {_service.ActiveSegmentationModelName} · Provider: {_service.ActiveProviderDescription}";
        }
        catch (OperationCanceledException)
        {
            lblProvider.Text = "Model / Provider: đã dừng trước khi nạp.";
            _isRunning = false;
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
            return;
        }
        catch (Exception ex)
        {
            lblProvider.Text = "Model / Provider: lỗi nạp model.";
            MessageBox.Show(ex.Message, "Không nạp được model");
            _isRunning = false;
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
            return;
        }

        try
        {
            foreach (BackgroundRemovalItem item in pending)
            {
                _cts.Token.ThrowIfCancellationRequested();

                item.Status = ImageProcessStatus.Processing;
                RefreshRow(item);
                lblProgress.Text = $"Đang xử lý {done + 1} / {total} — {item.FileName}";

                // inspect: true LUÔN — mask cuối + cutout là dữ liệu để Preview soi
                // biên, độc lập với toggle "Lưu mask debug" (toggle đó chỉ quyết
                // định có ghi thêm bộ mask ra <output>\debug\ hay không).
                BackgroundRemovalResult result = await _service.ProcessAsync(
                    item.FilePath,
                    outputFolder,
                    mode,
                    device,
                    chkDebug.Checked,
                    inspect: true,
                    _cts.Token);

                if (result.Success)
                {
                    item.Status = ImageProcessStatus.Done;
                    item.ResultPath = result.OutputPath;
                    AccumulateBenchmark(result.Timings);

                    if (result.Inspection != null)
                    {
                        using (result.Inspection)
                            CachePreviewLayers(item, result.Inspection);
                    }
                }
                else
                {
                    item.Status = ImageProcessStatus.Error;
                    item.ErrorMessage = result.ErrorMessage;
                }

                RefreshRow(item);

                done++;
                progressBar.Value = Math.Clamp((int)(done * 100.0 / total), 0, 100);
                lblProgress.Text = $"Đang xử lý {done} / {total}{BenchmarkSummary()}";

                if (dgvImages.SelectedRows.Count > 0 && _items.IndexOf(item) == dgvImages.SelectedRows[0].Index)
                    ShowPreviewForSelection();
            }

            lblProgress.Text = $"Hoàn tất {done} / {total}{BenchmarkSummary()}";
        }
        catch (OperationCanceledException)
        {
            lblProgress.Text = $"Đã dừng sau {done} / {total}{BenchmarkSummary()}";
        }
        finally
        {
            _isRunning = false;
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    // Trả về true nếu đã sẵn sàng chạy (không thiếu model hoặc đã tải xong).
    private async Task<bool> EnsureModelsAsync(QualityMode mode)
    {
        IReadOnlyList<BgModelInfo> missing = _service.DescribeMissingModels(mode);
        if (missing.Count == 0)
            return true;

        long totalMb = missing.Sum(m => m.SizeBytes) / 1024 / 1024;
        string list = string.Join("\n", missing.Select(m => $"  • {m.DisplayName} (~{m.ApproxDownloadSize})"));
        DialogResult confirm = MessageBox.Show(
            $"Lần đầu dùng chế độ này cần tải model AI về máy (chỉ tải một lần, " +
            $"sau đó chạy offline):\n\n{list}\n\nTổng ~{totalMb} MB. Tải ngay?",
            "Tải model AI",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes)
        {
            lblProgress.Text = "Đã hủy tải model.";
            return false;
        }

        progressBar.Value = 0;
        Progress<BgModelDownloadProgress> progress = new(p =>
        {
            progressBar.Value = Math.Clamp((int)(p.Fraction * 100), 0, 100);
            long got = p.BytesReceived / 1024 / 1024;
            long need = p.TotalBytes / 1024 / 1024;
            lblProgress.Text = $"Đang tải {p.ModelDisplayName}… {p.Fraction:P0} ({got}/{need} MB)";
        });

        try
        {
            await _service.EnsureModelsDownloadedAsync(mode, progress, _cts!.Token);
            progressBar.Value = 0;
            lblProgress.Text = "Đã tải xong model. Bắt đầu xử lý…";
            return true;
        }
        catch (OperationCanceledException)
        {
            lblProgress.Text = "Đã dừng khi đang tải model.";
            return false;
        }
        catch (Exception ex)
        {
            lblProgress.Text = "Lỗi tải model.";
            MessageBox.Show(ex.Message, "Không tải được model");
            return false;
        }
    }

    private void ResetBenchmark()
    {
        _benchCount = 0;
        _benchTotal = _benchInfer = _benchMatting = _benchRefine = _benchDecontam = 0;
    }

    private void AccumulateBenchmark(BgRemovalTimings? t)
    {
        if (t == null)
            return;
        _benchCount++;
        _benchTotal += t.TotalMs;
        _benchInfer += t.InferenceMs;
        _benchMatting += t.MattingMs;
        _benchRefine += t.RefineMs;
        _benchDecontam += t.DecontamMs;
    }

    private string BenchmarkSummary()
    {
        if (_benchCount == 0)
            return "";
        double n = _benchCount;
        string s = $" — TB {_benchTotal / n / 1000.0:0.0}s/ảnh (infer {_benchInfer / n / 1000.0:0.00}s";
        if (_benchMatting > 0)
            s += $" · matting {_benchMatting / n / 1000.0:0.00}s";
        if (_benchRefine > 0)
            s += $" · refine {_benchRefine / n / 1000.0:0.00}s";
        if (_benchDecontam > 0)
            s += $" · decontam {_benchDecontam / n / 1000.0:0.00}s";
        return s + ")";
    }

    private void SetRunningState(bool running)
    {
        btnStart.Enabled = !running;
        btnStop.Enabled = running;
        btnClearList.Enabled = !running;
        btnChooseFiles.Enabled = !running;
        btnChooseFolder.Enabled = !running;
        btnChooseOutput.Enabled = !running;
        cboDevice.Enabled = !running;
        cboQuality.Enabled = !running;
        chkDebug.Enabled = !running;
    }

    private void RefreshRow(BackgroundRemovalItem item)
    {
        int index = _items.IndexOf(item);
        if (index < 0 || index >= dgvImages.Rows.Count)
            return;

        dgvImages.Rows[index].Cells[3].Value = item.StatusText;
        dgvImages.InvalidateRow(index);
    }

    private void UpdateProgressIdle()
    {
        progressBar.Value = 0;
        int waiting = _items.Count(i => i.Status == ImageProcessStatus.Waiting);
        lblProgress.Text = _items.Count == 0
            ? "Sẵn sàng."
            : $"{_items.Count} ảnh trong danh sách — {waiting} đang chờ xử lý.";
    }
    #endregion

    #region Image helpers
    private static Image BuildThumbnail(SKBitmap decoded, int boxSize)
    {
        float scale = Math.Min((float)boxSize / decoded.Width, (float)boxSize / decoded.Height);
        int w = Math.Max(1, (int)Math.Round(decoded.Width * scale));
        int h = Math.Max(1, (int)Math.Round(decoded.Height * scale));

        SKImageInfo canvasInfo = new(boxSize, boxSize, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKBitmap canvas = new(canvasInfo);

        using (SKBitmap resized = decoded.Resize(
                   new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                   new SKSamplingOptions(SKCubicResampler.Mitchell))
               ?? throw new InvalidOperationException("Resize thumbnail thất bại."))
        using (SKCanvas c = new(canvas))
        {
            c.Clear(SKColors.White);
            c.DrawBitmap(resized, (boxSize - w) / 2f, (boxSize - h) / 2f);
        }

        return SkiaToBitmap(canvas);
    }

    private static Image DecodeForDisplay(string path, int maxSide)
    {
        using FileStream fs = File.OpenRead(path);
        using SKBitmap decoded = SKBitmap.Decode(fs)
            ?? throw new InvalidDataException("Không đọc được ảnh.");

        if (decoded.Width <= maxSide && decoded.Height <= maxSide)
            return SkiaToBitmap(decoded);

        float scale = Math.Min((float)maxSide / decoded.Width, (float)maxSide / decoded.Height);
        int w = Math.Max(1, (int)Math.Round(decoded.Width * scale));
        int h = Math.Max(1, (int)Math.Round(decoded.Height * scale));

        using SKBitmap resized = decoded.Resize(
                new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("Resize preview thất bại.");

        return SkiaToBitmap(resized);
    }

    // Bridge sang System.Drawing vì các control hiển thị của WinForms
    // (DataGridViewImageColumn, ZoomPanImageView) chỉ nhận kiểu này.
    //
    // Chép thẳng pixel, KHÔNG đi vòng qua PNG encode/decode: mỗi nét cọ phải dựng
    // lại 3 layer full-res, mã hoá PNG mỗi lần sẽ giật thấy rõ. Cũng bỏ luôn được
    // cái bẫy phải giữ MemoryStream sống suốt đời Image của đường PNG.
    private static Bitmap SkiaToBitmap(SKBitmap source)
    {
        int w = source.Width;
        int h = source.Height;

        Bitmap bmp = new(w, h, PixelFormat.Format32bppArgb);
        BitmapData dst = bmp.LockBits(
            new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

        try
        {
            byte[] src = source.Bytes;
            int srcStride = source.RowBytes;
            bool gray = source.ColorType == SKColorType.Gray8;
            byte[] row = new byte[dst.Stride];

            // "gray" không đổi trong suốt vòng lặp — tách hẳn 2 nhánh ra ngoài
            // thay vì kiểm tra lại cho từng pixel (w*h lần/ảnh), kết quả từng
            // byte giống hệt bản gộp chung trước đây.
            if (gray)
            {
                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * srcStride;
                    for (int x = 0; x < w; x++)
                    {
                        int di = x * 4;
                        byte g = src[srcRow + x];
                        row[di] = g;
                        row[di + 1] = g;
                        row[di + 2] = g;
                        row[di + 3] = 255;
                    }

                    Marshal.Copy(row, 0, IntPtr.Add(dst.Scan0, y * dst.Stride), dst.Stride);
                }
            }
            else
            {
                for (int y = 0; y < h; y++)
                {
                    int srcRow = y * srcStride;
                    for (int x = 0; x < w; x++)
                    {
                        // Skia RGBA -> GDI+ BGRA
                        int di = x * 4;
                        int si = srcRow + (x * 4);
                        row[di] = src[si + 2];
                        row[di + 1] = src[si + 1];
                        row[di + 2] = src[si];
                        row[di + 3] = src[si + 3];
                    }

                    Marshal.Copy(row, 0, IntPtr.Add(dst.Scan0, y * dst.Stride), dst.Stride);
                }
            }
        }
        finally
        {
            bmp.UnlockBits(dst);
        }

        return bmp;
    }
    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _editSession = null;
            SwapPreviewLayers(null);
            ClearPreviewCache();
            // Giải phóng các InferenceSession ONNX (BiRefNet-lite ~224 MB,
            // matting ~941 MB) — service sống suốt vòng đời màn hình này.
            _service.Dispose();
        }

        base.Dispose(disposing);
    }
}
