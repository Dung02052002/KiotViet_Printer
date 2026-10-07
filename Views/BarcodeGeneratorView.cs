using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Infrastructure.Excel;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services.Barcodes;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Views;

// Màn hình "Tạo mã vạch" — nhúng vào FormMain giống cơ chế show/hide của
// BackgroundRemoverView/ImageCompressorView (xem FormMain.OpenBarcodeGenerator).
// UI chỉ gọi Code128BarcodeService/BarcodePrintService, không tự mã hoá vạch.
public class BarcodeGeneratorView : UserControl
{
    private readonly Code128BarcodeService _service = new();
    private readonly BarcodePrintService _printService = new();

    private BarcodeImage? _current;
    private bool _isBatchRunning;

    /// <summary>Đang tạo ảnh hàng loạt — FormMain hỏi lại trước khi đóng app.</summary>
    public bool IsBusy => _isBatchRunning;
    private bool _suppressSettingsEvents;

    // Tem đang xem trước (từ ô nhập hoặc dòng đang chọn trong bảng HÀNG HÓA) —
    // đổi thông số thì vẽ lại đúng tem này.
    private string _previewName = "";
    private string _previewCode = "";
    private bool _bulkEditing;
    private bool _previewFromGrid;

    private readonly RoundedPanel pnlCard = new();

    // Header
    private readonly RoundedPanel pnlIconBadge = new();
    private readonly IconGlyph iconHeader = new();
    private readonly Label lblHeaderTitle = new();
    private readonly Label lblHeaderSubtitle = new();
    private readonly Panel pnlHeaderDivider = new();

    // Nhập tên hàng + mã hàng
    private readonly Label lblNameTitle = new();
    private readonly RoundedTextBox txtName = new();
    private readonly Label lblCodeTitle = new();
    private readonly RoundedTextBox txtCode = new();
    private readonly RoundedButton btnGenerate = new();

    // Preview + thao tác
    private readonly Label lblPreviewTitle = new();
    private readonly RoundedPanel pnlPreview = new();
    private readonly BarcodePreviewBox picPreview = new();
    private readonly Label lblPreviewEmpty = new();
    private readonly Label lblPreviewInfo = new();
    private readonly RoundedButton btnSave = new();
    private readonly RoundedButton btnCopy = new();
    private readonly RoundedButton btnPrint = new();

    // Tùy chỉnh
    private sealed class SettingField
    {
        public required Label Caption { get; init; }
        public required Control Input { get; init; }
    }

    private readonly Label lblSettingsTitle = new();
    private readonly List<SettingField> _settingFields = new();
    private readonly NumericUpDown nudNameFontSize = new();
    private readonly NumericUpDown nudFontSize = new();
    private readonly ComboBox cboFont = new();
    private readonly CheckBox chkBold = new();
    private readonly NumericUpDown nudNameGap = new();
    private readonly NumericUpDown nudTextGap = new();
    private readonly NumericUpDown nudModuleWidth = new();
    private readonly NumericUpDown nudTargetWidth = new();
    private readonly NumericUpDown nudBarHeight = new();
    private readonly NumericUpDown nudLabelWidth = new();
    private readonly NumericUpDown nudLabelHeight = new();
    private readonly NumericUpDown nudMarginX = new();
    private readonly NumericUpDown nudMarginY = new();
    private readonly ComboBox cboDpi = new();
    private readonly RoundedButton btnResetSettings = new();

    // Tạo hàng loạt
    private readonly Panel pnlBatchDivider = new();
    private readonly Label lblBatchTitle = new();
    private readonly Label lblBatchHint = new();
    private readonly BatchGrid dgvBatch = new();
    private readonly Label lblBatchCount = new();
    private readonly RoundedButton btnAddRow = new();
    private readonly RoundedButton btnImport = new();
    private readonly RoundedButton btnClearBatch = new();
    private readonly Label lblOutput = new();
    private readonly RoundedTextBox txtOutputFolder = new();
    private readonly RoundedButton btnChooseOutput = new();
    private readonly Label lblFormat = new();
    private readonly ComboBox cboFormat = new();
    private readonly RoundedButton btnBatchGenerate = new();
    private readonly RoundedButton btnBatchPrint = new();
    private readonly RoundedButton btnOpenOutput = new();
    private readonly Label lblProgress = new();
    private readonly ProgressBar progressBar = new();

    private static readonly int[] DpiChoices = { 203, 300, 600 };

    public BarcodeGeneratorView()
    {
        Dock = DockStyle.Fill;
        BuildUi();

        txtOutputFolder.Text = Path.Combine(AppContext.BaseDirectory, "Output", "BarcodeOutput");
        ApplyOptionsToInputs(new BarcodeRenderOptions());
        ShowEmptyPreview("Nhập tên hàng và mã hàng để xem trước tem.");
        UpdateBatchCount();
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
        BuildCodeInput();
        BuildPreview();
        BuildSettings();
        BuildBatch();

        pnlCard.SizeChanged += (_, _) => RelayoutAll();
        RelayoutAll();
    }

    #region Header
    private void BuildHeader()
    {
        pnlIconBadge.SetBounds(32, 32, 44, 44);
        pnlIconBadge.CornerRadius = 13;
        pnlIconBadge.FillColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.BorderThickness = 0;
        pnlIconBadge.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlIconBadge);

        iconHeader.Dock = DockStyle.Fill;
        iconHeader.Kind = IconGlyphs.Kind.Barcode;
        iconHeader.IconColor = AppTheme.Colors.Primary;
        iconHeader.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.Controls.Add(iconHeader);

