using System.Diagnostics;
using System.Globalization;
using KiotVietLabelPrinter.Forms;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.Fonts;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using KiotVietLabelPrinter.UI;
using NPOI.SS.Util;

namespace KiotVietLabelPrinter.Views;

// Màn hình "Phiếu xuất kho" — nhúng vào FormMain giống các công cụ khác (xem
// FormMain.OpenWarehouseExport). Màn hình chỉ thu thập lựa chọn, hiển thị và
// hỏi xác nhận; đọc/đối chiếu/ghi Excel nằm ở WarehouseDocumentService, xem
// trước bản in ở WarehousePrintPreviewService (+ FormWarehousePrintPreview),
// in ở WarehousePrinterService. Máy in / font của màn hình này lưu riêng trong
// WarehouseSettingsStore, không đụng tới cấu hình in tem. Thiết lập trang in
// tuỳ chỉnh KHÔNG được lưu: mỗi lần mở / đổi file mẫu đều về "giữ nguyên mẫu".
public class WarehouseExportView : UserControl
{
    private const string Title = "Phiếu xuất kho";

    private readonly WarehouseDocumentService _documentService = new();
    private readonly WarehousePrinterService _printerService = new();
    private readonly WarehousePrintPreviewService _previewService = new();
    private readonly InstalledFontService _fontService = new();
    private readonly WarehouseSettingsStore _settingsStore = new();
    private readonly WarehouseExportSettings _settings;

    private WarehouseTemplateLayout? _layout;
    private WarehouseDocument? _document;
    private WarehouseExportResult? _lastExport;
    private bool _exportStale;

    /// <summary>Thiết lập trang in của file mẫu — gốc để biết người dùng đã đổi mục nào.</summary>
    private WarehousePageSetup? _templatePage;

    /// <summary>Dấu file của lần xuất đã được xem trước bản in (để gợi ý xem trước trước khi in).</summary>
    private WarehouseFileStamp? _previewedStamp;
    private bool _busy;
    private bool _loadingUi;
    private Font? _previewFont;

    /// <summary>Đang xuất / in — FormMain hỏi lại trước khi đóng app.</summary>
    public bool IsBusy => _busy;

    private readonly CardPanel pnlCard;
    private bool _fillingGrid;

    /// <summary>
    /// Khung cuộn của màn hình. Lúc đang bận (khóa các ô nhập) hoặc đang nạp
    /// bảng, WinForms chuyển focus sang control khác và mặc định tự cuộn tới
    /// control đó — màn hình nhảy lung tung. Chỉ chặn tự cuộn trong lúc đó;
    /// bình thường (Tab giữa các ô) vẫn cuộn theo như cũ.
    /// </summary>
    private sealed class CardPanel(Func<bool> suppressAutoScroll) : RoundedPanel
    {
        protected override Point ScrollToControl(Control activeControl) =>
            suppressAutoScroll() ? DisplayRectangle.Location : base.ScrollToControl(activeControl);
    }

    // Header
    private readonly RoundedPanel pnlIconBadge = new();
    private readonly IconGlyph iconHeader = new();
    private readonly Label lblHeaderTitle = new();
    private readonly Label lblHeaderSubtitle = new();
    private readonly Panel pnlHeaderDivider = new();

    // Thông tin phiếu
    private readonly Label lblTicketTitle = new();
    private readonly Label lblPrefix = new();
    private readonly RoundedTextBox txtPrefix = new();
    private readonly Label lblTicketCode = new();
    private readonly RoundedTextBox txtTicketCode = new();
    private readonly Label lblFullCode = new();
    private readonly Label lblDataState = new();

    // File đầu vào + output
    private readonly Label lblFilesTitle = new();
    private readonly RoundedButton btnChooseTemplate = new();
    private readonly RoundedTextBox txtTemplate = new();
    private readonly Label lblTemplateInfo = new();
    private readonly RoundedButton btnChooseGoods = new();
    private readonly RoundedTextBox txtGoods = new();
    private readonly Label lblGoodsInfo = new();
    private readonly RoundedButton btnChooseOutput = new();
    private readonly RoundedTextBox txtOutput = new();
    private readonly Label lblOutputInfo = new();

    // Máy in
    private readonly Label lblPrinterTitle = new();
    private readonly ComboBox cboPrinter = new();
    private readonly RoundedButton btnRefreshPrinters = new();
    private readonly Label lblPrinterStatus = new();

    // Font chữ
    private readonly Label lblFontTitle = new();
    private readonly Label lblScope = new();
    private readonly ComboBox cboScope = new();
    private readonly Label lblFontFamily = new();
    private readonly ComboBox cboFont = new();
    private readonly Label lblFontSize = new();
    private readonly NumericUpDown nudFontSize = new();
    private readonly Label lblFontPreview = new();
    private readonly Label lblFontWarning = new();

    // Khổ giấy và thiết lập in
    private readonly Label lblPageTitle = new();
    private readonly Label lblPageMode = new();
    private readonly ComboBox cboPageMode = new();
    private readonly Label lblPaper = new();
    private readonly ComboBox cboPaper = new();
    private readonly Label lblOrientation = new();
    private readonly ComboBox cboOrientation = new();
    private readonly Label lblScaleMode = new();
    private readonly ComboBox cboScaleMode = new();
    private readonly NumericUpDown nudScale = new();
    private readonly Label lblMargins = new();
    private readonly NumericUpDown nudMarginLeft = new();
    private readonly NumericUpDown nudMarginRight = new();
    private readonly NumericUpDown nudMarginTop = new();
    private readonly NumericUpDown nudMarginBottom = new();
    private readonly RoundedButton btnLoadPage = new();
    private readonly RoundedButton btnRestorePage = new();
    private readonly Label lblPageInfo = new();
    private readonly Label lblPageWarning = new();
    private readonly List<short> _paperCodes = new();
    private readonly List<WarehouseScaleChoice?> _scaleChoices = new();

    // Xem trước
    private readonly Label lblPreviewTitle = new();
    private readonly Label lblSummary = new();
    private readonly SmoothDataGridView dgvRows = new();

    // Thao tác
    private readonly RoundedButton btnValidate = new();
    private readonly RoundedButton btnPreview = new();
    private readonly RoundedButton btnExport = new();
    private readonly RoundedButton btnPrintPreview = new();
    private readonly RoundedButton btnPrint = new();
    private readonly RoundedButton btnClear = new();
    private readonly RoundedButton btnOpenFolder = new();
    private readonly Label lblStatus = new();

    private static readonly (WarehouseFontApplyScope Scope, string Text)[] Scopes =
    [
        (WarehouseFontApplyScope.KeepTemplate, "Giữ nguyên định dạng của file mẫu"),
        (WarehouseFontApplyScope.ProductNameOnly, "Chỉ áp dụng cho tên hàng hóa"),
        (WarehouseFontApplyScope.ProductTable, "Áp dụng cho toàn bộ bảng hàng hóa"),
        (WarehouseFontApplyScope.WholeDocument, "Áp dụng cho toàn bộ nội dung phiếu")
    ];

    private static readonly Color WarningBack = Color.FromArgb(255, 247, 230);
    private static readonly Color WarningText = Color.FromArgb(160, 98, 0);

    public WarehouseExportView()
    {
        Dock = DockStyle.Fill;
        pnlCard = new CardPanel(() => _busy || _fillingGrid);
        _settings = _settingsStore.Load();

        BuildUi();
        LoadSettingsIntoUi();
        UpdateState();
    }

    /// <summary>Gọi khi FormMain mở màn hình — hiện cảnh báo đọc thiết lập (nếu có) đúng một lần.</summary>
    public void OnShown()
    {
        if (_settingsStore.LoadWarning is { } warning)
        {
            MessageBox.Show(warning, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _settingsStore.Save(_settings);
        }
    }

    #region Dựng giao diện
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
        BuildTicketSection();
        BuildFilesSection();
        BuildPrinterSection();
        BuildFontSection();
        BuildPageSection();
        BuildPreviewSection();
        BuildActions();

        pnlCard.SizeChanged += (_, _) => RelayoutAll();
        RelayoutAll();
    }

    private void BuildHeader()
    {
        pnlIconBadge.SetBounds(32, 32, 44, 44);
        pnlIconBadge.CornerRadius = 13;
        pnlIconBadge.FillColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.BorderThickness = 0;
        pnlIconBadge.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.Controls.Add(pnlIconBadge);

        iconHeader.Dock = DockStyle.Fill;
        iconHeader.Kind = IconGlyphs.Kind.Document;
        iconHeader.IconColor = AppTheme.Colors.Primary;
        iconHeader.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.Controls.Add(iconHeader);

        lblHeaderTitle.Text = "PHIẾU XUẤT KHO";
        lblHeaderTitle.SetBounds(88, 32, 500, 26);
        lblHeaderTitle.Font = AppTheme.Fonts.SectionTitle;
        lblHeaderTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCard.Controls.Add(lblHeaderTitle);

        lblHeaderSubtitle.Text = "Điền mã phiếu, tên hàng, đơn vị và số lượng vào chính file phiếu mẫu — giữ nguyên bố cục, định dạng và thiết lập in.";
        lblHeaderSubtitle.SetBounds(88, 58, 800, 18);
        lblHeaderSubtitle.Font = AppTheme.Fonts.Hint;
        lblHeaderSubtitle.ForeColor = AppTheme.Colors.TextMuted;
        lblHeaderSubtitle.AutoEllipsis = true;
        pnlCard.Controls.Add(lblHeaderSubtitle);

        pnlHeaderDivider.Height = 1;
        pnlHeaderDivider.BackColor = AppTheme.Colors.Border;
        pnlCard.Controls.Add(pnlHeaderDivider);
    }

