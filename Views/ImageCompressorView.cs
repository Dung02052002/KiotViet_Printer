using System.Diagnostics;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Views;

// Màn hình "Giảm dung lượng ảnh" — nhúng vào FormMain giống cơ chế show/hide
// đang dùng cho BackgroundRemoverView (xem FormMain.ShowHome/OpenImageCompressor).
// UI chỉ gọi ImageCompressionService, không chứa logic decode/resize/encode.
public class ImageCompressorView : UserControl
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private const int PreviewBoxSize = 220;
    private const int ThumbnailBoxSize = 48;

    private readonly ImageCompressionService _service = new();
    private readonly List<CompressionItem> _items = new();

    private CancellationTokenSource? _cts;
    private bool _isRunning;

    private CompressionItem? _previewItem;
    private Guid _previewToken;

    private readonly RoundedPanel pnlCard = new();

    // Header
    private readonly RoundedPanel pnlIconBadge = new();
    private readonly IconGlyph iconHeader = new();
    private readonly Label lblHeaderTitle = new();
    private readonly Label lblHeaderSubtitle = new();

    // Drop zone
    private readonly RoundedPanel pnlDropZone = new();
    private readonly Label lblDropHint = new();
    private readonly Label lblDropSub = new();
    private readonly RoundedButton btnChooseFiles = new();
    private readonly RoundedButton btnChooseFolder = new();
    private readonly ToggleSwitch chkIncludeSubfolders = new();

    // Quality presets (3 "chip" — tự vẽ bằng RoundedPanel, không phải combobox,
    // để giữ đúng cách trình bày Tên + % + cạnh tối đa như file HTML tham khảo).
    private sealed class PresetChip
    {
        public required RoundedPanel Panel { get; init; }
        public required Label NameLabel { get; init; }
        public required Label SpecLabel { get; init; }
        public required CompressionQuality Id { get; init; }
    }

    private readonly Label lblQualityLabel = new();
    private readonly List<PresetChip> _presetChips = new();
    private CompressionQuality _selectedQuality = CompressionQuality.Balanced;

    // Options: định dạng output + giữ nguyên kích thước
    private readonly Label lblFormatLabel = new();
    private readonly ComboBox cboFormat = new();
    private readonly ToggleSwitch chkKeepDims = new();

    // Thư mục lưu
    private readonly RoundedTextBox txtOutputFolder = new();
    private readonly RoundedButton btnChooseOutput = new();

    // Action buttons
    private readonly RoundedButton btnStart = new();
    private readonly RoundedButton btnStop = new();
    private readonly RoundedButton btnClearList = new();
    private readonly RoundedButton btnOpenOutput = new();

    private const int WStart = 190, WStop = 120, WClear = 178, WOpen = 210;
    private int ActionBlockHeight => ActionsFitOneRow() ? 44 : 100;

    // Progress
    private readonly ProgressBar progressBar = new();
    private readonly Label lblProgress = new();

    // Tổng dung lượng
    private readonly RoundedPanel pnlTotals = new();
    private readonly Label lblTotalReadout = new();
    private readonly Label lblTotalSaved = new();
    private readonly Label lblTotalMeta = new();

    // Danh sách + preview
    private readonly Panel pnlGrid = new();
    private readonly Label lblListTitle = new();
    private readonly SmoothDataGridView dgvImages = new();

    private readonly Panel pnlPreview = new();
    private readonly Label lblPreviewTitle = new();
    private readonly Label lblOrigTag = new();
    private readonly PictureBox picOriginal = new();
    private readonly Label lblOrigStats = new();
    private readonly Label lblSavedBig = new();
    private readonly Label lblCompTag = new();
    private readonly PictureBox picCompressed = new();
    private readonly Label lblCompStats = new();
    private readonly Label lblPreviewEmpty = new();

    private int _afterOutputRowTop;

    public ImageCompressorView()
    {
        Dock = DockStyle.Fill;
        BuildUi();
        WireDragDrop();

        txtOutputFolder.Text = Path.Combine(AppContext.BaseDirectory, "Output", "CompressedImages");

        UpdateQualityChipVisuals();
        UpdateProgressIdle();
        UpdateTotals();
        ShowEmptyPreview("Chọn một ảnh trong danh sách để xem.");
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
        BuildQualitySection();
        BuildOptionsRow();
        BuildOutputRow();
        BuildActionButtons();
        BuildProgressRow();
        BuildTotalsPanel();
        BuildSplitContent();

        pnlCard.SizeChanged += (_, _) => RelayoutAll();
        RelayoutAll();
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
        iconHeader.Kind = IconGlyphs.Kind.Compress;
        iconHeader.IconColor = AppTheme.Colors.Primary;
        iconHeader.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.Controls.Add(iconHeader);

        lblHeaderTitle.Text = "GIẢM DUNG LƯỢNG ẢNH";
        lblHeaderTitle.Left = 88;
        lblHeaderTitle.Top = 32;
        lblHeaderTitle.Width = 500;
        lblHeaderTitle.Height = 26;
        lblHeaderTitle.Font = AppTheme.Fonts.SectionTitle;
        lblHeaderTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblHeaderTitle);

        lblHeaderSubtitle.Text = "Nén và tối ưu dung lượng ảnh trực tiếp trên máy.";
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
        pnlDropZone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlDropZone.CornerRadius = 16;
        pnlDropZone.FillColor = AppTheme.Colors.PrimaryLight;
        pnlDropZone.BorderColor = AppTheme.Colors.BorderOutlineRest;
        pnlDropZone.BorderThickness = 1;
        pnlDropZone.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlDropZone);

        lblDropHint.Text = "KÉO ẢNH VÀO ĐÂY";
        lblDropHint.TextAlign = ContentAlignment.MiddleCenter;
        lblDropHint.Font = AppTheme.Fonts.BodyBold;
        lblDropHint.ForeColor = AppTheme.Colors.TextSecondary;
        pnlDropZone.Controls.Add(lblDropHint);

        lblDropSub.Text = "JPG · PNG · WEBP";
        lblDropSub.TextAlign = ContentAlignment.MiddleCenter;
        lblDropSub.Font = AppTheme.Fonts.Hint;
        lblDropSub.ForeColor = AppTheme.Colors.TextMuted;
        pnlDropZone.Controls.Add(lblDropSub);

        btnChooseFiles.Text = "Chọn ảnh";
        btnChooseFiles.Icon = IconGlyphs.Kind.Image;
        btnChooseFiles.Size = new Size(150, 40);
        btnChooseFiles.Variant = ButtonVariant.Outline;
        btnChooseFiles.ContainerColor = AppTheme.Colors.PrimaryLight;
        btnChooseFiles.Click += (_, _) => ChooseFiles();
        pnlDropZone.Controls.Add(btnChooseFiles);

        btnChooseFolder.Text = "Chọn thư mục";
        btnChooseFolder.Icon = IconGlyphs.Kind.Folder;
        btnChooseFolder.Size = new Size(160, 40);
        btnChooseFolder.Variant = ButtonVariant.Outline;
        btnChooseFolder.ContainerColor = AppTheme.Colors.PrimaryLight;
        btnChooseFolder.Click += (_, _) => ChooseFolder();
        pnlDropZone.Controls.Add(btnChooseFolder);

        chkIncludeSubfolders.Text = "Bao gồm thư mục con";
        chkIncludeSubfolders.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlDropZone.Controls.Add(chkIncludeSubfolders);

        pnlDropZone.SizeChanged += (_, _) => LayoutDropZone();
    }

    private void LayoutDropZone()
    {
        pnlDropZone.Left = 32;
        pnlDropZone.Top = 116;
        pnlDropZone.Width = Math.Max(0, pnlCard.ClientSize.Width - 64);
        pnlDropZone.Height = 168;

        int w = pnlDropZone.Width;

        lblDropHint.SetBounds(0, 16, w, 24);
        lblDropSub.SetBounds(0, 42, w, 18);

        int gap = 12;
        int totalWidth = btnChooseFiles.Width + gap + btnChooseFolder.Width;
        int startX = Math.Max(0, (w - totalWidth) / 2);
        btnChooseFiles.SetBounds(startX, 70, btnChooseFiles.Width, 40);
        btnChooseFolder.SetBounds(startX + btnChooseFiles.Width + gap, 70, btnChooseFolder.Width, 40);

        chkIncludeSubfolders.Size = new Size(220, 24);
        chkIncludeSubfolders.Location = new Point(Math.Max(0, (w - chkIncludeSubfolders.Width) / 2), 122);
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

    #region Quality presets
    private void BuildQualitySection()
    {
        lblQualityLabel.Text = "CHẤT LƯỢNG";
        lblQualityLabel.Font = AppTheme.Fonts.Overline;
        lblQualityLabel.ForeColor = AppTheme.Colors.TextMuted;
        pnlCard.Controls.Add(lblQualityLabel);

        foreach (CompressionPreset preset in CompressionPreset.All)
        {
            RoundedPanel chip = new()
            {
                CornerRadius = 14,
                BorderThickness = 1,
                ContainerColor = AppTheme.Colors.SurfaceElevated,
                Cursor = Cursors.Hand
            };

            Label lblName = new()
            {
                Text = preset.Name,
                Font = AppTheme.Fonts.BodyBold,
                Cursor = Cursors.Hand
            };

            Label lblSpec = new()
            {
                Text = preset.Spec,
                Font = AppTheme.Fonts.Hint,
                Cursor = Cursors.Hand
            };

            chip.Controls.Add(lblName);
            chip.Controls.Add(lblSpec);

            PresetChip entry = new() { Panel = chip, NameLabel = lblName, SpecLabel = lblSpec, Id = preset.Id };
            _presetChips.Add(entry);

            EventHandler onClick = (_, _) => SelectQuality(entry.Id);
            chip.Click += onClick;
            lblName.Click += onClick;
            lblSpec.Click += onClick;

            pnlCard.Controls.Add(chip);
        }
    }

    private void SelectQuality(CompressionQuality id)
    {
        if (_isRunning)
            return;

        _selectedQuality = id;
        UpdateQualityChipVisuals();
    }

    private void UpdateQualityChipVisuals()
    {
        foreach (PresetChip chip in _presetChips)
        {
            bool selected = chip.Id == _selectedQuality;
            chip.Panel.FillColor = selected ? AppTheme.Colors.Primary : AppTheme.Colors.SurfaceElevated;
            chip.Panel.BorderColor = selected ? AppTheme.Colors.Primary : AppTheme.Colors.BorderOutlineRest;
            chip.NameLabel.ForeColor = selected ? Color.White : AppTheme.Colors.TextPrimary;
            chip.SpecLabel.ForeColor = selected ? Color.FromArgb(225, 235, 255) : AppTheme.Colors.TextSecondary;
            chip.Panel.Invalidate(true);
        }
    }

    private void LayoutQualitySection(int top)
    {
        lblQualityLabel.SetBounds(32, top, 300, 16);

        int rowTop = top + 24;
        int left = 32;
        int gap = 12;
        int totalWidth = Math.Max(1, pnlCard.ClientSize.Width - 64);
        int chipWidth = Math.Max(120, (totalWidth - (gap * (_presetChips.Count - 1))) / _presetChips.Count);
        const int chipHeight = 60;

        int x = left;
        foreach (PresetChip chip in _presetChips)
        {
            chip.Panel.SetBounds(x, rowTop, chipWidth, chipHeight);
            chip.NameLabel.SetBounds(14, 9, chipWidth - 28, 20);
            chip.SpecLabel.SetBounds(14, 31, chipWidth - 28, 18);
            x += chipWidth + gap;
        }

        _afterQualityTop = rowTop + chipHeight;
    }

    private int _afterQualityTop;
    #endregion

    #region Options row (định dạng + giữ nguyên kích thước)
    private void BuildOptionsRow()
    {
        lblFormatLabel.Text = "Định dạng đầu ra";
        lblFormatLabel.Font = AppTheme.Fonts.Body;
        lblFormatLabel.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblFormatLabel);

        cboFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        cboFormat.Items.AddRange(new object[]
        {
            "JPEG — hoạt động mọi nơi",
            "WebP — nhỏ hơn, giữ được vùng trong suốt"
        });
        cboFormat.SelectedIndex = 0;
        AppTheme.StyleComboBox(cboFormat);
        pnlCard.Controls.Add(cboFormat);

        chkKeepDims.Text = "Giữ nguyên kích thước ảnh";
        chkKeepDims.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(chkKeepDims);
    }

    private void LayoutOptionsRow(int top)
    {
        lblFormatLabel.SetBounds(32, top + 10, 140, 20);
        cboFormat.SetBounds(178, top, 300, 32);

        chkKeepDims.Size = new Size(260, 28);
        chkKeepDims.Location = new Point(510, top + 2);

        _afterOptionsTop = top + 40;
    }

    private int _afterOptionsTop;
    #endregion

    #region Output folder row
    private void BuildOutputRow()
    {
        Label lblOutput = new()
        {
            Text = "Thư mục lưu",
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary,
            Width = 100
        };
        pnlCard.Controls.Add(lblOutput);
        _lblOutput = lblOutput;

        btnChooseOutput.Text = "Chọn";
        btnChooseOutput.Icon = IconGlyphs.Kind.Folder;
        btnChooseOutput.Width = 110;
        btnChooseOutput.Height = 40;
        btnChooseOutput.Variant = ButtonVariant.Outline;
        btnChooseOutput.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnChooseOutput.Click += (_, _) => ChooseOutputFolder();
        pnlCard.Controls.Add(btnChooseOutput);

        txtOutputFolder.Height = 40;
        txtOutputFolder.ContainerColor = AppTheme.Colors.SurfaceElevated;
        txtOutputFolder.ReadOnly = true;
        pnlCard.Controls.Add(txtOutputFolder);
    }

    private Label _lblOutput = new();

    private void LayoutOutputRow(int top)
    {
        _lblOutput.SetBounds(32, top + 10, 100, 20);

        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);
        btnChooseOutput.Left = Math.Max(138, cardWidth - 32 - btnChooseOutput.Width);
        btnChooseOutput.Top = top;

        txtOutputFolder.Left = 138;
        txtOutputFolder.Top = top;
        txtOutputFolder.Width = Math.Max(1, btnChooseOutput.Left - 12 - txtOutputFolder.Left);

        _afterOutputRowTop = top + 52;
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
    private void BuildActionButtons()
    {
        btnStart.Text = "GIẢM DUNG LƯỢNG";
        btnStart.Icon = IconGlyphs.Kind.Compress;
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
    }

    private bool ActionsFitOneRow()
    {
        int avail = Math.Max(1, pnlCard.ClientSize.Width - 32 - 32);
        return avail >= WStart + WStop + WClear + WOpen + (12 * 3);
    }

    private void LayoutActionButtons(int top)
    {
        const int left = 32;
        const int gap = 12;
        const int rowH = 44;

        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);
        int avail = Math.Max(1, cardWidth - left - 32);

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

        _afterActionsTop = top + ActionBlockHeight;
    }

    private int _afterActionsTop;

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
    }

    private void LayoutProgressRow(int top)
    {
        int width = Math.Max(1, pnlCard.ClientSize.Width - 64);

        lblProgress.SetBounds(32, top, width, 18);
        progressBar.SetBounds(32, top + 20, width, 10);

        _afterProgressTop = top + 34;
    }

    private int _afterProgressTop;
    #endregion

    #region Totals panel
    private void BuildTotalsPanel()
    {
        pnlTotals.CornerRadius = 14;
        pnlTotals.FillColor = AppTheme.Colors.TextPrimary;
        pnlTotals.BorderThickness = 0;
        pnlTotals.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlTotals);

        lblTotalReadout.Font = AppTheme.Fonts.SectionTitle;
        lblTotalReadout.ForeColor = Color.White;
        lblTotalReadout.TextAlign = ContentAlignment.MiddleLeft;
        pnlTotals.Controls.Add(lblTotalReadout);

        lblTotalSaved.Font = AppTheme.Fonts.Title;
        lblTotalSaved.ForeColor = Color.FromArgb(255, 209, 102);
        lblTotalSaved.TextAlign = ContentAlignment.MiddleRight;
        pnlTotals.Controls.Add(lblTotalSaved);

        lblTotalMeta.Font = AppTheme.Fonts.Hint;
        lblTotalMeta.ForeColor = Color.FromArgb(190, 197, 212);
        lblTotalMeta.TextAlign = ContentAlignment.MiddleRight;
        pnlTotals.Controls.Add(lblTotalMeta);
    }

    private void LayoutTotalsPanel(int top)
    {
        int width = Math.Max(1, pnlCard.ClientSize.Width - 64);
        pnlTotals.SetBounds(32, top, width, 60);

        int panelWidth = pnlTotals.ClientSize.Width;
        lblTotalReadout.SetBounds(18, 8, Math.Max(1, panelWidth - 240), 26);

        lblTotalSaved.SetBounds(panelWidth - 200, 6, 180, 28);
        lblTotalMeta.SetBounds(panelWidth - 260, 34, 240, 18);

        _afterTotalsTop = top + 60;
    }

    private int _afterTotalsTop;
    #endregion

    #region Split content: list + preview
    private const int SplitGap = 16;
    private const int MinSplitHeight = 220;

    private void BuildSplitContent()
    {
        pnlPreview.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlPreview);

        pnlGrid.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlGrid);

        BuildImageGrid();
        BuildPreviewPanel();
    }

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
        dgvImages.RowTemplate.Height = 52;

        DataGridViewImageColumn colThumb = new()
        {
            HeaderText = "",
            Width = 56,
            ImageLayout = DataGridViewImageCellLayout.Zoom
        };
        colThumb.DefaultCellStyle.NullValue = null;

        DataGridViewTextBoxColumn colName = new() { HeaderText = "Tên file", Width = 140, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill };
        DataGridViewTextBoxColumn colOriginal = new() { HeaderText = "Gốc", Width = 80 };
        DataGridViewTextBoxColumn colOutput = new() { HeaderText = "Sau nén", Width = 80 };
        DataGridViewTextBoxColumn colDims = new() { HeaderText = "Kích thước", Width = 110 };
        DataGridViewTextBoxColumn colSaved = new() { HeaderText = "Tiết kiệm", Width = 80 };
        DataGridViewTextBoxColumn colStatus = new() { HeaderText = "Trạng thái", Width = 100 };

        dgvImages.Columns.AddRange(colThumb, colName, colOriginal, colOutput, colDims, colSaved, colStatus);
        dgvImages.CellFormatting += DgvImages_CellFormatting;
        dgvImages.SelectionChanged += (_, _) => ShowPreviewForSelection();

        pnlGrid.Controls.Add(dgvImages);
    }

    private void DgvImages_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.ColumnIndex != 6 || e.RowIndex < 0 || e.RowIndex >= _items.Count)
            return;

        e.CellStyle!.ForeColor = _items[e.RowIndex].Status switch
        {
            CompressionStatus.Waiting => AppTheme.Colors.TextMuted,
            CompressionStatus.Compressing => AppTheme.Colors.Primary,
            CompressionStatus.Done => AppTheme.Colors.Success,
            CompressionStatus.OriginalKept => AppTheme.Colors.TextSecondary,
            CompressionStatus.Error => AppTheme.Colors.Danger,
            CompressionStatus.Cancelled => AppTheme.Colors.TextMuted,
            _ => AppTheme.Colors.TextPrimary
        };
    }

    private void BuildPreviewPanel()
    {
        lblPreviewTitle.Text = "PREVIEW";
        lblPreviewTitle.Font = AppTheme.Fonts.Overline;
        lblPreviewTitle.ForeColor = AppTheme.Colors.TextMuted;
        lblPreviewTitle.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlPreview.Controls.Add(lblPreviewTitle);

        lblOrigTag.Text = "ẢNH GỐC";
        lblOrigTag.Font = AppTheme.Fonts.BodyBold;
        lblOrigTag.ForeColor = AppTheme.Colors.TextSecondary;
        lblOrigTag.BackColor = AppTheme.Colors.SurfaceElevated;
        lblOrigTag.TextAlign = ContentAlignment.MiddleCenter;
        pnlPreview.Controls.Add(lblOrigTag);

        picOriginal.BorderStyle = BorderStyle.FixedSingle;
        picOriginal.BackColor = AppTheme.Colors.Surface;
        picOriginal.SizeMode = PictureBoxSizeMode.Zoom;
        pnlPreview.Controls.Add(picOriginal);

        lblOrigStats.Font = AppTheme.Fonts.Hint;
        lblOrigStats.ForeColor = AppTheme.Colors.TextSecondary;
        lblOrigStats.BackColor = AppTheme.Colors.SurfaceElevated;
        lblOrigStats.TextAlign = ContentAlignment.MiddleCenter;
        pnlPreview.Controls.Add(lblOrigStats);

        lblSavedBig.Font = AppTheme.Fonts.Title;
        lblSavedBig.ForeColor = AppTheme.Colors.Primary;
        lblSavedBig.BackColor = AppTheme.Colors.SurfaceElevated;
        lblSavedBig.TextAlign = ContentAlignment.MiddleCenter;
        pnlPreview.Controls.Add(lblSavedBig);

        lblCompTag.Text = "SAU NÉN";
        lblCompTag.Font = AppTheme.Fonts.BodyBold;
        lblCompTag.ForeColor = AppTheme.Colors.TextSecondary;
        lblCompTag.BackColor = AppTheme.Colors.SurfaceElevated;
        lblCompTag.TextAlign = ContentAlignment.MiddleCenter;
        pnlPreview.Controls.Add(lblCompTag);

        picCompressed.BorderStyle = BorderStyle.FixedSingle;
        picCompressed.BackColor = AppTheme.Colors.Surface;
        picCompressed.SizeMode = PictureBoxSizeMode.Zoom;
        pnlPreview.Controls.Add(picCompressed);

        lblCompStats.Font = AppTheme.Fonts.Hint;
        lblCompStats.ForeColor = AppTheme.Colors.TextSecondary;
        lblCompStats.BackColor = AppTheme.Colors.SurfaceElevated;
        lblCompStats.TextAlign = ContentAlignment.MiddleCenter;
        pnlPreview.Controls.Add(lblCompStats);

        lblPreviewEmpty.TextAlign = ContentAlignment.MiddleCenter;
        lblPreviewEmpty.Font = AppTheme.Fonts.Body;
        lblPreviewEmpty.ForeColor = AppTheme.Colors.TextMuted;
        lblPreviewEmpty.BackColor = AppTheme.Colors.Surface;
        lblPreviewEmpty.BorderStyle = BorderStyle.FixedSingle;
        pnlPreview.Controls.Add(lblPreviewEmpty);
    }

    private void RelayoutAll()
    {
        LayoutDropZone();

        int top = pnlDropZone.Bottom + 16;
        LayoutQualitySection(top);

        top = _afterQualityTop + 16;
        LayoutOptionsRow(top);

        LayoutOutputRow(_afterOptionsTop + 8);

        LayoutActionButtons(_afterOutputRowTop + 8);
        LayoutProgressRow(_afterActionsTop + 14);
        LayoutTotalsPanel(_afterProgressTop + 14);

        RelayoutSplit();
    }

    private void RelayoutSplit()
    {
        int splitTop = _afterTotalsTop + 16;

        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);
        int cardHeight = Math.Max(1, pnlCard.ClientSize.Height);
        int height = Math.Max(MinSplitHeight, cardHeight - splitTop - 24);

        int previewWidth = Math.Clamp((int)(cardWidth * 0.3), 260, 340);
        if (previewWidth > cardWidth - 300)
            previewWidth = Math.Max(180, cardWidth - 300);

        pnlPreview.SetBounds(cardWidth - 32 - previewWidth, splitTop, previewWidth, height);

        int gridWidth = Math.Max(1, pnlPreview.Left - SplitGap - 32);
        pnlGrid.SetBounds(32, splitTop, gridWidth, height);

        LayoutImageGrid();
        LayoutPreviewPanel();
    }

    private void LayoutImageGrid()
    {
        lblListTitle.Width = pnlGrid.ClientSize.Width;
        dgvImages.SetBounds(0, 24, pnlGrid.ClientSize.Width, Math.Max(1, pnlGrid.ClientSize.Height - 24));
    }

    private void LayoutPreviewPanel()
    {
        int w = Math.Max(1, pnlPreview.ClientSize.Width);

        lblPreviewTitle.SetBounds(2, 0, Math.Max(1, w - 4), 16);

        int boxSize = Math.Max(80, Math.Min(w - 4, PreviewBoxSize));
        int boxLeft = Math.Max(0, (w - boxSize) / 2);

        int y = 22;
        lblOrigTag.SetBounds(0, y, w, 16); y += 18;
        picOriginal.SetBounds(boxLeft, y, boxSize, boxSize); y += boxSize + 6;
        lblOrigStats.SetBounds(0, y, w, 16); y += 26;

        lblSavedBig.SetBounds(0, y, w, 30); y += 34;

        lblCompTag.SetBounds(0, y, w, 16); y += 18;
        picCompressed.SetBounds(boxLeft, y, boxSize, boxSize); y += boxSize + 6;
        lblCompStats.SetBounds(0, y, w, 16);

        lblPreviewEmpty.SetBounds(0, 22, w, Math.Max(1, pnlPreview.ClientSize.Height - 22));
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
        bool recursive = chkIncludeSubfolders.Checked;
        List<string> files = new();

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                SearchOption opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                try
                {
                    files.AddRange(Directory.EnumerateFiles(path, "*", opt)
                        .Where(f => AllowedExtensions.Contains(Path.GetExtension(f))));
                }
                catch (Exception)
                {
                    // Thư mục không đọc được (quyền truy cập, ổ rời...) — bỏ qua,
                    // không chặn phần còn lại của batch add.
                }
            }
            else if (File.Exists(path) && AllowedExtensions.Contains(Path.GetExtension(path)))
            {
                files.Add(path);
            }
        }

        HashSet<string> existing = new(_items.Select(i => i.FilePath), StringComparer.OrdinalIgnoreCase);
        List<CompressionItem> added = new();

        foreach (string file in files)
        {
            if (existing.Add(file))
            {
                CompressionItem item = new() { FilePath = file };
                _items.Add(item);
                added.Add(item);

                dgvImages.Rows.Add(DBNull.Value, item.FileName, "…", "", "…", "", item.StatusText);
            }
        }

        UpdateProgressIdle();
        UpdateTotals();

        if (added.Count > 0)
            _ = EnrichItemsAsync(added);
    }

    // Đọc dung lượng + kích thước thật + tạo thumbnail trong nền để danh sách
    // không đứng hình khi người dùng thêm hàng trăm/nghìn ảnh cùng lúc.
    private async Task EnrichItemsAsync(List<CompressionItem> items)
    {
        foreach (CompressionItem item in items)
        {
            try
            {
                (long bytes, Bitmap? thumb, int width, int height) = await Task.Run(() =>
                {
                    long size = new FileInfo(item.FilePath).Length;
                    Bitmap? bmp = ImageCompressionService.TryDecodeThumbnail(item.FilePath, ThumbnailBoxSize, out int w, out int h);
                    return (size, bmp, w, h);
                });

                item.OriginalBytes = bytes;
                item.OriginalWidth = width;
                item.OriginalHeight = height;

                int rowIndex = _items.IndexOf(item);
                if (rowIndex >= 0 && rowIndex < dgvImages.Rows.Count)
                {
                    dgvImages.Rows[rowIndex].Cells[0].Value = (object?)thumb ?? DBNull.Value;
                    dgvImages.Rows[rowIndex].Cells[2].Value = FormatBytes(bytes);
                    dgvImages.Rows[rowIndex].Cells[4].Value = item.OriginalResolution;
                }
            }
            catch (Exception ex)
            {
                item.Status = CompressionStatus.Error;
                item.ErrorMessage = ex.Message;
                RefreshRow(item);
            }
        }

        UpdateTotals();
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

        _previewItem = null;
        _previewToken = Guid.NewGuid();
        SetPreviewImages(null, null);
        ShowEmptyPreview("Chọn một ảnh trong danh sách để xem.");

        UpdateProgressIdle();
        UpdateTotals();
    }

    private void OpenOutputFolder()
    {
        string folder = txtOutputFolder.Text.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            MessageBox.Show(
                "Chưa có thư mục lưu. Bấm nút \"Chọn\" để chọn thư mục output trước.",
                "Chưa chọn thư mục", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

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
    private async void StartProcessing()
    {
        if (_isRunning)
            return;

        List<CompressionItem> pending = _items.Where(i => i.Status == CompressionStatus.Waiting).ToList();
        if (pending.Count == 0)
        {
            MessageBox.Show("Không có ảnh nào đang chờ xử lý.", "Thông báo");
            return;
        }

        string outputFolder = txtOutputFolder.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            MessageBox.Show("Vui lòng chọn thư mục lưu kết quả.", "Thông báo");
            return;
        }

        CompressionOptions options = new()
        {
            Quality = _selectedQuality,
            KeepOriginalDimensions = chkKeepDims.Checked,
            Format = cboFormat.SelectedIndex == 1 ? OutputImageFormat.WebP : OutputImageFormat.Jpeg,
            OutputFolder = outputFolder
        };

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        _isRunning = true;
        SetRunningState(true);

        int total = pending.Count;
        int done = 0;

        // Bounded worker pool (BUỘC #25) — không tạo N task cùng lúc cho batch
        // lớn, ưu tiên ổn định RAM hơn tốc độ tối đa.
        int workerCount = Math.Clamp(Environment.ProcessorCount / 2, 2, 4);
        SemaphoreSlim gate = new(workerCount);
        List<Task> running = new();

        try
        {
            foreach (CompressionItem item in pending)
            {
                await gate.WaitAsync(token);

                running.Add(RunOneAsync(item, options, token, gate, () =>
                {
                    done++;
                    progressBar.Value = Math.Clamp((int)(done * 100.0 / total), 0, 100);
                    lblProgress.Text = $"Đang xử lý {done} / {total}";
                    UpdateTotals();
                }));
            }
        }
        catch (OperationCanceledException)
        {
            // Dừng thêm việc mới — các worker đang chạy vẫn được đợi xong dưới đây,
            // ảnh chưa chạy giữ nguyên Waiting (BUỘC #27).
        }

        await Task.WhenAll(running);

        lblProgress.Text = token.IsCancellationRequested
            ? $"Đã dừng sau {done} / {total}"
            : $"Hoàn tất {done} / {total}";

        _isRunning = false;
        SetRunningState(false);
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunOneAsync(
        CompressionItem item,
        CompressionOptions options,
        CancellationToken token,
        SemaphoreSlim gate,
        Action onFinished)
    {
        try
        {
            item.Status = CompressionStatus.Compressing;
            RefreshRow(item);

            CompressionResult result = await Task.Run(() => _service.Compress(item.FilePath, options, token), token);
            ApplyResult(item, result);
        }
        catch (OperationCanceledException)
        {
            item.Status = CompressionStatus.Cancelled;
        }
        finally
        {
            RefreshRow(item);
            gate.Release();
            onFinished();

            if (ReferenceEquals(_previewItem, item))
                ShowPreviewForSelection();
        }
    }

    private static void ApplyResult(CompressionItem item, CompressionResult result)
    {
        if (result.Success)
        {
            item.Status = result.OriginalKept ? CompressionStatus.OriginalKept : CompressionStatus.Done;
            item.OutputPath = result.OutputPath;
            item.OutputBytes = result.OutputBytes;
            item.OutputWidth = result.OutputWidth;
            item.OutputHeight = result.OutputHeight;
            item.SavedPercent = result.SavedPercent;
        }
        else
        {
            item.Status = CompressionStatus.Error;
            item.ErrorMessage = result.Error;
        }
    }

    private void SetRunningState(bool running)
    {
        btnStart.Enabled = !running;
        btnStop.Enabled = running;
        btnClearList.Enabled = !running;
        btnChooseFiles.Enabled = !running;
        btnChooseFolder.Enabled = !running;
        btnChooseOutput.Enabled = !running;
        chkIncludeSubfolders.Enabled = !running;
        chkKeepDims.Enabled = !running;
        cboFormat.Enabled = !running;
    }

    private void RefreshRow(CompressionItem item)
    {
        int index = _items.IndexOf(item);
        if (index < 0 || index >= dgvImages.Rows.Count)
            return;

        bool finishedOk = item.Status is CompressionStatus.Done or CompressionStatus.OriginalKept;
        bool terminalNoOutput = item.Status is CompressionStatus.Error or CompressionStatus.Cancelled;

        DataGridViewRow row = dgvImages.Rows[index];
        row.Cells[2].Value = FormatBytes(item.OriginalBytes);
        row.Cells[3].Value = finishedOk ? FormatBytes(item.OutputBytes) : terminalNoOutput ? "—" : "";
        row.Cells[4].Value = finishedOk ? $"{item.OutputWidth} × {item.OutputHeight}" : item.OriginalResolution;
        row.Cells[5].Value = finishedOk ? $"{item.SavedPercent:0}%" : terminalNoOutput ? "—" : "";
        row.Cells[6].Value = item.StatusText;

        dgvImages.InvalidateRow(index);
    }

    private void UpdateProgressIdle()
    {
        progressBar.Value = 0;
        int waiting = _items.Count(i => i.Status == CompressionStatus.Waiting);
        lblProgress.Text = _items.Count == 0
            ? "Sẵn sàng."
            : $"{_items.Count} ảnh trong danh sách — {waiting} đang chờ xử lý.";
    }

    private void UpdateTotals()
    {
        List<CompressionItem> counted = _items.Where(i => i.CountsTowardTotals).ToList();
        long totalOriginal = counted.Sum(i => i.OriginalBytes);
        long totalOutput = counted.Sum(i => i.OutputBytes);
        long saved = Math.Max(0, totalOriginal - totalOutput);
        double pct = totalOriginal > 0 ? saved * 100.0 / totalOriginal : 0;

        lblTotalReadout.Text = $"{FormatBytes(totalOriginal)}   →   {FormatBytes(totalOutput)}";
        lblTotalSaved.Text = counted.Count > 0 ? $"−{pct:0.0}%" : "—";
        lblTotalMeta.Text = _items.Count == 0
            ? "Chưa có ảnh"
            : $"Tiết kiệm {FormatBytes(saved)} · {counted.Count}/{_items.Count} ảnh";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        double kb = bytes / 1024.0;
        if (kb < 1024)
            return kb < 10 ? $"{kb:0.0} KB" : $"{kb:0} KB";

        double mb = bytes / 1048576.0;
        return $"{mb:0.00} MB";
    }
    #endregion

    #region Preview
    private void ShowPreviewForSelection()
    {
        if (dgvImages.SelectedRows.Count == 0)
        {
            _previewItem = null;
            _previewToken = Guid.NewGuid();
            SetPreviewImages(null, null);
            ShowEmptyPreview("Chọn một ảnh trong danh sách để xem.");
            return;
        }

        int index = dgvImages.SelectedRows[0].Index;
        if (index < 0 || index >= _items.Count)
            return;

        CompressionItem item = _items[index];
        _previewItem = item;

        Guid token = Guid.NewGuid();
        _previewToken = token;

        bool hasOutput = item.Status is CompressionStatus.Done or CompressionStatus.OriginalKept
            && !string.IsNullOrEmpty(item.OutputPath) && File.Exists(item.OutputPath);

        string originalPath = item.FilePath;
        string? outputPath = hasOutput ? item.OutputPath : null;

        Task.Run(() =>
        {
            Bitmap? orig = ImageCompressionService.TryDecodeThumbnail(originalPath, PreviewBoxSize, out _, out _);
            Bitmap? comp = outputPath != null
                ? ImageCompressionService.TryDecodeThumbnail(outputPath, PreviewBoxSize, out _, out _)
                : null;
            return (orig, comp);
        }).ContinueWith(t =>
        {
            if (t.Status != TaskStatus.RanToCompletion)
                return;

            (Bitmap? orig, Bitmap? comp) = t.Result;

            if (token != _previewToken)
            {
                orig?.Dispose();
                comp?.Dispose();
                return;
            }

            SetPreviewImages(orig, comp);
            ApplyPreviewState(item, orig != null, comp != null, hasOutput);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void ApplyPreviewState(CompressionItem item, bool hasOriginal, bool hasCompressed, bool wasProcessed)
    {
        if (!hasOriginal)
        {
            ShowEmptyPreview("Không đọc được ảnh gốc.");
            return;
        }

        lblPreviewEmpty.Visible = false;
        picOriginal.Visible = true;
        lblOrigTag.Visible = true;
        lblOrigStats.Visible = true;
        lblOrigStats.Text = $"{item.OriginalResolution} · {FormatBytes(item.OriginalBytes)}";

        if (wasProcessed && hasCompressed)
        {
            picCompressed.Visible = true;
            lblCompTag.Visible = true;
            lblCompStats.Visible = true;
            lblCompStats.Text = $"{item.OutputWidth} × {item.OutputHeight} · {FormatBytes(item.OutputBytes)}";

            lblSavedBig.Visible = true;
            lblSavedBig.Text = item.Status == CompressionStatus.OriginalKept
                ? "Ảnh gốc đã tối ưu"
                : $"−{item.SavedPercent:0}%";
            lblSavedBig.Font = item.Status == CompressionStatus.OriginalKept ? AppTheme.Fonts.BodyBold : AppTheme.Fonts.Title;
        }
        else
        {
            picCompressed.Visible = false;
            lblCompTag.Visible = false;
            lblCompStats.Visible = true;
            lblCompStats.Text = item.Status == CompressionStatus.Error
                ? $"Lỗi: {item.ErrorMessage}"
                : item.Status == CompressionStatus.Cancelled
                    ? "Đã hủy — chưa có kết quả."
                    : "Chưa xử lý — bấm GIẢM DUNG LƯỢNG để xem kết quả.";
            lblSavedBig.Visible = false;
        }
    }

    private void ShowEmptyPreview(string message)
    {
        lblPreviewEmpty.Text = message;
        lblPreviewEmpty.Visible = true;

        picOriginal.Visible = false;
        lblOrigTag.Visible = false;
        lblOrigStats.Visible = false;
        picCompressed.Visible = false;
        lblCompTag.Visible = false;
        lblCompStats.Visible = false;
        lblSavedBig.Visible = false;
    }

    private void SetPreviewImages(Bitmap? orig, Bitmap? comp)
    {
        Image? oldOrig = picOriginal.Image;
        Image? oldComp = picCompressed.Image;

        picOriginal.Image = orig;
        picCompressed.Image = comp;

        oldOrig?.Dispose();
        oldComp?.Dispose();
    }
    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            SetPreviewImages(null, null);
        }

        base.Dispose(disposing);
    }
}