        lblHeaderTitle.Text = "TẠO MÃ VẠCH";
        lblHeaderTitle.SetBounds(88, 32, 500, 26);
        lblHeaderTitle.Font = AppTheme.Fonts.SectionTitle;
        lblHeaderTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblHeaderTitle);

        lblHeaderSubtitle.Text = "Tem Code 128: Tên hàng → mã vạch → Mã hàng — lưu ảnh, sao chép hoặc in tem trực tiếp.";
        lblHeaderSubtitle.SetBounds(88, 58, 600, 18);
        lblHeaderSubtitle.Font = AppTheme.Fonts.Hint;
        lblHeaderSubtitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlCard.Controls.Add(lblHeaderSubtitle);

        pnlHeaderDivider.Height = 1;
        pnlHeaderDivider.BackColor = AppTheme.Colors.Border;
        pnlCard.Controls.Add(pnlHeaderDivider);
    }
    #endregion

    #region Nhập tên hàng + mã hàng
    private void BuildCodeInput()
    {
        StyleOverline(lblNameTitle, "TÊN HÀNG");

        txtName.Height = 40;
        txtName.ContainerColor = AppTheme.Colors.SurfaceElevated;
        txtName.PlaceholderText = "VD: 6592 - BLACK";
        txtName.TextChanged += (_, _) => PreviewFromInputs(showErrors: false);
        pnlCard.Controls.Add(txtName);

        StyleOverline(lblCodeTitle, "MÃ HÀNG");

        txtCode.Height = 40;
        txtCode.ContainerColor = AppTheme.Colors.SurfaceElevated;
        txtCode.PlaceholderText = "VD: SP5456847";
        txtCode.TextChanged += (_, _) => PreviewFromInputs(showErrors: false);
        pnlCard.Controls.Add(txtCode);

        btnGenerate.Text = "TẠO MÃ VẠCH";
        btnGenerate.Icon = IconGlyphs.Kind.Barcode;
        btnGenerate.Variant = ButtonVariant.Primary;
        btnGenerate.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnGenerate.Click += (_, _) => PreviewFromInputs(showErrors: true);
        pnlCard.Controls.Add(btnGenerate);
    }
    #endregion

    #region Preview
    private void BuildPreview()
    {
        StyleOverline(lblPreviewTitle, "XEM TRƯỚC");

        pnlPreview.CornerRadius = 16;
        pnlPreview.FillColor = Color.White;
        pnlPreview.BorderColor = AppTheme.Colors.BorderOutlineRest;
        pnlPreview.BorderThickness = 1;
        pnlPreview.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlPreview);

        picPreview.BackColor = Color.White;
        pnlPreview.Controls.Add(picPreview);

        lblPreviewEmpty.TextAlign = ContentAlignment.MiddleCenter;
        lblPreviewEmpty.Font = AppTheme.Fonts.Hint;
        lblPreviewEmpty.ForeColor = AppTheme.Colors.TextMuted;
        pnlPreview.Controls.Add(lblPreviewEmpty);

        lblPreviewInfo.Font = AppTheme.Fonts.Hint;
        lblPreviewInfo.ForeColor = AppTheme.Colors.TextSecondary;
        lblPreviewInfo.AutoEllipsis = true;
        pnlCard.Controls.Add(lblPreviewInfo);

        btnSave.Text = "Lưu ảnh";
        btnSave.Icon = IconGlyphs.Kind.Image;
        btnSave.Variant = ButtonVariant.Outline;
        btnSave.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnSave.Click += (_, _) => SaveCurrent();
        pnlCard.Controls.Add(btnSave);

        btnCopy.Text = "Sao chép";
        btnCopy.Icon = IconGlyphs.Kind.Document;
        btnCopy.Variant = ButtonVariant.Outline;
        btnCopy.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnCopy.Click += (_, _) => CopyCurrent();
        pnlCard.Controls.Add(btnCopy);

        btnPrint.Text = "In";
        btnPrint.Icon = IconGlyphs.Kind.Printer;
        btnPrint.Variant = ButtonVariant.Outline;
        btnPrint.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnPrint.Click += (_, _) => PrintCurrent();
        pnlCard.Controls.Add(btnPrint);
    }

    private void ShowEmptyPreview(string message)
    {
        picPreview.Image = null;
        _current?.Dispose();
        _current = null;
        picPreview.Visible = false;
        lblPreviewEmpty.Text = message;
        lblPreviewEmpty.Visible = true;
        lblPreviewInfo.Text = "";
        UpdateSingleActions();
    }

    private void UpdateSingleActions()
    {
        bool hasImage = _current != null;
        btnSave.Enabled = hasImage;
        btnCopy.Enabled = hasImage;
        btnPrint.Enabled = hasImage;
    }
    #endregion

    #region Tùy chỉnh
    private void BuildSettings()
    {
        StyleOverline(lblSettingsTitle, "BỐ CỤC TEM  (TÊN HÀNG → MÃ VẠCH → MÃ HÀNG)");

        // Chữ
        AddNumericField(nudNameFontSize, "Tên hàng — dòng trên (pt)", 4m, 36m, 0.5m, 1);
        AddNumericField(nudFontSize, "Mã hàng — dưới vạch (pt)", 4m, 36m, 0.5m, 1);

        cboFont.DropDownStyle = ComboBoxStyle.DropDownList;
        using (System.Drawing.Text.InstalledFontCollection installed = new())
        {
            foreach (FontFamily family in installed.Families)
            {
                cboFont.Items.Add(family.Name);
                family.Dispose();
            }
        }
        AppTheme.StyleComboBox(cboFont);
        cboFont.SelectedIndexChanged += (_, _) => OnSettingsChanged();
        AddField(cboFont, "Font chữ");

        chkBold.Text = "In đậm tên hàng và mã hàng";
        chkBold.Font = AppTheme.Fonts.Body;
        chkBold.ForeColor = AppTheme.Colors.TextPrimary;
        chkBold.BackColor = AppTheme.Colors.SurfaceElevated;
        chkBold.CheckedChanged += (_, _) => OnSettingsChanged();
        AddField(chkBold, "Chữ đậm");

        AddNumericField(nudNameGap, "Tên hàng → vạch (mm)", 0m, 10m, 0.1m, 1);
        AddNumericField(nudTextGap, "Vạch → mã hàng (mm)", 0m, 10m, 0.1m, 1);

        // Mã vạch
        AddNumericField(nudModuleWidth, "Rộng vạch mảnh (mm)", 0.10m, 2.00m, 0.01m, 2);
        AddNumericField(nudTargetWidth, "Rộng phần vạch (mm, 0 = tự động)", 0m, 300m, 1m, 1);
        AddNumericField(nudBarHeight, "Cao vạch (mm)", 3m, 100m, 0.5m, 1);

        cboDpi.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (int dpi in DpiChoices)
            cboDpi.Items.Add($"{dpi} DPI");
        AppTheme.StyleComboBox(cboDpi);
        cboDpi.SelectedIndexChanged += (_, _) => OnSettingsChanged();
        AddField(cboDpi, "DPI máy in");

        // Kích thước tem
        AddNumericField(nudLabelWidth, "Rộng tem (mm, 0 = tự động)", 0m, 300m, 0.5m, 1);
        AddNumericField(nudLabelHeight, "Cao tem (mm, 0 = tự động)", 0m, 300m, 0.5m, 1);
        AddNumericField(nudMarginX, "Lề trái / phải (mm)", 0m, 30m, 0.5m, 1);
        AddNumericField(nudMarginY, "Lề trên / dưới (mm)", 0m, 30m, 0.5m, 1);

        btnResetSettings.Text = "Khôi phục mặc định";
        btnResetSettings.Icon = IconGlyphs.Kind.Refresh;
        btnResetSettings.Variant = ButtonVariant.Outline;
        btnResetSettings.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnResetSettings.Click += (_, _) =>
        {
            ApplyOptionsToInputs(new BarcodeRenderOptions());
            OnSettingsChanged();
        };
        pnlCard.Controls.Add(btnResetSettings);
    }

    private void AddNumericField(NumericUpDown input, string caption, decimal min, decimal max, decimal step, int decimals)
    {
        input.Minimum = min;
        input.Maximum = max;
        input.Increment = step;
        input.DecimalPlaces = decimals;
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Font = AppTheme.Fonts.Body;
        input.BackColor = AppTheme.Colors.InputBackground;
        input.ForeColor = AppTheme.Colors.TextPrimary;
        input.TextAlign = HorizontalAlignment.Right;
        input.ValueChanged += (_, _) => OnSettingsChanged();
        AddField(input, caption);
    }

    private void AddField(Control input, string caption)
    {
        Label lbl = new()
        {
            Text = caption,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextSecondary,
            AutoEllipsis = true
        };
        pnlCard.Controls.Add(lbl);
        pnlCard.Controls.Add(input);
        _settingFields.Add(new SettingField { Caption = lbl, Input = input });
    }

    private void ApplyOptionsToInputs(BarcodeRenderOptions options)
    {
        _suppressSettingsEvents = true;
        try
        {
            nudNameFontSize.Value = (decimal)options.NameFontSizePt;
            nudFontSize.Value = (decimal)options.FontSizePt;
            int fontIndex = cboFont.Items.IndexOf(options.FontFamily);
            if (fontIndex < 0)
                fontIndex = cboFont.Items.Add(options.FontFamily);
            cboFont.SelectedIndex = fontIndex;
            chkBold.Checked = options.Bold;
            nudNameGap.Value = (decimal)options.NameGapMm;
            nudTextGap.Value = (decimal)options.TextGapMm;
            nudModuleWidth.Value = (decimal)options.ModuleWidthMm;
            nudTargetWidth.Value = (decimal)options.TargetWidthMm;
            nudBarHeight.Value = (decimal)options.BarHeightMm;
            nudLabelWidth.Value = (decimal)options.LabelWidthMm;
            nudLabelHeight.Value = (decimal)options.LabelHeightMm;
            nudMarginX.Value = (decimal)options.MarginHorizontalMm;
            nudMarginY.Value = (decimal)options.MarginVerticalMm;
            int dpiIndex = Array.IndexOf(DpiChoices, options.Dpi);
            cboDpi.SelectedIndex = dpiIndex >= 0 ? dpiIndex : 1;
        }
        finally
        {
            _suppressSettingsEvents = false;
        }

        nudModuleWidth.Enabled = nudTargetWidth.Value == 0;
    }

    private BarcodeRenderOptions ReadOptions()
    {
        return new BarcodeRenderOptions
        {
            NameFontSizePt = (float)nudNameFontSize.Value,
            FontSizePt = (float)nudFontSize.Value,
            FontFamily = cboFont.SelectedItem as string ?? "Arial",
            Bold = chkBold.Checked,
            NameGapMm = (double)nudNameGap.Value,
            TextGapMm = (double)nudTextGap.Value,
            ModuleWidthMm = (double)nudModuleWidth.Value,
            TargetWidthMm = (double)nudTargetWidth.Value,
            BarHeightMm = (double)nudBarHeight.Value,
            LabelWidthMm = (double)nudLabelWidth.Value,
            LabelHeightMm = (double)nudLabelHeight.Value,
            MarginHorizontalMm = (double)nudMarginX.Value,
            MarginVerticalMm = (double)nudMarginY.Value,
            Dpi = cboDpi.SelectedIndex >= 0 ? DpiChoices[cboDpi.SelectedIndex] : 300
        };
    }

    // Đổi thông số thì vẽ lại ngay tem đang xem để preview luôn khớp với ảnh
    // sẽ lưu/in.
    private void OnSettingsChanged()
    {
        if (_suppressSettingsEvents)
            return;

        nudModuleWidth.Enabled = nudTargetWidth.Value == 0;

        if (_previewCode.Length > 0)
            ShowPreview(_previewName, _previewCode, showErrors: false, force: true);
    }
    #endregion

    #region Tạo hàng loạt
    private void BuildBatch()
    {
        pnlBatchDivider.Height = 1;
        pnlBatchDivider.BackColor = AppTheme.Colors.Border;
        pnlCard.Controls.Add(pnlBatchDivider);

        StyleOverline(lblBatchTitle, "HÀNG HÓA  (TẠO / IN HÀNG LOẠT — MỖI DÒNG MỘT TEM)");

        lblBatchHint.Text = "Nhập Tên hàng + Mã hàng, Enter để sang ô / dòng tiếp theo. Có thể dán (Ctrl+V) từ Excel " +
                            "hoặc mở file Excel/CSV: cột 1 = Tên hàng, cột 2 = Mã hàng.";
        lblBatchHint.Font = AppTheme.Fonts.Hint;
        lblBatchHint.ForeColor = AppTheme.Colors.TextMuted;
        lblBatchHint.AutoEllipsis = true;
        pnlCard.Controls.Add(lblBatchHint);

        BuildBatchGrid();

        lblBatchCount.Font = AppTheme.Fonts.BodyBold;
        lblBatchCount.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblBatchCount);

        btnAddRow.Text = "+ Thêm dòng";
        btnAddRow.Variant = ButtonVariant.Outline;
        btnAddRow.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnAddRow.Click += (_, _) => AddBatchRow();
        pnlCard.Controls.Add(btnAddRow);

        btnImport.Text = "Mở Excel / CSV";
        btnImport.Icon = IconGlyphs.Kind.Document;
        btnImport.Variant = ButtonVariant.Outline;
        btnImport.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnImport.Click += async (_, _) => await ImportItemsAsync();
        pnlCard.Controls.Add(btnImport);

        btnClearBatch.Text = "Xóa hết";
        btnClearBatch.Icon = IconGlyphs.Kind.Trash;
        btnClearBatch.Variant = ButtonVariant.Outline;
        btnClearBatch.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnClearBatch.Click += (_, _) =>
        {
            dgvBatch.CancelEdit();
            RunBulkEdit(() => dgvBatch.Rows.Clear());
        };
        pnlCard.Controls.Add(btnClearBatch);

        lblOutput.Text = "Thư mục lưu";
        lblOutput.Font = AppTheme.Fonts.Body;
        lblOutput.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblOutput);

        txtOutputFolder.Height = 40;
        txtOutputFolder.ContainerColor = AppTheme.Colors.SurfaceElevated;
        txtOutputFolder.ReadOnly = true;
        pnlCard.Controls.Add(txtOutputFolder);

        btnChooseOutput.Text = "Chọn";
        btnChooseOutput.Icon = IconGlyphs.Kind.Folder;
        btnChooseOutput.Variant = ButtonVariant.Outline;
        btnChooseOutput.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnChooseOutput.Click += (_, _) => ChooseOutputFolder();
        pnlCard.Controls.Add(btnChooseOutput);

        lblFormat.Text = "Định dạng";
        lblFormat.Font = AppTheme.Fonts.Body;
        lblFormat.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblFormat);

        cboFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        cboFormat.Items.AddRange(new object[] { "PNG — nét nhất khi in (khuyên dùng)", "JPG" });
        cboFormat.SelectedIndex = 0;
        AppTheme.StyleComboBox(cboFormat);
        pnlCard.Controls.Add(cboFormat);

        btnBatchGenerate.Text = "TẠO HÀNG LOẠT";
        btnBatchGenerate.Icon = IconGlyphs.Kind.Barcode;
        btnBatchGenerate.Variant = ButtonVariant.Primary;
        btnBatchGenerate.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnBatchGenerate.Click += async (_, _) => await GenerateBatchAsync();
        pnlCard.Controls.Add(btnBatchGenerate);

        btnBatchPrint.Text = "In hàng loạt";
        btnBatchPrint.Icon = IconGlyphs.Kind.Printer;
        btnBatchPrint.Variant = ButtonVariant.Outline;
        btnBatchPrint.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnBatchPrint.Click += (_, _) => PrintBatch();
        pnlCard.Controls.Add(btnBatchPrint);

        btnOpenOutput.Text = "Mở thư mục";
        btnOpenOutput.Icon = IconGlyphs.Kind.Folder;
        btnOpenOutput.Variant = ButtonVariant.Outline;
        btnOpenOutput.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnOpenOutput.Click += (_, _) => OpenOutputFolder();
        pnlCard.Controls.Add(btnOpenOutput);

        lblProgress.Text = "Sẵn sàng.";
        lblProgress.Font = AppTheme.Fonts.Hint;
        lblProgress.ForeColor = AppTheme.Colors.TextSecondary;
        lblProgress.AutoEllipsis = true;
        pnlCard.Controls.Add(lblProgress);

        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        pnlCard.Controls.Add(progressBar);
    }

    private void BuildBatchGrid()
    {
        dgvBatch.AllowUserToAddRows = true;       // luôn có một dòng trống ở cuối để gõ tiếp
        dgvBatch.AllowUserToDeleteRows = true;    // chọn cả dòng (bấm đầu dòng) + Delete để xoá dòng
        dgvBatch.AllowUserToResizeRows = false;
        dgvBatch.SelectionMode = DataGridViewSelectionMode.RowHeaderSelect;
        dgvBatch.MultiSelect = true;
        dgvBatch.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        dgvBatch.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvBatch.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        dgvBatch.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        dgvBatch.RowHeadersWidth = 48;
        AppTheme.StyleGrid(dgvBatch);

        dgvBatch.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colName",
            HeaderText = "Tên hàng",
            FillWeight = 60,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        dgvBatch.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "colCode",
            HeaderText = "Mã hàng",
            FillWeight = 40,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        // Dán / nhập file hàng trăm dòng: các handler dưới đây bị tạm bỏ qua
        // (_bulkEditing) rồi chạy MỘT lần ở cuối — tránh đếm lại cả bảng và vẽ
        // lại preview sau từng ô.
        dgvBatch.CellValueChanged += (_, _) =>
        {
            if (_bulkEditing)
                return;
            UpdateBatchCount();
            PreviewFromGridRow();
        };
        dgvBatch.CurrentCellChanged += (_, _) =>
        {
            if (!_bulkEditing)
                PreviewFromGridRow();
        };
        dgvBatch.RowsRemoved += (_, _) =>
        {
            if (_bulkEditing)
                return;
            UpdateBatchCount();
            PreviewFromGridRow();
        };
        dgvBatch.RowsAdded += (_, _) =>
        {
            if (!_bulkEditing)
                UpdateBatchCount();
        };
        dgvBatch.RowPostPaint += BatchGrid_RowPostPaint;
        dgvBatch.KeyDown += BatchGrid_KeyDown;
        pnlCard.Controls.Add(dgvBatch);
    }

    // Số thứ tự ở đầu dòng — khớp với "Dòng N" trong các thông báo lỗi.
    private void BatchGrid_RowPostPaint(object? sender, DataGridViewRowPostPaintEventArgs e)
    {
        if (dgvBatch.Rows[e.RowIndex].IsNewRow)
            return;

        Rectangle header = new(e.RowBounds.Left, e.RowBounds.Top, dgvBatch.RowHeadersWidth - 4, e.RowBounds.Height);
        TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), AppTheme.Fonts.Hint, header,
            AppTheme.Colors.TextSecondary, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }

    private void RunBulkEdit(Action edit)
    {
        _bulkEditing = true;
        dgvBatch.SuspendLayout();
        try
        {
            edit();
        }
        finally
        {
            _bulkEditing = false;
            dgvBatch.ResumeLayout();
        }

        UpdateBatchCount();
        PreviewFromGridRow();
    }

    private void AddBatchRow()
    {
        dgvBatch.EndEdit();
        int index = dgvBatch.Rows.Add();
        dgvBatch.CurrentCell = dgvBatch.Rows[index].Cells[0];
        dgvBatch.Focus();
        dgvBatch.BeginEdit(true);
    }

    private void BatchGrid_KeyDown(object? sender, KeyEventArgs e)
    {
        if (dgvBatch.IsCurrentCellInEditMode || dgvBatch.ReadOnly)
            return;

        if (e.Control && e.KeyCode == Keys.V)
        {
            PasteIntoGrid();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Delete && dgvBatch.SelectedRows.Count == 0)
        {
            // Chọn ô (không phải cả dòng) + Delete: xoá nội dung ô.
            RunBulkEdit(() =>
            {
                foreach (DataGridViewCell cell in dgvBatch.SelectedCells)
                {
                    if (cell.OwningRow is { IsNewRow: false })
                        cell.Value = null;
                }
            });
            e.Handled = true;
        }
    }

    // Dán bảng copy từ Excel (các cột cách nhau bằng tab) bắt đầu từ ô đang
    // chọn; thiếu dòng thì thêm dòng. Dữ liệu dạng "Tên hàng | Mã hàng" (không
    // có tab, mọi dòng đều có '|') dán vào cột Tên hàng cũng được tách đúng 2 cột.
    private void PasteIntoGrid()
    {
        string text;
        try
        {
            text = Clipboard.GetText();
        }
        catch (ExternalException ex)
        {
            MessageBox.Show($"Không đọc được dữ liệu đã sao chép (clipboard đang bị ứng dụng khác giữ).\n{ex.Message}",
                "Dán dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrEmpty(text))
            return;

        List<string> lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        int startRow = dgvBatch.CurrentCell?.RowIndex ?? 0;
        int startCol = dgvBatch.CurrentCell?.ColumnIndex ?? 0;
        bool pipeSeparated = startCol == 0 && !text.Contains('\t') &&
                             lines.Where(l => l.Trim().Length > 0).All(l => l.Contains('|'));

        RunBulkEdit(() =>
        {
            for (int i = 0; i < lines.Count; i++)
            {
                int rowIndex = startRow + i;
                while (rowIndex >= dgvBatch.Rows.Count - 1)
                    dgvBatch.Rows.Add();

                string[] cells = pipeSeparated ? SplitAtLastPipe(lines[i]) : lines[i].Split('\t');
                for (int c = 0; c < cells.Length && startCol + c < dgvBatch.ColumnCount; c++)
                    dgvBatch.Rows[rowIndex].Cells[startCol + c].Value = cells[c].Trim();
            }
        });
    }

    // Tách ở dấu '|' CUỐI: tên hàng có thể chứa '|', mã hàng thì gần như không.
    private static string[] SplitAtLastPipe(string line)
    {
        int index = line.LastIndexOf('|');
        return index < 0 ? new[] { line } : new[] { line[..index], line[(index + 1)..] };
    }

    private static string CellText(DataGridViewRow row, int column) =>
        row.Cells[column].Value?.ToString() ?? "";
    #endregion

    #region Layout
    private void RelayoutAll()
    {
        int scrollY = pnlCard.AutoScrollPosition.Y;
        const int left = 32;
        const int gap = 12;
        int contentWidth = Math.Max(600, pnlCard.ClientSize.Width - 64);
        int right = left + contentWidth;

        pnlHeaderDivider.SetBounds(left, 96 + scrollY, contentWidth, 1);

        // Tên hàng + mã hàng + nút tạo
        int top = 116;
        const int generateWidth = 190;
        btnGenerate.SetBounds(right - generateWidth, top + 24 + scrollY, generateWidth, 40);
        int inputsWidth = Math.Max(200, btnGenerate.Left - gap - left);
        int nameWidth = (int)((inputsWidth - gap) * 0.58);
        int codeLeft = left + nameWidth + gap;
        lblNameTitle.SetBounds(left, top + scrollY, nameWidth, 16);
        lblCodeTitle.SetBounds(codeLeft, top + scrollY, 300, 16);
        top += 24;
        txtName.SetBounds(left, top + scrollY, nameWidth, 40);
        txtCode.SetBounds(codeLeft, top + scrollY, Math.Max(80, btnGenerate.Left - gap - codeLeft), 40);
        top += 40 + 24;

        // Hai cột: preview (trái) + tùy chỉnh (phải); hẹp quá thì xếp chồng.
        const int columnGap = 28;
        bool twoColumns = contentWidth >= 760;
        int previewWidth = twoColumns ? (int)((contentWidth - columnGap) * 0.55) : contentWidth;
        int settingsLeft = twoColumns ? left + previewWidth + columnGap : left;
        int settingsWidth = twoColumns ? right - settingsLeft : contentWidth;

        int previewBottom = LayoutPreview(left, top, previewWidth, scrollY);
        int settingsTop = twoColumns ? top : previewBottom + 24;
        int settingsBottom = LayoutSettings(settingsLeft, settingsTop, settingsWidth, scrollY);
        top = Math.Max(previewBottom, settingsBottom) + 28;

        top = LayoutBatch(left, top, contentWidth, scrollY);

        // Khoảng trống cuối để thanh cuộn kéo hết được nội dung.
        pnlCard.AutoScrollMinSize = new Size(0, top + 32);
    }

    private int LayoutPreview(int left, int top, int width, int scrollY)
    {
        lblPreviewTitle.SetBounds(left, top + scrollY, 300, 16);
        top += 24;

        const int previewHeight = 260;
        pnlPreview.SetBounds(left, top + scrollY, width, previewHeight);
        picPreview.SetBounds(16, 16, Math.Max(1, width - 32), previewHeight - 32);
        lblPreviewEmpty.SetBounds(16, 16, Math.Max(1, width - 32), previewHeight - 32);
        top += previewHeight + 8;

        lblPreviewInfo.SetBounds(left, top + scrollY, width, 18);
        top += 26;

        const int gap = 12;
        int buttonWidth = (width - (gap * 2)) / 3;
        btnSave.SetBounds(left, top + scrollY, buttonWidth, 40);
        btnCopy.SetBounds(left + buttonWidth + gap, top + scrollY, buttonWidth, 40);
        btnPrint.SetBounds(left + ((buttonWidth + gap) * 2), top + scrollY, width - ((buttonWidth + gap) * 2), 40);

        return top + 40;
    }

    private int LayoutSettings(int left, int top, int width, int scrollY)
    {
        lblSettingsTitle.SetBounds(left, top + scrollY, 300, 16);
        top += 24;

        const int colGap = 16;
        const int rowHeight = 58;
        int colWidth = (width - colGap) / 2;

        for (int i = 0; i < _settingFields.Count; i++)
        {
            SettingField field = _settingFields[i];
            int col = i % 2;
            int row = i / 2;
            int x = left + (col * (colWidth + colGap));
            int y = top + (row * rowHeight) + scrollY;

            field.Caption.SetBounds(x, y, colWidth, 18);
            field.Input.SetBounds(x, y + 20, colWidth, 30);
        }

        int rows = (_settingFields.Count + 1) / 2;
        top += rows * rowHeight + 4;

        btnResetSettings.SetBounds(left, top + scrollY, Math.Min(width, 210), 40);
        return top + 40;
    }

    private int LayoutBatch(int left, int top, int width, int scrollY)
    {
        const int gap = 12;
        int right = left + width;

        pnlBatchDivider.SetBounds(left, top + scrollY, width, 1);
        top += 20;

        lblBatchTitle.SetBounds(left, top + scrollY, 300, 16);
        top += 22;
        lblBatchHint.SetBounds(left, top + scrollY, width, 18);
        top += 28;

        // Bảng hàng hoá + nút thêm/mở/xoá (trái) + thao tác (phải)
        int listWidth = Math.Max(330, (int)(width * 0.5));
        int panelLeft = left + listWidth + 24;
        int panelWidth = right - panelLeft;
        const int gridHeight = 236;

        dgvBatch.SetBounds(left, top + scrollY, listWidth, gridHeight);
        int listButtonsTop = top + gridHeight + gap;
        int listButtonWidth = (listWidth - (gap * 2)) / 3;
        btnAddRow.SetBounds(left, listButtonsTop + scrollY, listButtonWidth, 40);
        btnImport.SetBounds(left + listButtonWidth + gap, listButtonsTop + scrollY, listButtonWidth, 40);
        btnClearBatch.SetBounds(left + ((listButtonWidth + gap) * 2), listButtonsTop + scrollY, listWidth - ((listButtonWidth + gap) * 2), 40);
        int listHeight = gridHeight + gap + 40;

        int y = top;
        lblBatchCount.SetBounds(panelLeft, y + 10 + scrollY, panelWidth, 20);
        y += 52;

        lblOutput.SetBounds(panelLeft, y + 10 + scrollY, 92, 20);
        btnChooseOutput.SetBounds(right - 110, y + scrollY, 110, 40);
        txtOutputFolder.SetBounds(panelLeft + 96, y + scrollY, Math.Max(1, btnChooseOutput.Left - gap - panelLeft - 96), 40);
        y += 52;

        lblFormat.SetBounds(panelLeft, y + 8 + scrollY, 92, 20);
        cboFormat.SetBounds(panelLeft + 96, y + scrollY, Math.Max(1, Math.Min(320, right - panelLeft - 96)), 32);
        y += 44;

        int buttonWidth = (panelWidth - (gap * 2)) / 3;
        btnBatchGenerate.SetBounds(panelLeft, y + scrollY, buttonWidth, 44);
        btnBatchPrint.SetBounds(panelLeft + buttonWidth + gap, y + scrollY, buttonWidth, 44);
        btnOpenOutput.SetBounds(panelLeft + ((buttonWidth + gap) * 2), y + scrollY, panelWidth - ((buttonWidth + gap) * 2), 44);
        y += 56;

        lblProgress.SetBounds(panelLeft, y + scrollY, panelWidth, 18);
        progressBar.SetBounds(panelLeft, y + 20 + scrollY, panelWidth, 10);
        y += 34;

        return Math.Max(top + listHeight, y);
    }

    private void StyleOverline(Label label, string text)
    {
        label.Text = text;
        label.Font = AppTheme.Fonts.Overline;
        label.ForeColor = AppTheme.Colors.TextMuted;
        pnlCard.Controls.Add(label);
    }
    #endregion

    #region Tạo / lưu / sao chép / in một mã
    // Xem trước theo ô nhập Tên hàng / Mã hàng (gọi mỗi lần gõ phím).
    private void PreviewFromInputs(bool showErrors)
    {
        // Mã hoá NGUYÊN VĂN nội dung ô nhập — không trim/đổi hoa thường.
        _previewFromGrid = false;
        ShowPreview(txtName.Text, txtCode.Text, showErrors);
        if (showErrors && Code128BarcodeService.Validate(txtCode.Text) != null)
            txtCode.Focus();
    }

    // Xem trước dòng đang chọn trong bảng HÀNG HÓA (khi chọn dòng / sửa ô).
    // Preview đang lấy từ bảng mà dòng đó bị xoá mã / bị xoá / bảng bị xoá
    // hết thì phải xoá preview — không để tem cũ còn hiển thị như đang hợp lệ.
    private void PreviewFromGridRow()
    {
        DataGridViewRow? row = dgvBatch.CurrentRow;
        string code = row == null || row.IsNewRow ? "" : CellText(row, 1);

        if (code.Length > 0)
        {
            _previewFromGrid = true;
            ShowPreview(CellText(row!, 0), code, showErrors: false);
        }
        else if (_previewFromGrid)
        {
            _previewFromGrid = false;
            _previewName = "";
            _previewCode = "";
            ShowEmptyPreview("Nhập tên hàng và mã hàng để xem trước tem.");
        }
    }

    // force = true khi đổi thông số (cùng tên/mã nhưng phải vẽ lại).
    private void ShowPreview(string name, string code, bool showErrors, bool force = false)
    {
        // Chỉ vẽ lại khi dữ liệu thực sự đổi — vd. di chuyển giữa 2 ô của cùng
        // một dòng trong bảng không cần render lại.
        if (!force && !showErrors && _current != null &&
            string.Equals(name, _previewName, StringComparison.Ordinal) &&
            string.Equals(code, _previewCode, StringComparison.Ordinal))
            return;

        _previewName = name;
        _previewCode = code;

        if (code.Length == 0)
        {
            if (showErrors)
            {
                string message = name.Trim().Length == 0 ? "Vui lòng nhập tên hàng và mã hàng." : "Vui lòng nhập mã hàng.";
                MessageBox.Show(message, "Thiếu dữ liệu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            ShowEmptyPreview("Nhập tên hàng và mã hàng để xem trước tem.");
            return;
        }

        string? error = Code128BarcodeService.Validate(code);
        if (error != null)
        {
            if (showErrors)
                MessageBox.Show(error, "Mã hàng không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowEmptyPreview(error);
            return;
        }

        try
        {
            BarcodeImage image = _service.Render(code, name, ReadOptions());
            SetCurrent(image);
        }
        catch (Exception ex)
        {
            if (showErrors)
                MessageBox.Show($"Không thể tạo mã vạch cho mã hàng này.\n\n{ex.Message}", "Không tạo được mã vạch",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowEmptyPreview(ex.Message);
        }
    }

    private void SetCurrent(BarcodeImage image)
    {
        BarcodeImage? old = _current;
        _current = image;

        picPreview.Image = image.Bitmap;
        picPreview.Visible = true;
        lblPreviewEmpty.Visible = false;
        old?.Dispose();

        BarcodeRenderOptions options = ReadOptions();
        string info = $"Code 128 · {image.WidthMm:0.0} × {image.HeightMm:0.0} mm · vạch {image.ModuleWidthMm:0.00} mm · {image.Dpi} DPI";
        if (options.TargetWidthMm > 0)
            info += $" · phần vạch {image.BarsWidthMm:0.0} mm";
        lblPreviewInfo.Text = info;

        UpdateSingleActions();
    }

    private void SaveCurrent()
    {
        if (_current == null)
            return;

        using SaveFileDialog dialog = new()
        {
            Title = "Lưu ảnh mã vạch",
            Filter = "PNG (khuyên dùng)|*.png|JPG|*.jpg",
            FilterIndex = 1,
            DefaultExt = "png",
            AddExtension = true,
            FileName = Code128BarcodeService.ToFileName(_current.Code) + ".png"
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        try
        {
            Code128BarcodeService.Save(_current, dialog.FileName);
            ToastForm.ShowSuccess("Đã lưu ảnh mã vạch");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không lưu được ảnh:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyCurrent()
    {
        if (_current == null)
            return;

        try
        {
            Clipboard.SetImage(_current.Bitmap);
            ToastForm.ShowSuccess("Đã sao chép mã vạch");
        }
        catch (ExternalException ex)
        {
            MessageBox.Show($"Không sao chép được (clipboard đang bị ứng dụng khác giữ).\n{ex.Message}",
                "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PrintCurrent()
    {
        if (_current == null)
            return;

        RunPrint(new[] { new BarcodeLabelItem(_current.Name, _current.Code) });
    }

    private void RunPrint(IReadOnlyList<BarcodeLabelItem> items)
    {
        try
        {
            if (PrintLabels(FindForm()!, items, ReadOptions()))
                ToastForm.ShowSuccess(items.Count == 1 ? "Đã gửi lệnh in" : $"Đã gửi lệnh in {items.Count} tem");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không in được:\n{ex.Message}", "Lỗi in", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Hiện PrintDialog cho chọn máy in/số bản rồi in. Trả về false nếu người
    /// dùng huỷ.
    /// </summary>
    private bool PrintLabels(IWin32Window owner, IReadOnlyList<BarcodeLabelItem> items, BarcodeRenderOptions options)
    {
        if (items.Count == 0)
            return false;

        using PrintDocument document = _printService.CreateDocument(items);

        using PrintDialog dialog = new()
        {
            Document = document,
            UseEXDialog = true,
            AllowSomePages = false,
            AllowSelection = false
        };

        if (dialog.ShowDialog(owner) != DialogResult.OK)
            return false;

        // PrintDocument gửi từng trang trên luồng UI (cơ chế của WinForms) —
        // hiện con trỏ chờ trong lúc kiểm tra và gửi lệnh in.
        Cursor.Current = Cursors.WaitCursor;
        PageFitCheck fit = _printService.CheckFitsPage(document, items, options);
        if (!fit.Fits)
        {
            DialogResult answer = MessageBox.Show(owner,
                $"Tem ({fit.LabelWidthMm:0.#} × {fit.LabelHeightMm:0.#} mm) lớn hơn vùng in của khổ giấy đang chọn " +
                $"({fit.PrintableWidthMm:0.#} × {fit.PrintableHeightMm:0.#} mm), phần thừa sẽ bị cắt.\n\n" +
                "Chương trình không tự thu nhỏ để tránh làm sai tỷ lệ vạch. Vẫn in?",
                "Tem lớn hơn khổ giấy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
                return false;
        }

        _printService.Print(document, items, options);
        return true;
    }
    #endregion

    #region Hàng loạt: nhập / tạo / in
    // Mỗi dòng của bảng là đúng một tem, giữ nguyên thứ tự (mã trùng vẫn in đủ
    // số tem). Mã hàng giữ nguyên văn như trong ô; dòng trống hoàn toàn bị bỏ
    // qua, dòng có tên mà thiếu mã vẫn được trả về để báo lỗi đúng dòng.
    private List<(int Line, BarcodeLabelItem Item)> ReadBatchItems()
    {
        List<(int, BarcodeLabelItem)> result = new();
        foreach (DataGridViewRow row in dgvBatch.Rows)
        {
            if (row.IsNewRow)
                continue;

            string name = CellText(row, 0);
            string code = CellText(row, 1);
            if (name.Trim().Length == 0 && code.Trim().Length == 0)
                continue;

            result.Add((row.Index + 1, new BarcodeLabelItem(name, code)));
        }

        return result;
    }

    private void UpdateBatchCount()
    {
        int count = ReadBatchItems().Count;
        lblBatchCount.Text = count == 0 ? "Chưa có hàng hoá" : $"{count} dòng → {count} tem";
    }

    private async Task ImportItemsAsync()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Mở danh sách hàng hoá (cột 1 = Tên hàng, cột 2 = Mã hàng)",
            Filter = "Excel / CSV|*.xlsx;*.xls;*.csv;*.txt|Tất cả file|*.*"
        };

        if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        // Đọc file trên luồng nền — file Excel lớn không làm đơ giao diện.
        string path = dialog.FileName;
        List<BarcodeLabelItem> items;
        btnImport.Enabled = false;
        UseWaitCursor = true;
        try
        {
            items = await Task.Run(() =>
            {
                string ext = Path.GetExtension(path);
                List<List<string>> table = ext.Equals(".csv", StringComparison.OrdinalIgnoreCase) || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                    ? Code128BarcodeService.ReadTableFromCsv(path)
                    : new ExcelService().ReadTextTable(path);
                return Code128BarcodeService.ParseLabelTable(table);
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể đọc file. Vui lòng kiểm tra định dạng dữ liệu.\n\nChi tiết: {ex.Message}",
                "Lỗi nhập file", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            UseWaitCursor = false;
            btnImport.Enabled = !_isBatchRunning;
        }

        if (items.Count == 0)
        {
            MessageBox.Show("File không có dòng hàng hoá nào.", "Mở Excel / CSV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        dgvBatch.EndEdit();
        if (ReadBatchItems().Count > 0)
        {
            DialogResult answer = MessageBox.Show(
                $"Đã đọc {items.Count} dòng hàng hoá.\n\nYes: thay danh sách hiện tại\nNo: thêm vào cuối danh sách",
                "Mở Excel / CSV", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (answer == DialogResult.Cancel)
                return;
            if (answer == DialogResult.Yes)
                dgvBatch.Rows.Clear();
        }

        int firstNewRow = 0;
        RunBulkEdit(() =>
        {
            // Xoá các dòng trống ở cuối trước khi thêm để danh sách liền mạch.
            for (int i = dgvBatch.Rows.Count - 2; i >= 0; i--)
            {
                DataGridViewRow row = dgvBatch.Rows[i];
                if (CellText(row, 0).Trim().Length > 0 || CellText(row, 1).Trim().Length > 0)
                    break;
                dgvBatch.Rows.RemoveAt(i);
            }

            firstNewRow = dgvBatch.Rows.Count - 1;
            foreach (BarcodeLabelItem item in items)
                dgvBatch.Rows.Add(item.Name, item.Code);
        });

        // Dòng lỗi (thiếu mã, ký tự không mã hoá được...) vẫn được nhập để sửa
        // ngay trong bảng — chỉ báo cho người dùng biết là dòng nào.
        List<string> problems = items
            .Select((item, i) => (Line: firstNewRow + i + 1, item.Code, Error: Code128BarcodeService.Validate(item.Code)))
            .Where(x => x.Error != null)
            .Select(x => $"Dòng {x.Line} \"{x.Code}\": {x.Error!.Replace("\n", " ")}")
            .ToList();

        if (problems.Count > 0)
        {
            string details = string.Join("\n", problems.Take(12)) + (problems.Count > 12 ? $"\n... và {problems.Count - 12} dòng khác" : "");
            MessageBox.Show($"Đã nhập {items.Count} dòng hàng hoá, trong đó {problems.Count} dòng cần sửa trước khi tạo/in:\n\n{details}",
                "Mở Excel / CSV", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            ToastForm.ShowSuccess($"Đã nhập {items.Count} dòng hàng hoá");
        }
    }

    private async Task GenerateBatchAsync()
    {
        if (_isBatchRunning)
            return;

        dgvBatch.EndEdit();
        List<(int Line, BarcodeLabelItem Item)> items = ReadBatchItems();
        if (items.Count == 0)
        {
            MessageBox.Show("Danh sách hàng hoá đang trống.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string folder = txtOutputFolder.Text;
        string extension = cboFormat.SelectedIndex == 1 ? ".jpg" : ".png";
        BarcodeRenderOptions options = ReadOptions();

        SetBatchRunning(true);
        progressBar.Value = 0;

        Progress<int> progress = new(done =>
        {
            progressBar.Value = Math.Min(100, done * 100 / items.Count);
            lblProgress.Text = $"Đang tạo {done}/{items.Count}...";
        });

        List<string> errors = new();
        int saved = 0;

        try
        {
            saved = await Task.Run(() => SaveBatch(items, options, folder, extension, errors, progress));
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
        }
        finally
        {
            SetBatchRunning(false);
        }

        progressBar.Value = 100;
        lblProgress.Text = $"Đã tạo {saved}/{items.Count} tem" + (errors.Count > 0 ? $" · {errors.Count} lỗi" : "") + $" → {folder}";

        if (errors.Count > 0)
        {
            string details = string.Join("\n", errors.Take(12)) + (errors.Count > 12 ? $"\n... và {errors.Count - 12} lỗi khác" : "");
            MessageBox.Show($"Đã tạo {saved}/{items.Count} tem.\n\nCác mã bị lỗi:\n{details}",
                "Tạo hàng loạt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        else
        {
            ToastForm.ShowSuccess($"Đã tạo {saved} tem");
        }
    }

    private int SaveBatch(List<(int Line, BarcodeLabelItem Item)> items, BarcodeRenderOptions options, string folder,
        string extension, List<string> errors, IProgress<int> progress)
    {
        Directory.CreateDirectory(folder);

        // Windows không phân biệt hoa/thường trong tên file: "sp1" và "SP1"
        // sẽ ghi đè nhau — thêm hậu tố _2, _3... vào TÊN FILE (không phải mã).
        HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
        int saved = 0;

        for (int i = 0; i < items.Count; i++)
        {
            (int line, BarcodeLabelItem item) = items[i];
            string code = item.Code;
            try
            {
                string? error = Code128BarcodeService.Validate(code);
                if (error != null)
                {
                    errors.Add($"Dòng {line} \"{code}\": {error.Replace("\n", " ")}");
                    continue;
                }

                string baseName = Code128BarcodeService.ToFileName(code);
                string name = baseName;
                for (int n = 2; !usedNames.Add(name); n++)
                    name = $"{baseName}_{n}";

                using BarcodeImage image = _service.Render(code, item.Name, options);
                Code128BarcodeService.Save(image, Path.Combine(folder, name + extension));
                saved++;
            }
            catch (Exception ex)
            {
                errors.Add($"Dòng {line} \"{code}\": {ex.Message}");
            }
            finally
            {
                progress.Report(i + 1);
            }
        }

        return saved;
    }

    private void PrintBatch()
    {
        dgvBatch.EndEdit();
        List<(int Line, BarcodeLabelItem Item)> items = ReadBatchItems();
        if (items.Count == 0)
        {
            MessageBox.Show("Danh sách hàng hoá đang trống.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        List<string> invalid = items
            .Select(x => (x.Line, x.Item.Code, Error: Code128BarcodeService.Validate(x.Item.Code)))
            .Where(x => x.Error != null)
            .Select(x => $"Dòng {x.Line} \"{x.Code}\": {x.Error!.Replace("\n", " ")}")
            .ToList();

        if (invalid.Count > 0)
        {
            MessageBox.Show("Sửa các mã hàng không hợp lệ trước khi in:\n\n" + string.Join("\n", invalid.Take(12)),
                "Mã hàng không hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        RunPrint(items.Select(x => x.Item).ToList());
    }

    private void SetBatchRunning(bool running)
    {
        _isBatchRunning = running;
        btnBatchGenerate.Enabled = !running;
        btnBatchPrint.Enabled = !running;
        btnImport.Enabled = !running;
        btnClearBatch.Enabled = !running;
        btnChooseOutput.Enabled = !running;
        cboFormat.Enabled = !running;
        btnAddRow.Enabled = !running;
        dgvBatch.ReadOnly = running;
    }

    private void ChooseOutputFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            SelectedPath = Directory.Exists(txtOutputFolder.Text) ? txtOutputFolder.Text : ""
        };

        if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
            txtOutputFolder.Text = dialog.SelectedPath;
    }

    private void OpenOutputFolder()
    {
        try
        {
            Directory.CreateDirectory(txtOutputFolder.Text);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{txtOutputFolder.Text}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không mở được thư mục:\n{ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            picPreview.Image = null;
            _current?.Dispose();
            _current = null;
        }

        base.Dispose(disposing);
    }

    // Bảng HÀNG HÓA: Enter đi theo thứ tự nhập liệu Tên hàng → Mã hàng → Tên
    // hàng của dòng kế tiếp (dòng trống cuối bảng tự thành dòng mới khi gõ),
    // thay vì mặc định của DataGridView là nhảy xuống cùng cột.
    private sealed class BatchGrid : SmoothDataGridView
    {
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Enter && MoveToNextInputCell())
                return true;

            return base.ProcessDialogKey(keyData);
        }

        protected override bool ProcessDataGridViewKey(KeyEventArgs e)
        {
            if (e.KeyData == Keys.Enter && MoveToNextInputCell())
                return true;

            return base.ProcessDataGridViewKey(e);
        }

        private bool MoveToNextInputCell()
        {
            DataGridViewCell? cell = CurrentCell;
            if (cell == null)
                return false;

            int row = cell.RowIndex;
            int column = cell.ColumnIndex;
            if (IsCurrentCellInEditMode && !EndEdit())
                return true;

            // EndEdit trên dòng trống cuối bảng đã biến nó thành dòng thật và
            // thêm một dòng trống mới bên dưới.
            if (column < ColumnCount - 1)
                CurrentCell = Rows[row].Cells[column + 1];
            else if (row + 1 < Rows.Count)
                CurrentCell = Rows[row + 1].Cells[0];

            return true;
        }
    }

    // Ô xem trước: thu nhỏ thì dùng nội suy chất lượng cao, phóng to thì
    // phóng theo bội số nguyên + NearestNeighbor để vạch luôn sắc cạnh (PictureBox
    // Zoom làm nhoè vạch khi phóng to). Chỉ ảnh hưởng hiển thị, không ảnh hưởng
    // ảnh lưu/in.
    private sealed class BarcodePreviewBox : Control
    {
        private Image? _image;

        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public Image? Image
        {
            get => _image;
            set
            {
                _image = value;
                Invalidate();
            }
        }

        public BarcodePreviewBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            if (_image == null || Width <= 0 || Height <= 0)
                return;

            float scale = Math.Min((float)Width / _image.Width, (float)Height / _image.Height);
            if (scale >= 1f)
            {
                scale = (float)Math.Floor(scale);
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            }
            else
            {
                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            }

            int w = (int)Math.Round(_image.Width * scale);
            int h = (int)Math.Round(_image.Height * scale);
            Rectangle dest = new((Width - w) / 2, (Height - h) / 2, w, h);
            e.Graphics.DrawImage(_image, dest, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel);
        }
    }
}