    private void BuildTicketSection()
    {
        StyleOverline(lblTicketTitle, "THÔNG TIN PHIẾU");
        StyleCaption(lblPrefix, "Tiền tố");
        StyleCaption(lblTicketCode, "Mã phiếu");

        StyleTextBox(txtPrefix, "XK");
        txtPrefix.TextChanged += (_, _) => OnTicketInputChanged();
        txtPrefix.Leave += (_, _) => SaveSettings();

        StyleTextBox(txtTicketCode, "VD: 23609");
        txtTicketCode.TextChanged += (_, _) => OnTicketInputChanged();

        lblFullCode.Font = AppTheme.Fonts.BodyBold;
        lblFullCode.ForeColor = AppTheme.Colors.TextPrimary;
        lblFullCode.AutoEllipsis = true;
        pnlCard.Controls.Add(lblFullCode);

        lblDataState.Font = AppTheme.Fonts.Hint;
        lblDataState.ForeColor = AppTheme.Colors.TextSecondary;
        lblDataState.AutoEllipsis = true;
        pnlCard.Controls.Add(lblDataState);
    }

    private void BuildFilesSection()
    {
        StyleOverline(lblFilesTitle, "FILE ĐẦU VÀO VÀ THƯ MỤC OUTPUT");

        StyleFileButton(btnChooseTemplate, "CHỌN FILE PHIẾU MẪU", IconGlyphs.Kind.Document, ChooseTemplate);
        StyleTextBox(txtTemplate, "Chưa chọn file phiếu xuất kho mẫu (.xlsx / .xls)");
        txtTemplate.ReadOnly = true;
        StyleInfo(lblTemplateInfo);

        StyleFileButton(btnChooseGoods, "CHỌN FILE HÀNG HÓA", IconGlyphs.Kind.Document, ChooseGoods);
        StyleTextBox(txtGoods, "Chưa chọn file hàng hóa (file Chi tiết chuyển hàng từ KiotViet)");
        txtGoods.ReadOnly = true;
        StyleInfo(lblGoodsInfo);

        StyleFileButton(btnChooseOutput, "CHỌN THƯ MỤC OUTPUT", IconGlyphs.Kind.Folder, ChooseOutputFolder);
        StyleTextBox(txtOutput, "Chưa chọn thư mục lưu file phiếu");
        txtOutput.ReadOnly = true;
        StyleInfo(lblOutputInfo);
    }

    private void BuildPrinterSection()
    {
        StyleOverline(lblPrinterTitle, "MÁY IN PHIẾU (RIÊNG, KHÔNG ẢNH HƯỞNG MÁY IN TEM)");

        cboPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        AppTheme.StyleComboBox(cboPrinter);
        cboPrinter.SelectedIndexChanged += (_, _) =>
        {
            if (_loadingUi)
                return;
            _settings.PrinterName = cboPrinter.SelectedItem as string ?? "";
            SaveSettings();
            UpdateState();
        };
        pnlCard.Controls.Add(cboPrinter);

        btnRefreshPrinters.Text = "Làm mới";
        btnRefreshPrinters.Icon = IconGlyphs.Kind.Refresh;
        btnRefreshPrinters.Variant = ButtonVariant.Outline;
        btnRefreshPrinters.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnRefreshPrinters.Click += (_, _) => LoadPrinters();
        pnlCard.Controls.Add(btnRefreshPrinters);

        StyleInfo(lblPrinterStatus);
    }

    private void BuildFontSection()
    {
        StyleOverline(lblFontTitle, "FONT CHỮ VÀ CỠ CHỮ");
        StyleCaption(lblScope, "Phạm vi áp dụng");
        StyleCaption(lblFontFamily, "Font chữ");
        StyleCaption(lblFontSize, "Cỡ chữ (pt)");

        cboScope.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach ((_, string text) in Scopes)
            cboScope.Items.Add(text);
        AppTheme.StyleComboBox(cboScope);
        cboScope.SelectedIndexChanged += (_, _) => OnFontChanged();
        pnlCard.Controls.Add(cboScope);

        cboFont.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string family in _fontService.GetInstalledFamilies())
            cboFont.Items.Add(family);
        AppTheme.StyleComboBox(cboFont);
        cboFont.SelectedIndexChanged += (_, _) => OnFontChanged();
        pnlCard.Controls.Add(cboFont);

        nudFontSize.Minimum = (decimal)WarehouseFontSettings.MinFontSize;
        nudFontSize.Maximum = (decimal)WarehouseFontSettings.MaxFontSize;
        nudFontSize.Increment = 0.5m;
        nudFontSize.DecimalPlaces = 1;
        nudFontSize.Value = 12;
        nudFontSize.BorderStyle = BorderStyle.FixedSingle;
        nudFontSize.Font = AppTheme.Fonts.Body;
        nudFontSize.TextAlign = HorizontalAlignment.Right;
        nudFontSize.ValueChanged += (_, _) => OnFontChanged();
        pnlCard.Controls.Add(nudFontSize);

        lblFontPreview.Text = "Giày nữ Pucini · Kính mắt · Đôi / Cái · ẮẰẲẴẶ ỨỪỬỮỰ ỳỹ";
        lblFontPreview.BackColor = AppTheme.Colors.GridHeaderBack;
        lblFontPreview.ForeColor = AppTheme.Colors.TextPrimary;
        lblFontPreview.TextAlign = ContentAlignment.MiddleLeft;
        lblFontPreview.Padding = new Padding(10, 0, 10, 0);
        lblFontPreview.AutoEllipsis = true;
        pnlCard.Controls.Add(lblFontPreview);

