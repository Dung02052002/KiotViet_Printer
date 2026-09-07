using System.Diagnostics;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
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

    private readonly SmoothFlowLayoutPanel flpButtons = new();
    private readonly RoundedButton btnStart = new();
    private readonly RoundedButton btnStop = new();
    private readonly RoundedButton btnClearList = new();
    private readonly RoundedButton btnOpenOutput = new();

    private readonly ProgressBar progressBar = new();
    private readonly Label lblProgress = new();

    private readonly Panel pnlGrid = new();
    private readonly SmoothDataGridView dgvImages = new();

    private readonly Panel pnlPreview = new();
    private readonly Label lblPreviewOriginalTitle = new();
    private readonly PictureBox pbOriginal = new();
    private readonly Label lblPreviewResultTitle = new();
    private readonly PictureBox pbResult = new();
    private readonly Label lblResultEmpty = new();

    public string OutputFolder => txtOutputFolder.Text.Trim();

    public BackgroundRemoverView()
    {
        Dock = DockStyle.Fill;
        BuildUi();
        WireDragDrop();

        txtOutputFolder.Text = Path.Combine(AppContext.BaseDirectory, "Output", "BackgroundRemoved");

        Resize += (_, _) => flpButtons.PerformLayout();
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

        // Provider ONNX Runtime thực tế đang chạy — cập nhật khi bắt đầu xử lý
        // (StartProcessing gọi EnsureLoaded trước batch), không phải chỉ "không
        // crash". Log chi tiết hơn nằm ở BackgroundRemovalDiagnosticsLog.LogFolder.
        lblProvider.Left = 310;
        lblProvider.Top = top + 48;
        lblProvider.Width = 300;
        lblProvider.Font = AppTheme.Fonts.Hint;
        lblProvider.ForeColor = AppTheme.Colors.TextMuted;
        lblProvider.Text = "ONNX Provider: chưa tải model";
        pnlCard.Controls.Add(lblProvider);

        pnlCard.SizeChanged += (_, _) => LayoutSettingsRow();
        LayoutSettingsRow();

        RepositionAfterSettings(top + 88);
    }

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
    private void BuildActionButtons()
    {
        flpButtons.Left = 28;
        flpButtons.Top = _afterSettingsTop;
        flpButtons.Width = Math.Max(0, pnlCard.Width - 56);
        flpButtons.Height = 108;
        flpButtons.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        flpButtons.WrapContents = true;
        flpButtons.FlowDirection = FlowDirection.LeftToRight;
        flpButtons.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(flpButtons);

        Padding margin = new(0, 0, 12, 12);

        btnStart.Text = "XÓA NỀN";
        btnStart.Icon = IconGlyphs.Kind.MagicWand;
        btnStart.Size = new Size(150, 44);
        btnStart.Margin = margin;
        btnStart.Variant = ButtonVariant.Primary;
        btnStart.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnStart.Click += (_, _) => StartProcessing();
        flpButtons.Controls.Add(btnStart);

        btnStop.Text = "DỪNG";
        btnStop.Icon = IconGlyphs.Kind.Stop;
        btnStop.Size = new Size(120, 44);
        btnStop.Margin = margin;
        btnStop.Variant = ButtonVariant.Outline;
        btnStop.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnStop.Enabled = false;
        btnStop.Click += (_, _) => _cts?.Cancel();
        flpButtons.Controls.Add(btnStop);

        btnClearList.Text = "XÓA DANH SÁCH";
        btnClearList.Icon = IconGlyphs.Kind.Trash;
        btnClearList.Size = new Size(178, 44);
        btnClearList.Margin = margin;
        btnClearList.Variant = ButtonVariant.Outline;
        btnClearList.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnClearList.Click += (_, _) => ClearList();
        flpButtons.Controls.Add(btnClearList);

        btnOpenOutput.Text = "MỞ THƯ MỤC OUTPUT";
        btnOpenOutput.Icon = IconGlyphs.Kind.Folder;
        btnOpenOutput.Size = new Size(206, 44);
        btnOpenOutput.Margin = margin;
        btnOpenOutput.Variant = ButtonVariant.Outline;
        btnOpenOutput.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnOpenOutput.Click += (_, _) => OpenOutputFolder();
        flpButtons.Controls.Add(btnOpenOutput);
    }

    private void BuildProgressRow()
    {
        int top = flpButtons.Bottom + 4;

        lblProgress.Text = "Sẵn sàng.";
        lblProgress.Left = 32;
        lblProgress.Top = top;
        lblProgress.Width = Math.Max(0, pnlCard.Width - 64);
        lblProgress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblProgress.Font = AppTheme.Fonts.Hint;
        lblProgress.ForeColor = AppTheme.Colors.TextSecondary;
        pnlCard.Controls.Add(lblProgress);

        progressBar.Left = 32;
        progressBar.Top = top + 20;
        progressBar.Width = Math.Max(0, pnlCard.Width - 64);
        progressBar.Height = 10;
        progressBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        progressBar.Value = 0;
        pnlCard.Controls.Add(progressBar);
    }
    #endregion

    #region Split content: list + preview
    private const int PreviewWidth = 360;
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
        _splitTop = progressBar.Bottom + 20;

        pnlPreview.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlPreview);

        pnlGrid.BackColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlGrid);

        BuildImageGrid();
        BuildPreviewPanel();

        pnlCard.SizeChanged += (_, _) => RelayoutSplit();
        RelayoutSplit();
    }

    private void RelayoutSplit()
    {
        int cardWidth = Math.Max(1, pnlCard.ClientSize.Width);
        int cardHeight = Math.Max(1, pnlCard.ClientSize.Height);
        int height = Math.Max(1, cardHeight - _splitTop - 24);

        pnlPreview.SetBounds(cardWidth - 32 - PreviewWidth, _splitTop, PreviewWidth, height);

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
        lblPreviewOriginalTitle.Text = "ẢNH GỐC";
        lblPreviewOriginalTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblPreviewOriginalTitle.Font = AppTheme.Fonts.Overline;
        lblPreviewOriginalTitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlPreview.Controls.Add(lblPreviewOriginalTitle);

        lblPreviewResultTitle.Text = "KẾT QUẢ";
        lblPreviewResultTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblPreviewResultTitle.Font = AppTheme.Fonts.Overline;
        lblPreviewResultTitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlPreview.Controls.Add(lblPreviewResultTitle);

        pbOriginal.SizeMode = PictureBoxSizeMode.Zoom;
        pbOriginal.BackColor = AppTheme.Colors.Surface;
        pbOriginal.BorderStyle = BorderStyle.FixedSingle;
        pnlPreview.Controls.Add(pbOriginal);

        pbResult.SizeMode = PictureBoxSizeMode.Zoom;
        pbResult.BackColor = AppTheme.Colors.Surface;
        pbResult.BorderStyle = BorderStyle.FixedSingle;
        pnlPreview.Controls.Add(pbResult);

        lblResultEmpty.Text = "Chưa xử lý";
        lblResultEmpty.TextAlign = ContentAlignment.MiddleCenter;
        lblResultEmpty.Font = AppTheme.Fonts.Body;
        lblResultEmpty.ForeColor = AppTheme.Colors.TextMuted;
        lblResultEmpty.BackColor = AppTheme.Colors.Surface;
        lblResultEmpty.BorderStyle = BorderStyle.FixedSingle;
        pnlPreview.Controls.Add(lblResultEmpty);
    }

    private void LayoutPreviewPanel()
    {
        int halfWidth = Math.Max(1, (pnlPreview.ClientSize.Width - 12) / 2);
        int bodyHeight = Math.Max(1, pnlPreview.ClientSize.Height - 24);

        lblPreviewOriginalTitle.SetBounds(0, 0, halfWidth, 18);
        lblPreviewResultTitle.SetBounds(halfWidth + 12, 0, halfWidth, 18);

        pbOriginal.SetBounds(0, 24, halfWidth, bodyHeight);
        pbResult.SetBounds(halfWidth + 12, 24, halfWidth, bodyHeight);
        lblResultEmpty.SetBounds(halfWidth + 12, 24, halfWidth, bodyHeight);
    }

    private void ShowPreviewForSelection()
    {
        if (dgvImages.SelectedRows.Count == 0)
            return;

        int index = dgvImages.SelectedRows[0].Index;
        if (index < 0 || index >= _items.Count)
            return;

        BackgroundRemovalItem item = _items[index];

        pbOriginal.Image?.Dispose();
        pbOriginal.Image = null;

        try
        {
            pbOriginal.Image = DecodeForDisplay(item.FilePath, 900);
        }
        catch
        {
            // Ảnh lỗi/không đọc được — để trống thay vì crash preview.
        }

        pbResult.Image?.Dispose();
        pbResult.Image = null;

        bool done = item.Status == ImageProcessStatus.Done &&
                    !string.IsNullOrWhiteSpace(item.ResultPath) &&
                    File.Exists(item.ResultPath);

        if (done)
        {
            try
            {
                pbResult.Image = DecodeForDisplay(item.ResultPath!, 900);
            }
            catch
            {
                done = false;
            }
        }

        pbResult.Visible = done;
        lblResultEmpty.Visible = !done;
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

        pbOriginal.Image?.Dispose();
        pbOriginal.Image = null;
        pbResult.Image?.Dispose();
        pbResult.Image = null;
        lblResultEmpty.Visible = true;
        pbResult.Visible = false;

        UpdateProgressIdle();
    }

    private void OpenOutputFolder()
    {
        try
        {
            Directory.CreateDirectory(OutputFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{OutputFolder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Không mở được thư mục");
        }
    }
    #endregion

    #region Processing
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

        if (!_service.IsModelAvailable)
        {
            MessageBox.Show(
                $"Không tìm thấy model ONNX:\n{_service.ModelPath}\n\nVui lòng đặt file model đúng vị trí rồi thử lại.",
                "Thiếu model");
            return;
        }

        string outputFolder = OutputFolder;
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            MessageBox.Show("Vui lòng chọn thư mục lưu kết quả.", "Thông báo");
            return;
        }

        InferenceDevice device = cboDevice.SelectedIndex switch
        {
            1 => InferenceDevice.Cpu,
            2 => InferenceDevice.Gpu,
            _ => InferenceDevice.Auto
        };

        _cts = new CancellationTokenSource();
        _isRunning = true;
        SetRunningState(true);

        int total = pending.Count;
        int done = 0;

        // Load/xác nhận provider TRƯỚC batch (thay vì để lần lượt xảy ra ở ảnh đầu
        // tiên bên trong ProcessCore) — người dùng thấy provider thật ngay, kể cả
        // khi batch bị Dừng trước khi xử lý xong ảnh nào.
        lblProvider.Text = "ONNX Provider: đang tải model...";
        try
        {
            await Task.Run(() => _service.EnsureLoaded(device), _cts.Token);
            lblProvider.Text = $"ONNX Provider: {_service.ActiveProviderDescription}";
        }
        catch (OperationCanceledException)
        {
            lblProvider.Text = "ONNX Provider: đã dừng trước khi tải model.";
            _isRunning = false;
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
            return;
        }
        catch (Exception ex)
        {
            lblProvider.Text = "ONNX Provider: lỗi tải model.";
            MessageBox.Show(ex.Message, "Không tải được model");
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

                BackgroundRemovalResult result = await _service.ProcessAsync(
                    item.FilePath,
                    outputFolder,
                    device,
                    _cts.Token);

                if (result.Success)
                {
                    item.Status = ImageProcessStatus.Done;
                    item.ResultPath = result.OutputPath;
                }
                else
                {
                    item.Status = ImageProcessStatus.Error;
                    item.ErrorMessage = result.ErrorMessage;
                }

                RefreshRow(item);

                done++;
                progressBar.Value = Math.Clamp((int)(done * 100.0 / total), 0, 100);

                if (dgvImages.SelectedRows.Count > 0 && _items.IndexOf(item) == dgvImages.SelectedRows[0].Index)
                    ShowPreviewForSelection();
            }

            lblProgress.Text = $"Hoàn tất {done} / {total}.";
        }
        catch (OperationCanceledException)
        {
            lblProgress.Text = $"Đã dừng sau {done} / {total}.";
        }
        finally
        {
            _isRunning = false;
            SetRunningState(false);
            _cts?.Dispose();
            _cts = null;
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
        cboDevice.Enabled = !running;
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

        return EncodeToGdiImage(canvas);
    }

    private static Image DecodeForDisplay(string path, int maxSide)
    {
        using FileStream fs = File.OpenRead(path);
        using SKBitmap decoded = SKBitmap.Decode(fs)
            ?? throw new InvalidDataException("Không đọc được ảnh.");

        if (decoded.Width <= maxSide && decoded.Height <= maxSide)
            return EncodeToGdiImage(decoded);

        float scale = Math.Min((float)maxSide / decoded.Width, (float)maxSide / decoded.Height);
        int w = Math.Max(1, (int)Math.Round(decoded.Width * scale));
        int h = Math.Max(1, (int)Math.Round(decoded.Height * scale));

        using SKBitmap resized = decoded.Resize(
                new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("Resize preview thất bại.");

        return EncodeToGdiImage(resized);
    }

    // Bridge sang System.Drawing.Image vì các control hiển thị của WinForms
    // (PictureBox, DataGridViewImageColumn) chỉ nhận kiểu này — không dùng thêm
    // thư viện ảnh thứ hai, chỉ mã hoá PNG rồi đọc lại bằng GDI+.
    private static Image EncodeToGdiImage(SKBitmap bitmap)
    {
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 90);

        // Không dispose stream: GDI+ Image có thể truy cập lười vào stream gốc,
        // dispose sớm gây lỗi "generic GDI+ error" khi vẽ lại sau này.
        MemoryStream ms = new(data.ToArray());
        return Image.FromStream(ms);
    }
    #endregion
}