        StyleInfo(lblFontWarning);
    }

    private void BuildPageSection()
    {
        StyleOverline(lblPageTitle, "KHỔ GIẤY VÀ THIẾT LẬP IN");
        StyleCaption(lblPageMode, "Chế độ");
        StyleCaption(lblPaper, "Khổ giấy");
        StyleCaption(lblOrientation, "Hướng giấy");
        StyleCaption(lblScaleMode, "Tỷ lệ in");
        StyleCaption(lblMargins, "Lề trang (cm): Trái · Phải · Trên · Dưới");

        cboPageMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPageMode.Items.AddRange(["Giữ nguyên thiết lập in của file mẫu", "Tùy chỉnh (chỉ đổi mục khác mẫu)"]);
        cboPageMode.SelectedIndex = 0;
        AppTheme.StyleComboBox(cboPageMode);
        cboPageMode.SelectedIndexChanged += (_, _) => OnPageSettingsChanged();
        pnlCard.Controls.Add(cboPageMode);

        foreach (ComboBox combo in new[] { cboPaper, cboOrientation, cboScaleMode })
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            AppTheme.StyleComboBox(combo);
            combo.SelectedIndexChanged += (_, _) => OnPageSettingsChanged();
            pnlCard.Controls.Add(combo);
        }
        cboOrientation.Items.AddRange(["Dọc", "Ngang"]);

        nudScale.Value = 100;
        StyleNumber(nudScale, WarehousePageSettings.MinScalePercent, WarehousePageSettings.MaxScalePercent, 0, 5);
        foreach (NumericUpDown margin in new[] { nudMarginLeft, nudMarginRight, nudMarginTop, nudMarginBottom })
            StyleNumber(margin, 0, 10, 2, 0.1m);

        StyleAction(btnLoadPage, "LẤY THIẾT LẬP TỪ FILE MẪU", IconGlyphs.Kind.Refresh, ButtonVariant.Outline, async () => await LoadPageFromTemplateAsync());
        StyleAction(btnRestorePage, "KHÔI PHỤC THIẾT LẬP MẪU", IconGlyphs.Kind.ArrowLeft, ButtonVariant.Outline, RestoreTemplatePage);

        StyleInfo(lblPageInfo);
        lblPageInfo.AutoEllipsis = false;
        StyleInfo(lblPageWarning);
    }

    private void StyleNumber(NumericUpDown box, decimal min, decimal max, int decimals, decimal increment)
    {
        box.Minimum = min;
        box.Maximum = max;
        box.DecimalPlaces = decimals;
        box.Increment = increment;
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = AppTheme.Fonts.Body;
        box.TextAlign = HorizontalAlignment.Right;
        box.ValueChanged += (_, _) => OnPageSettingsChanged();
        pnlCard.Controls.Add(box);
    }

    private void BuildPreviewSection()
    {
        StyleOverline(lblPreviewTitle, "XEM TRƯỚC DỮ LIỆU  (SỬA ĐƯỢC TÊN HÀNG MỚI, ĐƠN VỊ, SỐ LƯỢNG)");

        lblSummary.Font = AppTheme.Fonts.BodyBold;
        lblSummary.ForeColor = AppTheme.Colors.TextSecondary;
        lblSummary.AutoEllipsis = true;
        pnlCard.Controls.Add(lblSummary);

        dgvRows.AllowUserToAddRows = false;
        dgvRows.AllowUserToDeleteRows = false;
        dgvRows.AllowUserToResizeRows = false;
        dgvRows.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvRows.MultiSelect = false;
        dgvRows.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        dgvRows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvRows.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
        dgvRows.RowHeadersWidth = 44;
        dgvRows.ShowCellToolTips = true;
        AppTheme.StyleGrid(dgvRows);

        AddColumn("colSourceRow", "Dòng", 5, readOnly: true, minWidth: 64);
        AddColumn("colCode", "Mã hàng", 10, readOnly: true, minWidth: 96);
        AddColumn("colSourceName", "Tên hàng gốc", 27, readOnly: true);
        AddColumn("colTicketName", "Tên hàng mới (ghi lên phiếu)", 27, readOnly: false);
        DataGridViewComboBoxColumn unit = new()
        {
            Name = "colUnit",
            HeaderText = "Đơn vị",
            FillWeight = 8,
            MinimumWidth = 78,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        unit.Items.AddRange(WarehouseUnitService.PairUnit, WarehouseUnitService.PieceUnit);
        dgvRows.Columns.Add(unit);
        AddColumn("colRequested", "SL yêu cầu", 9, readOnly: false, numeric: true, minWidth: 104);
        AddColumn("colActual", "SL thực xuất", 9, readOnly: false, numeric: true, minWidth: 112);
        AddColumn("colStatus", "Trạng thái", 25, readOnly: true);

        dgvRows.CellEndEdit += Grid_CellEndEdit;
        dgvRows.CurrentCellDirtyStateChanged += (_, _) =>
        {
            // Combo đơn vị: ghi nhận ngay khi chọn, không đợi rời ô.
            if (dgvRows.IsCurrentCellDirty && dgvRows.CurrentCell is DataGridViewComboBoxCell)
                dgvRows.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        dgvRows.CellValueChanged += (_, e) =>
        {
            if (e.RowIndex >= 0 && dgvRows.Columns[e.ColumnIndex].Name == "colUnit")
                Grid_CellEndEdit(dgvRows, e);
        };
        dgvRows.DataError += (_, e) => e.ThrowException = false;
        pnlCard.Controls.Add(dgvRows);
    }

    private void AddColumn(string name, string header, float weight, bool readOnly, bool numeric = false, int minWidth = 60)
    {
        DataGridViewTextBoxColumn column = new()
        {
            Name = name,
            HeaderText = header,
            FillWeight = weight,
            MinimumWidth = minWidth,
            ReadOnly = readOnly,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (numeric)
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        if (name is "colSourceName" or "colTicketName" or "colStatus")
            column.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        dgvRows.Columns.Add(column);
    }

    private void BuildActions()
    {
        StyleAction(btnValidate, "KIỂM TRA DỮ LIỆU", IconGlyphs.Kind.ShieldCheck, ButtonVariant.Outline, ValidateAndShow);
        StyleAction(btnPreview, "XEM TRƯỚC DỮ LIỆU", IconGlyphs.Kind.Eye, ButtonVariant.Outline, async () => await LoadDocumentAsync(showErrors: true));
        StyleAction(btnExport, "XUẤT PHIẾU EXCEL", IconGlyphs.Kind.Document, ButtonVariant.Primary, async () => await ExportAsync());
        StyleAction(btnPrintPreview, "XEM TRƯỚC BẢN IN", IconGlyphs.Kind.Eye, ButtonVariant.Outline, async () => await PrintPreviewAsync());
        StyleAction(btnPrint, "IN PHIẾU", IconGlyphs.Kind.Printer, ButtonVariant.Outline, async () => await PrintAsync(FindForm(), null));
        StyleAction(btnClear, "XÓA DỮ LIỆU", IconGlyphs.Kind.Trash, ButtonVariant.Outline, ClearData);
        StyleAction(btnOpenFolder, "Mở thư mục", IconGlyphs.Kind.Folder, ButtonVariant.Outline, OpenOutputFolder);

        lblStatus.Font = AppTheme.Fonts.Hint;
        lblStatus.ForeColor = AppTheme.Colors.TextSecondary;
        lblStatus.AutoEllipsis = true;
        lblStatus.Text = "Sẵn sàng.";
        pnlCard.Controls.Add(lblStatus);
    }
    #endregion

    #region Layout
    private void RelayoutAll()
    {
        int scrollY = pnlCard.AutoScrollPosition.Y;
        const int left = 32;
        const int gap = 12;
        int width = Math.Max(640, pnlCard.ClientSize.Width - 64);
        int right = left + width;

        pnlHeaderDivider.SetBounds(left, 96 + scrollY, width, 1);
        int top = 116;

        // Thông tin phiếu: [Tiền tố] [Mã phiếu] [Mã hoàn chỉnh + trạng thái]
        lblTicketTitle.SetBounds(left, top + scrollY, 400, 16);
        top += 24;
        const int prefixWidth = 90;
        int codeWidth = Math.Min(260, (width - prefixWidth - gap * 2) / 2);
        int codeLeft = left + prefixWidth + gap;
        int infoLeft = codeLeft + codeWidth + gap * 2;
        lblPrefix.SetBounds(left, top + scrollY, prefixWidth, 18);
        lblTicketCode.SetBounds(codeLeft, top + scrollY, codeWidth, 18);
        txtPrefix.SetBounds(left, top + 20 + scrollY, prefixWidth, 40);
        txtTicketCode.SetBounds(codeLeft, top + 20 + scrollY, codeWidth, 40);
        lblFullCode.SetBounds(infoLeft, top + 22 + scrollY, right - infoLeft, 20);
        lblDataState.SetBounds(infoLeft, top + 42 + scrollY, right - infoLeft, 18);
        top += 20 + 40 + 24;

        // File đầu vào + output: [nút] [đường dẫn] / dòng thông tin
        lblFilesTitle.SetBounds(left, top + scrollY, 400, 16);
        top += 24;
        const int fileButtonWidth = 230;
        foreach ((RoundedButton button, RoundedTextBox box, Label info) in new[]
                 {
                     (btnChooseTemplate, txtTemplate, lblTemplateInfo),
                     (btnChooseGoods, txtGoods, lblGoodsInfo),
                     (btnChooseOutput, txtOutput, lblOutputInfo)
                 })
        {
            button.SetBounds(left, top + scrollY, fileButtonWidth, 40);
            box.SetBounds(left + fileButtonWidth + gap, top + scrollY, width - fileButtonWidth - gap, 40);
            info.SetBounds(left + fileButtonWidth + gap + 4, top + 42 + scrollY, width - fileButtonWidth - gap - 4, 18);
            top += 40 + 26;
        }
        top += 12;

        // Hai cột: máy in (trái) + font (phải); hẹp quá thì xếp chồng.
        const int columnGap = 28;
        bool twoColumns = width >= 900;
        int printerWidth = twoColumns ? (int)((width - columnGap) * 0.42) : width;
        int fontLeft = twoColumns ? left + printerWidth + columnGap : left;
        int fontWidth = twoColumns ? right - fontLeft : width;

        int printerBottom = LayoutPrinter(left, top, printerWidth, scrollY);
        int fontTop = twoColumns ? top : printerBottom + 20;
        int fontBottom = LayoutFont(fontLeft, fontTop, fontWidth, scrollY);
        top = Math.Max(printerBottom, fontBottom) + 24;

        top = LayoutPage(left, top, width, scrollY) + 24;

        // Xem trước
        lblPreviewTitle.SetBounds(left, top + scrollY, width, 16);
        top += 22;
        lblSummary.SetBounds(left, top + scrollY, width, 20);
        top += 26;
        int gridHeight = Math.Max(260, Math.Min(460, pnlCard.ClientSize.Height - 360));
        dgvRows.SetBounds(left, top + scrollY, width, gridHeight);
        top += gridHeight + 16;

        // Thao tác
        RoundedButton[] actions = [btnValidate, btnPreview, btnExport, btnPrintPreview, btnPrint, btnClear, btnOpenFolder];
        int perRow = width >= 1180 ? actions.Length : 4;
        int buttonWidth = (width - gap * (perRow - 1)) / perRow;
        for (int i = 0; i < actions.Length; i++)
        {
            int row = i / perRow;
            int col = i % perRow;
            actions[i].SetBounds(left + col * (buttonWidth + gap), top + row * 56 + scrollY, buttonWidth, 44);
        }
        top += ((actions.Length + perRow - 1) / perRow) * 56;

        lblStatus.SetBounds(left, top + scrollY, width, 18);
        top += 30;

        pnlCard.AutoScrollMinSize = new Size(0, top + 24);
    }

    private int LayoutPrinter(int left, int top, int width, int scrollY)
    {
        lblPrinterTitle.SetBounds(left, top + scrollY, width, 16);
        top += 24;
        const int refreshWidth = 120;
        cboPrinter.SetBounds(left, top + 4 + scrollY, width - refreshWidth - 12, 32);
        btnRefreshPrinters.SetBounds(left + width - refreshWidth, top + scrollY, refreshWidth, 40);
        top += 46;
        lblPrinterStatus.SetBounds(left, top + scrollY, width, 36);
        return top + 36;
    }

    private int LayoutPage(int left, int top, int width, int scrollY)
    {
        const int gap = 12;
        lblPageTitle.SetBounds(left, top + scrollY, width, 16);
        top += 24;

        // Dòng 1: [Chế độ] [Khổ giấy] [Hướng] [Tỷ lệ] [%]
        int modeWidth = (int)((width - gap * 4) * 0.30);
        int paperWidth = (int)((width - gap * 4) * 0.28);
        int orientationWidth = (int)((width - gap * 4) * 0.12);
        int scaleWidth = (int)((width - gap * 4) * 0.20);
        int x = left;
        foreach ((Label caption, Control input, int w) in new (Label, Control, int)[]
                 {
                     (lblPageMode, cboPageMode, modeWidth),
                     (lblPaper, cboPaper, paperWidth),
                     (lblOrientation, cboOrientation, orientationWidth),
                     (lblScaleMode, cboScaleMode, scaleWidth)
                 })
        {
            caption.SetBounds(x, top + scrollY, w, 18);
            input.SetBounds(x, top + 20 + scrollY, w, 32);
            x += w + gap;
        }
        nudScale.SetBounds(x, top + 21 + scrollY, left + width - x, 30);
        top += 20 + 32 + 12;

        // Dòng 2: 4 ô lề + 2 nút lấy / khôi phục thiết lập mẫu
        lblMargins.SetBounds(left, top + scrollY, 420, 18);
        x = left;
        foreach (NumericUpDown margin in new[] { nudMarginLeft, nudMarginRight, nudMarginTop, nudMarginBottom })
        {
            margin.SetBounds(x, top + 21 + scrollY, 86, 30);
            x += 86 + 8;
        }
        int buttonWidth = Math.Min(250, (left + width - x - gap * 2) / 2);
        btnRestorePage.SetBounds(left + width - buttonWidth, top + 16 + scrollY, buttonWidth, 40);
        btnLoadPage.SetBounds(btnRestorePage.Left - gap - buttonWidth, top + 16 + scrollY, buttonWidth, 40);
        top += 20 + 40 + 8;

        lblPageInfo.SetBounds(left, top + scrollY, width, 36);
        top += 38;
        lblPageWarning.SetBounds(left, top + scrollY, width, 18);
        return top + 18;
    }

    private int LayoutFont(int left, int top, int width, int scrollY)
    {
        lblFontTitle.SetBounds(left, top + scrollY, width, 16);
        top += 24;
        const int gap = 12;
        int scopeWidth = (int)((width - gap * 2) * 0.44);
        int fontWidth = (int)((width - gap * 2) * 0.36);
        int sizeLeft = left + scopeWidth + fontWidth + gap * 2;
        lblScope.SetBounds(left, top + scrollY, scopeWidth, 18);
        lblFontFamily.SetBounds(left + scopeWidth + gap, top + scrollY, fontWidth, 18);
        lblFontSize.SetBounds(sizeLeft, top + scrollY, left + width - sizeLeft, 18);
        cboScope.SetBounds(left, top + 20 + scrollY, scopeWidth, 32);
        cboFont.SetBounds(left + scopeWidth + gap, top + 20 + scrollY, fontWidth, 32);
        nudFontSize.SetBounds(sizeLeft, top + 20 + scrollY, left + width - sizeLeft, 30);
        top += 20 + 32 + 10;
        lblFontPreview.SetBounds(left, top + scrollY, width, 34);
        top += 38;
        lblFontWarning.SetBounds(left, top + scrollY, width, 18);
        return top + 18;
    }
    #endregion

    #region Thiết lập (lưu riêng)
    private void LoadSettingsIntoUi()
    {
        _loadingUi = true;
        try
        {
            txtPrefix.Text = string.IsNullOrWhiteSpace(_settings.TicketPrefix) ? WarehouseTicketCodeService.DefaultPrefix : _settings.TicketPrefix;
            txtOutput.Text = _settings.OutputFolder;
            cboScope.SelectedIndex = Math.Max(0, Array.FindIndex(Scopes, s => s.Scope == _settings.FontScope));
            SelectFont(string.IsNullOrWhiteSpace(_settings.FontFamily) ? "Times New Roman" : _settings.FontFamily);
            if (_settings.FontSize >= WarehouseFontSettings.MinFontSize && _settings.FontSize <= WarehouseFontSettings.MaxFontSize)
                nudFontSize.Value = (decimal)_settings.FontSize;
        }
        finally
        {
            _loadingUi = false;
        }

        LoadPrinters();
        UpdateFontPreview();
        UpdatePageInfo();

        // Nhớ file mẫu lần trước (file hàng hóa thì mỗi phiếu một file nên không tự nạp).
        if (File.Exists(_settings.TemplatePath))
            _ = SetTemplateAsync(_settings.TemplatePath, showErrors: false);
    }

    private void SaveSettings()
    {
        if (_loadingUi)
            return;

        _settings.TicketPrefix = txtPrefix.Text.Trim();
        _settings.OutputFolder = txtOutput.Text.Trim();
        _settings.FontScope = SelectedScope;
        _settings.FontFamily = cboFont.SelectedItem as string ?? "";
        _settings.FontSize = (double)nudFontSize.Value;

        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WarehouseLog.Error("Lưu thiết lập phiếu xuất kho thất bại", ex);
            lblStatus.Text = "Không lưu được thiết lập (máy in / font / thư mục) — lần mở sau có thể phải chọn lại.";
        }
    }

    private void SelectFont(string family)
    {
        int index = cboFont.Items.IndexOf(family);
        if (index < 0)
        {
            // Font đã lưu không còn trên máy: vẫn hiện để người dùng thấy và đổi.
            index = cboFont.Items.Add(family);
        }
        cboFont.SelectedIndex = index;
    }
    #endregion

    #region Mã phiếu
    private TicketCodeResult CurrentTicketCode => WarehouseTicketCodeService.Normalize(txtTicketCode.Text, txtPrefix.Text);

    private void OnTicketInputChanged()
    {
        MarkExportStale();
        UpdateState();
    }
    #endregion

    #region Chọn file / thư mục
    private void ChooseTemplate()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Chọn file phiếu xuất kho mẫu",
            Filter = "File Excel (*.xlsx;*.xls)|*.xlsx;*.xls",
            InitialDirectory = FolderOf(txtTemplate.Text)
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _ = SetTemplateAsync(dialog.FileName, showErrors: true);
    }

    private void ChooseGoods()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Chọn file hàng hóa",
            Filter = "File Excel (*.xlsx;*.xls)|*.xlsx;*.xls",
            InitialDirectory = FolderOf(txtGoods.Text) ?? (Directory.Exists(_settings.LastGoodsFolder) ? _settings.LastGoodsFolder : null)
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        txtGoods.Text = dialog.FileName;
        _settings.LastGoodsFolder = Path.GetDirectoryName(dialog.FileName) ?? "";
        SaveSettings();
        _ = LoadDocumentAsync(showErrors: true);
    }

    private void ChooseOutputFolder()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Chọn thư mục lưu file phiếu xuất kho",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (Directory.Exists(txtOutput.Text))
            dialog.InitialDirectory = txtOutput.Text;

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        txtOutput.Text = dialog.SelectedPath;
        SaveSettings();
        MarkExportStale();
        UpdateState();
    }

    private static string? FolderOf(string path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);

    private async Task SetTemplateAsync(string path, bool showErrors)
    {
        txtTemplate.Text = path;
        _layout = null;
        _templatePage = null;
        _document = null;
        MarkExportStale();
        FillGrid();

        try
        {
            SetBusy(true, "Đang đọc file phiếu mẫu...");
            _layout = await Task.Run(() => _documentService.ReadTemplate(path));
            _settings.TemplatePath = path;
            SaveSettings();

            // File mẫu mới: thiết lập in luôn về "giữ nguyên mẫu" (tuỳ chỉnh của mẫu cũ không áp sang mẫu khác).
            _templatePage = _layout.Page;
            FillPageControls(_templatePage, keepTemplate: true);

            // Chưa chọn đổi font: lấy font/cỡ chữ của tên hàng trong mẫu làm gợi ý.
            if (SelectedScope == WarehouseFontApplyScope.KeepTemplate)
            {
                _loadingUi = true;
                try
                {
                    SelectFont(_layout.NameFontName);
                    nudFontSize.Value = Math.Clamp((decimal)_layout.NameFontSize, nudFontSize.Minimum, nudFontSize.Maximum);
                }
                finally
                {
                    _loadingUi = false;
                }
                UpdateFontPreview();
            }
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Đọc file mẫu {path}", ex);
            if (showErrors)
                MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false, _layout != null ? "Đã đọc file phiếu mẫu." : "File phiếu mẫu chưa hợp lệ.");
        }

        if (_layout != null && File.Exists(txtGoods.Text))
            await LoadDocumentAsync(showErrors);
    }
    #endregion

    #region Đọc + xem trước dữ liệu
    private async Task LoadDocumentAsync(bool showErrors)
    {
        string template = txtTemplate.Text;
        string goods = txtGoods.Text;
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(goods))
        {
            if (showErrors)
                MessageBox.Show("Hãy chọn cả file phiếu mẫu và file hàng hóa trước khi xem trước.", Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _document = null;
        MarkExportStale();
        try
        {
            SetBusy(true, "Đang đọc và đối chiếu dữ liệu...");
            _document = await Task.Run(() => _documentService.Load(template, goods));
            _layout = _document.Layout;
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Đọc dữ liệu phiếu: mẫu={template}, hàng hóa={goods}", ex);
            if (showErrors)
                MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            FillGrid();
            SetBusy(false, _document != null
                ? $"Đã đọc {_document.Rows.Count} dòng hàng từ {Path.GetFileName(goods)}."
                : "Chưa đọc được dữ liệu.");
        }
    }

    private void FillGrid()
    {
        _fillingGrid = true;
        dgvRows.SuspendLayout();
        try
        {
            dgvRows.Rows.Clear();
            if (_document == null)
                return;

            foreach (WarehouseProductRow item in _document.Rows)
            {
                int index = dgvRows.Rows.Add(item.SourceRowNumber, item.ProductCode, item.SourceName, item.TicketName,
                    item.Unit, FormatQuantity(item.RequestedQuantity), FormatQuantity(item.ActualQuantity), "");
                dgvRows.Rows[index].Tag = item;
                dgvRows.Rows[index].Cells["colSourceName"].ToolTipText = item.SourceName;
                PaintRow(dgvRows.Rows[index], item);
            }
        }
        finally
        {
            dgvRows.ResumeLayout();
            dgvRows.ClearSelection();
            _fillingGrid = false;
            UpdateState();
        }
    }

    private void PaintRow(DataGridViewRow gridRow, WarehouseProductRow item)
    {
        (string text, Color back, Color fore) = item.Status switch
        {
            WarehouseRowStatus.Error => ("✗ " + string.Join(" ", item.Messages), AppTheme.Colors.DangerLight, AppTheme.Colors.Danger),
            WarehouseRowStatus.Warning => ("⚠ " + string.Join(" ", item.Messages), WarningBack, WarningText),
            _ => ("✓ Hợp lệ", dgvRows.DefaultCellStyle.BackColor, AppTheme.Colors.Success)
        };

        DataGridViewCell status = gridRow.Cells["colStatus"];
        status.Value = text;
        status.ToolTipText = string.Join(Environment.NewLine, item.Messages);
        status.Style.ForeColor = fore;
        gridRow.DefaultCellStyle.BackColor = back;
        gridRow.Cells["colTicketName"].ToolTipText = item.TicketName;
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || dgvRows.Rows[e.RowIndex].Tag is not WarehouseProductRow item)
            return;

        DataGridViewRow gridRow = dgvRows.Rows[e.RowIndex];
        string column = dgvRows.Columns[e.ColumnIndex].Name;
        string text = gridRow.Cells[e.ColumnIndex].Value?.ToString() ?? "";

        switch (column)
        {
            case "colTicketName":
                item.TicketName = VietnameseText.CollapseSpaces(text);
                gridRow.Cells[e.ColumnIndex].Value = item.TicketName;
                break;
            case "colUnit":
                item.Unit = text;
                break;
            case "colRequested":
                item.RequestedQuantity = ParseQuantity(text);
                gridRow.Cells[e.ColumnIndex].Value = FormatQuantity(item.RequestedQuantity) is { Length: > 0 } r ? r : text;
                break;
            case "colActual":
                item.ActualQuantity = ParseQuantity(text);
                gridRow.Cells[e.ColumnIndex].Value = FormatQuantity(item.ActualQuantity) is { Length: > 0 } a ? a : text;
                break;
            default:
                return;
        }

        WarehouseProductNameService.Revalidate(item);
        PaintRow(gridRow, item);
        MarkExportStale();
        UpdateState();
    }

    // Số lượng sửa tay: chấp nhận "4", "2,5", "2.5"; còn lại = không hợp lệ.
    private static double? ParseQuantity(string text)
    {
        string s = text.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
    }

    private static string FormatQuantity(double? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    #endregion

    #region Máy in
    private void LoadPrinters()
    {
        _loadingUi = true;
        try
        {
            cboPrinter.Items.Clear();
            foreach (string printer in _printerService.GetInstalledPrinters())
                cboPrinter.Items.Add(printer);

            int index = cboPrinter.Items.IndexOf(_settings.PrinterName);
            cboPrinter.SelectedIndex = index;
        }
        finally
        {
            _loadingUi = false;
        }

        UpdateState();
    }

    private string SelectedPrinter => cboPrinter.SelectedItem as string ?? "";
    #endregion

    #region Khổ giấy và thiết lập in
    private bool CustomPageMode => cboPageMode.SelectedIndex == 1;

    /// <summary>Đưa thiết lập trang của mẫu lên các ô (không phát sinh sự kiện "đã đổi").</summary>
    private void FillPageControls(WarehousePageSetup page, bool keepTemplate)
    {
        _loadingUi = true;
        try
        {
            if (keepTemplate)
                cboPageMode.SelectedIndex = 0;

            cboPaper.Items.Clear();
            _paperCodes.Clear();
            foreach (WarehousePaperSizes.Paper paper in WarehousePaperSizes.Common)
            {
                cboPaper.Items.Add(paper.Display);
                _paperCodes.Add(paper.Code);
            }
            if (!_paperCodes.Contains(page.PaperSize))
            {
                // Khổ riêng của mẫu (theo máy in): vẫn chọn được để giữ nguyên.
                cboPaper.Items.Insert(0, WarehousePaperSizes.Describe(page.PaperSize));
                _paperCodes.Insert(0, page.PaperSize);
            }
            cboPaper.SelectedIndex = _paperCodes.IndexOf(page.PaperSize);
            cboOrientation.SelectedIndex = page.Landscape ? 1 : 0;

            cboScaleMode.Items.Clear();
            _scaleChoices.Clear();
            AddScaleChoice("Theo tỷ lệ %", new WarehouseScaleChoice(WarehouseScaleMode.Percent, page.Scale));
            AddScaleChoice("Vừa chiều rộng trang", new WarehouseScaleChoice(WarehouseScaleMode.FitWidth));
            AddScaleChoice("Vừa một trang", new WarehouseScaleChoice(WarehouseScaleMode.FitOnePage));
            int scaleIndex = !page.FitToPage ? 0
                : page is { FitWidth: 1, FitHeight: 0 } ? 1
                : page is { FitWidth: 1, FitHeight: 1 } ? 2
                : -1;
            if (scaleIndex < 0)
            {
                // Co giãn kiểu khác (vd 2 trang ngang) chỉ giữ được như mẫu.
                cboScaleMode.Items.Insert(0, "Như mẫu: " + page.ScaleDescription);
                _scaleChoices.Insert(0, null);
                scaleIndex = 0;
            }
            cboScaleMode.SelectedIndex = scaleIndex;
            nudScale.Value = Math.Clamp(page.Scale, nudScale.Minimum, nudScale.Maximum);

            nudMarginLeft.Value = ToCm(page.Margins.Left);
            nudMarginRight.Value = ToCm(page.Margins.Right);
            nudMarginTop.Value = ToCm(page.Margins.Top);
            nudMarginBottom.Value = ToCm(page.Margins.Bottom);
        }
        finally
        {
            _loadingUi = false;
        }

        UpdatePageInfo();
    }

    private void AddScaleChoice(string text, WarehouseScaleChoice choice)
    {
        cboScaleMode.Items.Add(text);
        _scaleChoices.Add(choice);
    }

    private decimal ToCm(double inches) =>
        Math.Clamp(Math.Round((decimal)(inches * WarehouseMargins.CentimetersPerInch), 2), nudMarginLeft.Minimum, nudMarginLeft.Maximum);

    private static double ToInches(decimal cm) => Math.Round((double)cm / WarehouseMargins.CentimetersPerInch, 4);

    /// <summary>
    /// Thiết lập in sẽ áp dụng: chỉ những mục KHÁC file mẫu. Giữ nguyên mẫu
    /// (hoặc chưa có mẫu) thì không đổi gì.
    /// </summary>
    private WarehousePageSettings CurrentPageSettings()
    {
        WarehousePageSetup? template = _templatePage;
        if (!CustomPageMode || template == null)
            return WarehousePageSettings.KeepTemplate();

        short? paper = cboPaper.SelectedIndex >= 0 && _paperCodes[cboPaper.SelectedIndex] != template.PaperSize
            ? _paperCodes[cboPaper.SelectedIndex]
            : null;
        bool landscape = cboOrientation.SelectedIndex == 1;

        decimal[] shown = [nudMarginLeft.Value, nudMarginRight.Value, nudMarginTop.Value, nudMarginBottom.Value];
        double[] original = [template.Margins.Left, template.Margins.Right, template.Margins.Top, template.Margins.Bottom];
        bool marginsChanged = shown.Where((cm, i) => cm != ToCm(original[i])).Any();

        WarehouseScaleChoice? scale = cboScaleMode.SelectedIndex >= 0 ? _scaleChoices[cboScaleMode.SelectedIndex] : null;
        if (scale?.Mode == WarehouseScaleMode.Percent)
            scale = scale with { Percent = (short)nudScale.Value };
        bool scaleChanged = scale != null && scale.Mode switch
        {
            WarehouseScaleMode.Percent => template.FitToPage || template.Scale != scale.Percent,
            WarehouseScaleMode.FitWidth => !(template.FitToPage && template is { FitWidth: 1, FitHeight: 0 }),
            _ => !(template.FitToPage && template is { FitWidth: 1, FitHeight: 1 })
        };

        return new WarehousePageSettings
        {
            PaperSize = paper,
            Landscape = landscape != template.Landscape ? landscape : null,
            Margins = marginsChanged
                ? new WarehouseMargins(ToInches(shown[0]), ToInches(shown[1]), ToInches(shown[2]), ToInches(shown[3]),
                    template.Margins.Header, template.Margins.Footer)
                : null,
            Scale = scaleChanged ? scale : null
        };
    }

    private void OnPageSettingsChanged()
    {
        if (_loadingUi)
            return;

        MarkExportStale();
        UpdatePageInfo();
        UpdateState();
    }

    private void UpdatePageInfo()
    {
        if (_templatePage == null)
        {
            lblPageInfo.Text = "Chọn file phiếu mẫu để đọc khổ giấy, hướng giấy, lề, tỷ lệ in và vùng in của mẫu.";
            lblPageWarning.Text = "";
            return;
        }

        WarehousePageSettings settings = CurrentPageSettings();
        WarehousePageSetup result = settings.ApplyTo(_templatePage);
        string area = _templatePage.PrintArea == null
            ? "Vùng in: mẫu không đặt (Excel in toàn bộ vùng có dữ liệu)"
            : $"Vùng in: {_templatePage.PrintArea} (tự dời theo số dòng hàng)";
        lblPageInfo.Text =
            $"Mẫu: {_templatePage.PaperName} · {_templatePage.OrientationName} · {_templatePage.ScaleDescription} · " +
            $"{_templatePage.Margins.Describe()} · {area}." + Environment.NewLine +
            (settings.KeepsTemplate
                ? "Sẽ in đúng thiết lập của mẫu (không đổi gì)."
                : $"Sẽ in: {result.PaperName} · {result.OrientationName} · {result.ScaleDescription} · lề {result.Margins.Describe()} — chỉ đổi: {settings.Describe()}.");

        WarehouseValidationReport report = new();
        if (_layout != null)
            WarehouseDocumentService.ValidatePage(settings, _layout, report);
        (lblPageWarning.ForeColor, lblPageWarning.Text) = report.Errors.Count > 0
            ? (AppTheme.Colors.Danger, "✗ " + report.Errors[0])
            : report.Warnings.Count > 0
                ? (WarningText, "⚠ " + report.Warnings[0])
                : (AppTheme.Colors.TextMuted, settings.KeepsTemplate ? "" : "Số trang thực tế xem ở XEM TRƯỚC BẢN IN sau khi xuất.");
    }

    /// <summary>Đọc lại thiết lập trang in trực tiếp từ file mẫu (file có thể vừa được sửa) và đưa lên các ô.</summary>
    private async Task LoadPageFromTemplateAsync()
    {
        string template = txtTemplate.Text;
        if (!File.Exists(template))
        {
            MessageBox.Show("Chưa chọn file phiếu mẫu (hoặc file không còn tồn tại).", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        WarehouseTemplateLayout? layout = null;
        try
        {
            SetBusy(true, "Đang đọc thiết lập in của file mẫu...");
            layout = await Task.Run(() => _documentService.ReadTemplate(template));
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Đọc thiết lập in của mẫu {template}", ex);
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false, layout != null ? "Đã lấy thiết lập in từ file mẫu." : "Chưa đọc được thiết lập in của mẫu.");
        }

        if (layout == null)
            return;

        _templatePage = layout.Page;
        FillPageControls(layout.Page, keepTemplate: false);
        MarkExportStale();
        UpdateState();
    }

    private void RestoreTemplatePage()
    {
        if (_templatePage == null)
            return;

        FillPageControls(_templatePage, keepTemplate: true);
        MarkExportStale();
        UpdateState();
        lblStatus.Text = "Đã khôi phục thiết lập in của file mẫu.";
    }
    #endregion

    #region Font chữ
    private WarehouseFontApplyScope SelectedScope =>
        cboScope.SelectedIndex >= 0 ? Scopes[cboScope.SelectedIndex].Scope : WarehouseFontApplyScope.KeepTemplate;

    private WarehouseFontSettings CurrentFontSettings() => new()
    {
        Scope = SelectedScope,
        FontFamily = cboFont.SelectedItem as string ?? "",
        FontSize = (double)nudFontSize.Value
    };

    private void OnFontChanged()
    {
        if (_loadingUi)
            return;

        SaveSettings();
        UpdateFontPreview();
        MarkExportStale();
        UpdateState();
    }

    private void UpdateFontPreview()
    {
        bool keep = SelectedScope == WarehouseFontApplyScope.KeepTemplate;
        cboFont.Enabled = !keep && !_busy;
        nudFontSize.Enabled = !keep && !_busy;

        string family = cboFont.SelectedItem as string ?? "";
        Font? font = null;
        try
        {
            if (family.Length > 0)
                font = new Font(family, (float)nudFontSize.Value, FontStyle.Regular, GraphicsUnit.Point);
        }
        catch (ArgumentException ex)
        {
            WarehouseLog.Error($"Không tạo được font xem trước {family}", ex);
        }

        Font? old = _previewFont;
        _previewFont = font;
        lblFontPreview.Font = font ?? AppTheme.Fonts.Body;
        old?.Dispose();

        if (keep)
        {
            lblFontWarning.ForeColor = AppTheme.Colors.TextSecondary;
            lblFontWarning.Text = _layout != null
                ? $"Đang giữ nguyên font của mẫu (tên hàng: {_layout.NameFontName} {_layout.NameFontSize:0.#}pt) — không ô nào bị đổi font."
                : "Đang giữ nguyên font và cỡ chữ của file mẫu — không ô nào bị đổi font.";
            return;
        }

        FontCheck check = _fontService.Check(family);
        (lblFontWarning.ForeColor, lblFontWarning.Text) = !check.IsInstalled
            ? (AppTheme.Colors.Danger, $"Font \"{family}\" chưa được cài trên máy này — chọn font khác.")
            : !check.SupportsVietnamese
                ? (WarningText, $"⚠ Font \"{family}\" thiếu một số chữ tiếng Việt có dấu — có thể in ra ô vuông.")
                : (AppTheme.Colors.Success, "✓ Đủ chữ tiếng Việt. Chỉ đổi font/cỡ chữ trong phạm vi đã chọn; viền, căn lề, đậm/nghiêng giữ nguyên.");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _previewFont?.Dispose();
        base.Dispose(disposing);
    }
    #endregion

    #region Kiểm tra / xuất / in
    private WarehouseValidationReport BuildReport() =>
        _documentService.Validate(txtTicketCode.Text, txtPrefix.Text, _document, txtOutput.Text.Trim(), CurrentFontSettings(),
            CurrentPageSettings());

    private void ValidateAndShow()
    {
        WarehouseValidationReport report = BuildReport();
        string exists = report.ExistingOutputPath != null
            ? $"\n\nLưu ý: file {Path.GetFileName(report.ExistingOutputPath)} đã có trong thư mục output — khi xuất sẽ hỏi trước khi ghi đè."
            : "";

        if (report.CanExport && report.Warnings.Count == 0)
        {
            MessageBox.Show($"Dữ liệu hợp lệ — {_document!.Rows.Count} dòng hàng, sẵn sàng xuất phiếu {CurrentTicketCode.Code}." + exists,
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show(FormatReport(report) + exists, Title, MessageBoxButtons.OK,
            report.CanExport ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static string FormatReport(WarehouseValidationReport report, int max = 12)
    {
        List<string> lines = new();
        if (report.Errors.Count > 0)
        {
            lines.Add($"CÒN {report.Errors.Count} LỖI PHẢI SỬA TRƯỚC KHI XUẤT:");
            lines.AddRange(report.Errors.Take(max).Select(e => "  • " + e));
            if (report.Errors.Count > max)
                lines.Add($"  ... và {report.Errors.Count - max} lỗi khác (xem cột Trạng thái).");
        }

        if (report.Warnings.Count > 0)
        {
            if (lines.Count > 0)
                lines.Add("");
            lines.Add($"{report.Warnings.Count} CẢNH BÁO (vẫn xuất được, nên xem lại):");
            lines.AddRange(report.Warnings.Take(max).Select(w => "  • " + w));
            if (report.Warnings.Count > max)
                lines.Add($"  ... và {report.Warnings.Count - max} cảnh báo khác.");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private async Task ExportAsync()
    {
        dgvRows.EndEdit();
        WarehouseValidationReport report = BuildReport();
        if (!report.CanExport)
        {
            MessageBox.Show(FormatReport(report), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string code = CurrentTicketCode.Code!;
        WarehouseDocument document = _document!;
        string output = WarehouseDocumentService.BuildOutputPath(txtOutput.Text.Trim(), code, document.Layout.FileExtension);

        if (report.Warnings.Count > 0 &&
            MessageBox.Show(FormatReport(report) + "\n\nVẫn xuất phiếu?", Title, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        bool overwrite = false;
        if (File.Exists(output))
        {
            if (MessageBox.Show($"File {Path.GetFileName(output)} đã có trong thư mục output.\n\nGhi đè file này?",
                    Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            overwrite = true;
        }

        WarehouseExportRequest request = new()
        {
            TemplatePath = document.TemplatePath,
            OutputPath = output,
            TicketCode = code,
            Rows = document.Rows,
            Font = CurrentFontSettings(),
            Page = CurrentPageSettings(),
            OverwriteExisting = overwrite
        };

        try
        {
            SetBusy(true, $"Đang xuất phiếu {code}...");
            WarehouseExportResult result = await Task.Run(() => _documentService.Export(request));
            _lastExport = result;
            _exportStale = false;
            _previewedStamp = null;
            WarehouseLog.Info($"Xuất phiếu {code}: {result.OutputPath} ({result.RowCount} dòng, chênh {result.RowDelta}), " +
                              $"font={request.Font.Scope}, thiết lập in: {request.Page.Describe()}");

            MessageBox.Show(
                $"Đã xuất phiếu {code} ({result.RowCount} dòng hàng):\n{result.OutputPath}\n\n" +
                "Đã mở lại file và kiểm tra:\n" + string.Join("\n", result.VerifiedChecks.Select(c => "  ✓ " + c)) +
                "\n\nBước tiếp theo: XEM TRƯỚC BẢN IN để kiểm tra từng trang trước khi in.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Xuất phiếu {code} → {output}", ex);
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false, _lastExport != null && !_exportStale ? $"Đã xuất: {_lastExport.OutputPath}" : "Chưa xuất được phiếu.");
        }
    }

    /// <summary>
    /// File phiếu mới nhất còn dùng được để xem trước / in: đã xuất, dữ liệu /
    /// font / thiết lập in chưa đổi sau lần xuất, file còn đó và chưa bị sửa
    /// hay ghi đè bên ngoài. <paramref name="reason"/> = lý do khi không dùng được.
    /// </summary>
    private bool IsExportCurrent(out string reason)
    {
        reason = _lastExport == null
            ? "Chưa có file phiếu. Hãy bấm XUẤT PHIẾU EXCEL trước."
            : _exportStale
                ? "Dữ liệu, font hoặc thiết lập in đã thay đổi sau lần xuất trước — bản đã xuất là bản CŨ.\n\nHãy bấm XUẤT PHIẾU EXCEL lại."
                : !File.Exists(_lastExport.OutputPath)
                    ? $"File phiếu không còn tồn tại:\n{_lastExport.OutputPath}\n\nHãy xuất lại."
                    : !_lastExport.Stamp.Matches(_lastExport.OutputPath)
                        ? $"File phiếu đã bị sửa hoặc ghi đè bên ngoài sau khi xuất:\n{_lastExport.OutputPath}\n\nHãy xuất lại để in đúng dữ liệu đã kiểm tra."
                        : "";
        return reason.Length == 0;
    }

    private string DescribeFont()
    {
        WarehouseFontSettings font = CurrentFontSettings();
        return font.Scope == WarehouseFontApplyScope.KeepTemplate
            ? _layout != null
                ? $"Giữ nguyên font của mẫu (tên hàng: {_layout.NameFontName} {_layout.NameFontSize:0.#}pt)"
                : "Giữ nguyên font của mẫu"
            : $"{font.FontFamily} {font.FontSize:0.#}pt — {Scopes.First(s => s.Scope == font.Scope).Text.ToLowerInvariant()}";
    }

    /// <summary>
    /// Xem trước bản in: Excel xuất chính file phiếu ra PDF (phân trang theo
    /// máy in đã chọn) rồi mở cửa sổ xem từng trang. In từ cửa sổ này dùng
    /// đúng file đã xem.
    /// </summary>
    private async Task PrintPreviewAsync()
    {
        if (!IsExportCurrent(out string reason))
        {
            MessageBox.Show(reason, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!WarehousePrinterService.IsExcelAvailable())
        {
            MessageBox.Show("Xem trước bản in cần Microsoft Excel (dùng chính Excel để phân trang giống khi in), " +
                            "nhưng máy này chưa cài Excel.\n\nFile phiếu đã xuất vẫn mở được bằng phần mềm bảng tính khác để xem và in.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        WarehouseExportResult export = _lastExport!;
        string printer = SelectedPrinter;
        bool printerOk = _printerService.IsPrinterAvailable(printer);
        string pdfFolder = WarehousePrintPreviewService.DefaultPreviewFolder;
        WarehousePrintPreview? preview = null;

        try
        {
            SetBusy(true, "Đang dựng bản xem trước bằng Microsoft Excel (phân trang thực tế)...");
            WarehousePrintPreviewService.CleanupOldPreviews(pdfFolder);
            preview = await _previewService.BuildAsync(new WarehousePreviewRequest
            {
                WorkbookPath = export.OutputPath,
                Layout = export.Layout,
                TemplatePath = _document?.TemplatePath ?? txtTemplate.Text,
                TemplateRowCount = _layout?.DataRowCount ?? export.Layout.DataRowCount,
                PrinterName = printerOk ? printer : null,
                PdfFolder = pdfFolder,
                FontDescription = DescribeFont()
            });
            _previewedStamp = preview.SourceStamp;
            WarehouseLog.Info($"Xem trước {export.OutputPath}: {preview.PageCount} trang (mẫu {preview.TemplatePageCount?.ToString() ?? "?"}), " +
                              $"máy in phân trang {preview.PaginationPrinter}, {preview.WarningCount} cảnh báo bố cục");
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Xem trước bản in {export.OutputPath}", ex);
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false, preview != null
                ? $"Bản xem trước: {preview.PageCount} trang, {preview.WarningCount} cảnh báo bố cục."
                : "Chưa dựng được bản xem trước.");
        }

        if (preview == null)
            return;

        string hint = printerOk
            ? $"Bấm IN để in {preview.PageCount} trang ra máy in: {printer}"
            : "Chưa chọn máy in hợp lệ nên chưa in được — đang phân trang theo máy in mặc định của Excel. Chọn máy in ở màn hình phiếu rồi xem trước lại.";
        try
        {
            using FormWarehousePrintPreview form = new(preview, printerOk ? owner => PrintAsync(owner, preview) : null, hint);
            form.ShowDialog(FindForm());
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Mở cửa sổ xem trước {preview.PdfPath}", ex);
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            TryDeleteFile(preview.PdfPath);
            UpdateState();
        }
    }

    /// <summary>
    /// In file phiếu mới nhất ra máy in đã chọn sau khi người dùng xác nhận.
    /// <paramref name="preview"/> != null: in từ cửa sổ xem trước — file phải
    /// đúng là bản vừa xem. Trả về true nếu đã gửi lệnh in.
    /// </summary>
    private async Task<bool> PrintAsync(IWin32Window? owner, WarehousePrintPreview? preview)
    {
        if (!IsExportCurrent(out string reason))
        {
            MessageBox.Show(owner, reason, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (preview != null && !preview.IsStillCurrent())
        {
            MessageBox.Show(owner, "File phiếu đã thay đổi sau khi dựng bản xem trước này — hãy đóng và xem trước lại.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        string printer = SelectedPrinter;
        if (!_printerService.IsPrinterAvailable(printer))
        {
            MessageBox.Show(owner, printer.Length == 0
                    ? "Chưa chọn máy in cho phiếu xuất kho."
                    : $"Máy in \"{printer}\" không còn tồn tại hoặc không dùng được.\n\nHãy bấm Làm mới và chọn lại máy in.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        WarehouseExportResult export = _lastExport!;
        string file = export.OutputPath;
        string pages = preview != null ? $" ({preview.PageCount} trang)" : "";
        string notPreviewed = preview == null && _previewedStamp != export.Stamp
            ? "\n\nChưa xem trước bản in của file này — nên bấm XEM TRƯỚC BẢN IN để kiểm tra từng trang trước."
            : "";
        if (MessageBox.Show(owner, $"In phiếu {Path.GetFileName(file)}{pages} ra máy in:\n{printer}?{notPreviewed}", Title,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return false;

        bool ok = false;
        try
        {
            SetBusy(true, $"Đang gửi lệnh in tới {printer} (qua Microsoft Excel)...");
            await _printerService.PrintWorkbookAsync(file, printer, sheetName: export.SheetName);
            ok = true;
            WarehouseLog.Info($"In phiếu {file} (sheet {export.SheetName}){pages} → {printer}");
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"In phiếu {file} → {printer}", ex);
            MessageBox.Show(owner, ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false, ok ? $"Đã gửi lệnh in {Path.GetFileName(file)} tới {printer}." : "In chưa thành công.");
        }

        return ok;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // File PDF tạm nằm trong %TEMP% — lần xem trước sau sẽ dọn file cũ.
            WarehouseLog.Error($"Không xoá được file xem trước {path}", ex);
        }
    }

    private void ClearData()
    {
        if ((_document != null || txtTicketCode.Text.Length > 0) &&
            MessageBox.Show("Xóa mã phiếu, file hàng hóa và bảng xem trước?\n\n(File mẫu, thư mục output, máy in và font vẫn được giữ.)",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        txtTicketCode.Text = "";
        txtGoods.Text = "";
        _document = null;
        _lastExport = null;
        _exportStale = false;
        _previewedStamp = null;
        FillGrid();
        lblStatus.Text = "Đã xóa dữ liệu phiếu.";
    }

    private void OpenOutputFolder()
    {
        string folder = txtOutput.Text.Trim();
        if (!Directory.Exists(folder))
        {
            MessageBox.Show("Thư mục output chưa có (sẽ được tạo khi xuất phiếu).", Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            string? file = _lastExport != null && File.Exists(_lastExport.OutputPath) ? _lastExport.OutputPath : null;
            Process.Start(new ProcessStartInfo("explorer.exe", file != null ? $"/select,\"{file}\"" : $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            WarehouseLog.Error($"Mở thư mục {folder}", ex);
            MessageBox.Show($"Không mở được thư mục:\n{folder}", Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void MarkExportStale()
    {
        if (_lastExport != null)
            _exportStale = true;
    }
    #endregion

    #region Trạng thái nút
    private void SetBusy(bool busy, string status)
    {
        _busy = busy;
        lblStatus.Text = status;
        UseWaitCursor = busy;
        UpdateState();
    }

    private void UpdateState()
    {
        TicketCodeResult code = CurrentTicketCode;
        lblFullCode.Text = code.IsValid ? $"Mã phiếu hoàn chỉnh: {code.Code}" : (txtTicketCode.Text.Length == 0 ? "Mã phiếu hoàn chỉnh: —" : "⚠ " + code.Error);
        lblFullCode.ForeColor = code.IsValid || txtTicketCode.Text.Length == 0 ? AppTheme.Colors.TextPrimary : AppTheme.Colors.Danger;

        lblTemplateInfo.Text = _layout == null
            ? (txtTemplate.Text.Length == 0 ? "File gốc chỉ được đọc, không bao giờ bị sửa." : "File mẫu chưa hợp lệ — chọn lại file.")
            : $"Sheet \"{_layout.SheetName}\" · ô số phiếu {new CellReference(_layout.TicketCodeRow, _layout.TicketCodeColumn).FormatAsString()} " +
              $"({_layout.TicketCodeLabel}{_layout.TemplateTicketCode}) · bảng hàng từ dòng {_layout.DataFirstRow + 1}, " +
              $"{_layout.DataRowCount} dòng mẫu · {_layout.NameFontName} {_layout.NameFontSize:0.#}pt";
        lblTemplateInfo.ForeColor = _layout == null && txtTemplate.Text.Length > 0 ? AppTheme.Colors.Danger : AppTheme.Colors.TextMuted;

        lblGoodsInfo.Text = _document == null
            ? (txtGoods.Text.Length == 0 ? "Cần các cột: Mã hàng, Tên hàng, Số lượng chuyển (hoặc Số lượng)." : "Chưa đọc được file hàng hóa.")
            : $"Sheet \"{_document.Goods.SheetName}\" · tiêu đề dòng {_document.Goods.HeaderRowNumber} · " +
              $"số lượng lấy từ cột \"{_document.Goods.QuantityColumnHeader}\" · {_document.Rows.Count} dòng hàng";

        string folder = txtOutput.Text.Trim();
        lblOutputInfo.Text = folder.Length == 0
            ? "Thư mục chưa có sẽ được tạo khi xuất."
            : code.IsValid
                ? $"Tên file dự kiến: {code.Code}{_layout?.FileExtension ?? ".xlsx"}" +
                  (File.Exists(Path.Combine(folder, code.Code + (_layout?.FileExtension ?? ".xlsx"))) ? "  (đã tồn tại — sẽ hỏi trước khi ghi đè)" : "") +
                  (Directory.Exists(folder) ? "" : "  · thư mục chưa có, sẽ được tạo")
                : "Tên file dự kiến: <mã phiếu>.xlsx";

        string printer = SelectedPrinter;
        bool printerOk = _printerService.IsPrinterAvailable(printer);
        lblPrinterStatus.Text = printer.Length == 0
            ? $"Chưa chọn máy in. (Máy in mặc định của Windows: {_printerService.GetWindowsDefaultPrinter()} — không bị thay đổi.)"
            : printerOk
                ? $"✓ Đang chọn: {printer}. In bằng Microsoft Excel, đúng khổ giấy/lề của file mẫu."
                : $"✗ Máy in \"{printer}\" không dùng được — bấm Làm mới hoặc chọn máy khác.";
        lblPrinterStatus.ForeColor = printer.Length == 0 ? AppTheme.Colors.TextMuted : printerOk ? AppTheme.Colors.Success : AppTheme.Colors.Danger;

        int errors = _document?.ErrorCount ?? 0;
        int warnings = _document?.WarningCount ?? 0;
        int total = _document?.Rows.Count ?? 0;
        lblSummary.Text = _document == null
            ? "Chưa có dữ liệu — chọn file phiếu mẫu và file hàng hóa."
            : $"{total} dòng hàng · {total - errors - warnings} hợp lệ · {warnings} cảnh báo · {errors} lỗi" +
              (errors > 0 ? " — sửa các dòng lỗi (đỏ) trước khi xuất." : "");
        lblSummary.ForeColor = errors > 0 ? AppTheme.Colors.Danger : warnings > 0 ? WarningText : AppTheme.Colors.TextSecondary;

        bool exported = IsExportCurrent(out _);
        lblDataState.Text = _busy ? "Đang xử lý..."
            : _document == null ? "Trạng thái: chưa có dữ liệu."
            : exported ? $"Trạng thái: đã xuất {Path.GetFileName(_lastExport!.OutputPath)} — xem trước bản in rồi in."
            : _lastExport != null ? "Trạng thái: bản đã xuất là bản cũ (dữ liệu/thiết lập đã đổi hoặc file bị sửa) — cần xuất lại."
            : errors > 0 ? "Trạng thái: còn dòng lỗi." : "Trạng thái: sẵn sàng xuất.";

        bool idle = !_busy;
        btnChooseTemplate.Enabled = idle;
        btnChooseGoods.Enabled = idle;
        btnChooseOutput.Enabled = idle;
        btnRefreshPrinters.Enabled = idle;
        cboPrinter.Enabled = idle;
        cboScope.Enabled = idle;
        txtPrefix.Enabled = idle;
        txtTicketCode.Enabled = idle;
        dgvRows.ReadOnly = !idle;
        btnValidate.Enabled = idle;
        btnPreview.Enabled = idle && txtTemplate.Text.Length > 0 && txtGoods.Text.Length > 0;
        btnExport.Enabled = idle && _document != null && code.IsValid && folder.Length > 0 && errors == 0;
        btnPrintPreview.Enabled = idle && exported;
        btnPrint.Enabled = idle && exported && printerOk;
        btnClear.Enabled = idle && (_document != null || txtTicketCode.Text.Length > 0 || txtGoods.Text.Length > 0);
        btnOpenFolder.Enabled = idle && folder.Length > 0;

        bool keep = SelectedScope == WarehouseFontApplyScope.KeepTemplate;
        cboFont.Enabled = idle && !keep;
        nudFontSize.Enabled = idle && !keep;

        bool hasTemplatePage = _templatePage != null;
        bool customPage = idle && hasTemplatePage && CustomPageMode;
        cboPageMode.Enabled = idle && hasTemplatePage;
        foreach (Control control in new Control[]
                 { cboPaper, cboOrientation, cboScaleMode, nudMarginLeft, nudMarginRight, nudMarginTop, nudMarginBottom })
            control.Enabled = customPage;
        nudScale.Enabled = customPage && cboScaleMode.SelectedIndex >= 0 &&
                           _scaleChoices[cboScaleMode.SelectedIndex]?.Mode == WarehouseScaleMode.Percent;
        btnLoadPage.Enabled = idle && txtTemplate.Text.Length > 0;
        btnRestorePage.Enabled = idle && hasTemplatePage;
    }
    #endregion

    #region Style helpers
    private void StyleOverline(Label label, string text)
    {
        label.Text = text;
        label.Font = AppTheme.Fonts.Overline;
        label.ForeColor = AppTheme.Colors.TextMuted;
        pnlCard.Controls.Add(label);
    }

    private void StyleCaption(Label label, string text)
    {
        label.Text = text;
        label.Font = AppTheme.Fonts.Hint;
        label.ForeColor = AppTheme.Colors.TextSecondary;
        pnlCard.Controls.Add(label);
    }

    private void StyleInfo(Label label)
    {
        label.Font = AppTheme.Fonts.Hint;
        label.ForeColor = AppTheme.Colors.TextMuted;
        label.AutoEllipsis = true;
        pnlCard.Controls.Add(label);
    }

    private void StyleTextBox(RoundedTextBox box, string placeholder)
    {
        box.Height = 40;
        box.ContainerColor = AppTheme.Colors.SurfaceElevated;
        box.PlaceholderText = placeholder;
        pnlCard.Controls.Add(box);
    }

    private void StyleFileButton(RoundedButton button, string text, IconGlyphs.Kind icon, Action onClick)
    {
        button.Text = text;
        button.Icon = icon;
        button.Variant = ButtonVariant.Outline;
        button.ContainerColor = AppTheme.Colors.SurfaceElevated;
        button.Click += (_, _) => onClick();
        pnlCard.Controls.Add(button);
    }

    private void StyleAction(RoundedButton button, string text, IconGlyphs.Kind icon, ButtonVariant variant, Action onClick)
    {
        button.Text = text;
        button.Icon = icon;
        button.Variant = variant;
        button.ContainerColor = AppTheme.Colors.SurfaceElevated;
        button.Click += (_, _) => onClick();
        pnlCard.Controls.Add(button);
    }
    #endregion
}
