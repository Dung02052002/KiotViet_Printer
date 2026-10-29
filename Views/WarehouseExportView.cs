using System.Diagnostics;
using System.Globalization;
using KiotVietLabelPrinter.Forms.Warehouse;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.Fonts;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Services.Warehouse;
using KiotVietLabelPrinter.UI.Controls;
using KiotVietLabelPrinter.UI.Theme;
using KiotVietLabelPrinter.UI;
using NPOI.SS.Util;

namespace KiotVietLabelPrinter.Views;

// Màn hình "Phiếu xuất kho" — nhúng vào FormMain giống các công cụ khác (xem
// FormMain.OpenWarehouseExport). Màn hình chỉ thu thập lựa chọn, hiển thị và
// hỏi xác nhận; đọc/đối chiếu/ghi Excel nằm ở WarehouseDocumentService, xem
// trước bản in ở WarehousePrintPreviewService (+ FormWarehousePrintPreview),
// in ở WarehousePrinterService, tình trạng file đã xuất ở WarehouseExportTracker.
// Máy in / font của màn hình này lưu riêng trong WarehouseSettingsStore, không
// đụng tới cấu hình in tem. Thiết lập trang in tuỳ chỉnh KHÔNG được lưu: mỗi
// lần mở / đổi file mẫu đều về "giữ nguyên mẫu".
//
// Bố cục: card bo góc (chỉ vẽ viền + bóng) chứa MỘT vùng cuộn cho nội dung và
// một thanh thao tác cố định ở đáy (nút luôn nhìn thấy, không bị cuộn mất).
// Nội dung xếp bằng TableLayoutPanel tự co giãn theo font/DPI — không đặt toạ
// độ tay. Lưu ý: card không được tự cuộn (AutoScroll) — Windows cuộn bằng cách
// dịch ảnh đã vẽ, viền bo góc và bóng của card sẽ bị kéo theo vào giữa nội
// dung thành các đường viền / mảng trắng chồng lên nhau.
public class WarehouseExportView : UserControl
{
    private const string Title = "Phiếu xuất kho";

    // Kích thước theo đơn vị logic (96 DPI) — luôn đổi qua S() trước khi dùng.
    private const int ControlHeight = 40;
    private const int ActionHeight = 44;
    private const int ColumnGap = 12;
    private const int SectionGap = 22;
    private const int SideBySideMinWidth = 900;
    private const int MinContentWidth = 700;
    private const int MinVisibleGridRows = 4;
    private const int PrinterColumnPercent = 42;

    private readonly WarehouseDocumentService _documentService = new();
    private readonly WarehousePrinterService _printerService = new();
    private readonly WarehousePrintPreviewService _previewService = new();
    private readonly InstalledFontService _fontService = new();
    private readonly WarehouseSettingsStore _settingsStore;
    private readonly WarehouseExportSettings _settings;
    private readonly WarehouseExportTracker _singleExport = new();

    /// <summary>
    /// Chế độ NHIỀU PHIẾU: file hàng hóa là danh sách chi tiết nhiều phiếu
    /// chuyển (cột "Mã chuyển hàng"). Mỗi phiếu xuất ra một file riêng, mã
    /// phiếu lấy từ mã chuyển (TRF023522 → XK023522). Bảng xem trước hiện phiếu
    /// đang chọn (<see cref="_batchTicket"/>) và <see cref="_document"/> chính
    /// là phiếu đó, nên sửa bảng / kiểm tra / xuất của phiếu dùng chung code
    /// với chế độ một phiếu. null = chế độ một phiếu như cũ.
    /// </summary>
    private List<WarehouseBatchTicket>? _batch;
    private WarehouseBatchTicket? _batchTicket;
    private bool _fillingBatch;

    private bool BatchMode => _batch != null;

    /// <summary>Tình trạng file đã xuất của phiếu đang hiển thị.</summary>
    private WarehouseExportTracker CurrentExport => _batchTicket?.Export ?? _singleExport;

    private IEnumerable<WarehouseExportTracker> AllExports =>
        _batch == null ? [_singleExport] : _batch.Select(t => t.Export);

    /// <summary>Một thay đổi chung (mã, ngày, font, thiết lập in, thư mục, file nguồn): mọi bản đã xuất thành bản cũ.</summary>
    private void MarkAllStale()
    {
        foreach (WarehouseExportTracker export in AllExports)
            export.MarkStale();
    }

    private void ForgetAllPreviews()
    {
        foreach (WarehouseExportTracker export in AllExports)
            export.ForgetPreview();
    }

    /// <summary>"Hôm nay" theo giờ ĐỊA PHƯƠNG của máy (DateTime.Today, không dùng UTC — tối ở Việt Nam UTC vẫn là hôm trước).</summary>
    private readonly Func<DateTime> _today;

    /// <summary>
    /// Người dùng đã chủ động chọn một ngày lập phiếu khác hôm nay. Chưa chọn
    /// thì ngày trên màn hình là "mặc định" và đi theo ngày của máy (mở lại màn
    /// hình sang ngày mới là ngày mới); đã chọn thì giữ nguyên suốt phiên làm
    /// việc — không tự đổi khi nạp lại dữ liệu, kiểm tra, sửa bảng, xuất, xem
    /// trước. Không lưu vào thiết lập: lần chạy sau luôn về ngày hôm đó.
    /// </summary>
    private bool _documentDateChosen;
    private bool _settingDocumentDate;

    private WarehouseTemplateLayout? _layout;
    private WarehouseDocument? _document;

    /// <summary>Thiết lập trang in của file mẫu — gốc để biết người dùng đã đổi mục nào.</summary>
    private WarehousePageSetup? _templatePage;

    private bool _busy;
    private bool _loadingUi;
    private bool _fillingGrid;
    private Font? _previewFont;

    // Máy in phiếu được chọn trong Cấu hình (FormConfig), màn hình này KHÔNG
    // liệt kê máy in: hỏi Print Spooler có thể treo vô thời hạn trên máy có
    // máy in mạng / driver lỗi (đã làm cửa sổ chính không hiện được). Chỉ kiểm
    // tra máy in đã chọn — ở luồng nền, có giới hạn thời gian — ngay trước khi
    // xem trước / in, khi mở màn hình, và khi bấm "Kiểm tra kết nối".
    private PrinterState _printerState = PrinterState.Unknown;
    private Task? _printerCheck;
    private string _printerCheckFor = "";

    private enum PrinterState { Unknown, Checking, Ready, NotInstalled, Invalid, NotResponding }

    private bool PrinterFailed => _printerState is PrinterState.NotInstalled or PrinterState.Invalid or PrinterState.NotResponding;

    /// <summary>Người dùng bấm "Đổi máy in" — FormMain mở màn hình Cấu hình.</summary>
    public event EventHandler? PrinterSettingsRequested;

    /// <summary>Đang xuất / in — FormMain hỏi lại trước khi đóng app.</summary>
    public bool IsBusy => _busy;

    /// <summary>Tình trạng file đã xuất (cho test).</summary>
    internal WarehouseExportTracker ExportTracker => CurrentExport;

    /// <summary>
    /// Vùng cuộn duy nhất của màn hình. Lúc đang bận (khóa các ô nhập) hoặc
    /// đang nạp bảng, WinForms chuyển focus sang control khác và mặc định tự
    /// cuộn tới control đó — màn hình nhảy lung tung. Chỉ chặn tự cuộn trong
    /// lúc đó; bình thường (Tab giữa các ô) vẫn cuộn theo như cũ.
    /// </summary>
    private sealed class ScrollPanel : Panel
    {
        private readonly Func<bool> _suppressAutoScroll;

        public ScrollPanel(Func<bool> suppressAutoScroll)
        {
            _suppressAutoScroll = suppressAutoScroll;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override Point ScrollToControl(Control activeControl) =>
            _suppressAutoScroll() ? DisplayRectangle.Location : base.ScrollToControl(activeControl);

        /// <summary>Cuộn trang như khi lăn chuột trên nền trang (dùng khi control con nhường cú lăn).</summary>
        public void ScrollByWheel(int delta) => OnMouseWheel(new HandledMouseEventArgs(MouseButtons.None, 0, 0, 0, delta));
    }

    // Khung
    private readonly RoundedPanel pnlCard = new();
    private readonly ScrollPanel pnlScroll;
    private readonly TableLayoutPanel tlpContent = NewTable(1);
    private readonly TableLayoutPanel tlpColumns = NewTable(2);
    private readonly TableLayoutPanel secPrinter = NewTable(1);
    private readonly TableLayoutPanel secFont = NewTable(1);
    private readonly TableLayoutPanel secPage = NewTable(1);
    private readonly TableLayoutPanel tlpPageActions = NewTable(2);
    private readonly FlowLayoutPanel flpMargins = NewFlow();
    private readonly FlowLayoutPanel flpPageButtons = NewFlow();
    private readonly TableLayoutPanel tlpFooter = NewTable(1);
    private readonly TableLayoutPanel tlpActions = NewTable(1);
    private bool? _sideBySide;
    private bool _applyingLayout;
    private bool _relayoutPending;
    private readonly Dictionary<Label, int> _wrapWidths = new();
    private bool? _pageButtonsInline;
    private int _actionsPerRow;

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
    private readonly Label lblDocumentDate = new();
    private readonly DateTimePicker dtpDocumentDate = new();
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
    private readonly RoundedTextBox txtPrinterName = new();
    private readonly RoundedButton btnCheckPrinter = new();
    private readonly RoundedButton btnChangePrinter = new();
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
    private readonly TableLayoutPanel pnlBatch = NewTable(1);
    private readonly Label lblBatchTicket = new();
    private readonly ComboBox cboBatchTicket = new();
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
    private RoundedButton[] Actions => [btnValidate, btnPreview, btnExport, btnPrintPreview, btnPrint, btnClear, btnOpenFolder];

    private static readonly (WarehouseFontApplyScope Scope, string Text)[] Scopes =
    [
        (WarehouseFontApplyScope.KeepTemplate, "Giữ nguyên định dạng của file mẫu"),
        (WarehouseFontApplyScope.ProductNameOnly, "Chỉ áp dụng cho tên hàng hóa"),
        (WarehouseFontApplyScope.ProductTable, "Áp dụng cho toàn bộ bảng hàng hóa"),
        (WarehouseFontApplyScope.WholeDocument, "Áp dụng cho toàn bộ nội dung phiếu")
    ];

    private static readonly Color WarningBack = Color.FromArgb(255, 247, 230);
    private static readonly Color WarningText = Color.FromArgb(160, 98, 0);

    public WarehouseExportView() : this(new WarehouseSettingsStore())
    {
    }

    public WarehouseExportView(WarehouseSettingsStore settingsStore) : this(settingsStore, () => DateTime.Today)
    {
    }

    /// <param name="today">Nguồn "hôm nay" — test thay để giả lập sang ngày mới.</param>
    internal WarehouseExportView(WarehouseSettingsStore settingsStore, Func<DateTime> today)
    {
        Dock = DockStyle.Fill;
        _settingsStore = settingsStore;
        _today = today;
        pnlScroll = new ScrollPanel(() => _busy || _fillingGrid);
        _settings = _settingsStore.Load();

        BuildUi();
        LoadSettingsIntoUi();
        UpdateState();
    }

    /// <summary>
    /// Gọi mỗi khi FormMain mở màn hình: cập nhật ngày lập phiếu mặc định theo
    /// ngày hiện tại, hiện cảnh báo đọc thiết lập (nếu có) đúng một lần và nạp
    /// lại danh sách máy in (máy in có thể vừa bị gỡ / thêm trong lúc màn hình
    /// đang ẩn).
    /// </summary>
    public void OnShown()
    {
        RefreshDefaultDocumentDate();

        if (_settingsStore.LoadWarning is { } warning)
        {
            MessageBox.Show(warning, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SaveSettings();
        }

        ReloadPrinterSetting();

        // Tự kiểm tra máy in một lần khi mở màn hình — chạy nền, có giới hạn
        // thời gian: máy in lỗi chỉ hiện trạng thái lỗi, không làm đơ màn hình.
        if (_printerState == PrinterState.Unknown)
            _ = CheckPrinterAsync();
    }

    /// <summary>
    /// Đọc lại máy in phiếu từ file thiết lập (màn hình Cấu hình có thể vừa
    /// đổi nó). Không hỏi spooler.
    /// </summary>
    public void ReloadPrinterSetting()
    {
        string printer = _settingsStore.Load().PrinterName;
        if (!string.Equals(printer, _settings.PrinterName, StringComparison.Ordinal))
        {
            _settings.PrinterName = printer;
            _printerState = PrinterState.Unknown;
            // Bản xem trước phân trang theo máy in cũ — không còn đại diện cho bản sẽ in.
            ForgetAllPreviews();
        }

        UpdateState();
    }

    /// <summary>Đơn vị logic (96 DPI) → pixel thật theo DPI màn hình.</summary>
    private int S(int logical) => LogicalToDeviceUnits(logical);

    #region Dựng giao diện
    // Quy ước bố cục: mỗi khối là một cột xếp chồng (Stack); hàng nào nhiều cột
    // thì là một lưới riêng KHÔNG gộp ô (column span). TableLayoutPanel tự co
    // giãn đo sai các ô gộp vắt qua cột % (đo ở bề rộng gần 0 → khối cao hàng
    // nghìn px) — nên không dùng gộp ô ở đâu cả.
    private void BuildUi()
    {
        SuspendLayout();

        pnlCard.Dock = DockStyle.Fill;
        pnlCard.CornerRadius = 22;
        pnlCard.FillColor = AppTheme.Colors.SurfaceElevated;
        pnlCard.BorderColor = AppTheme.Colors.Border;
        pnlCard.BorderThickness = 1;
        pnlCard.ShadowEnabled = true;
        // Nội dung nằm lọt trong viền bo góc + bóng đổ của card.
        pnlCard.Padding = new Padding(S(20), S(20), S(20), S(14));
        Controls.Add(pnlCard);

        pnlScroll.Dock = DockStyle.Fill;
        pnlScroll.AutoScroll = true;
        pnlScroll.BackColor = AppTheme.Colors.SurfaceElevated;

        tlpContent.Dock = DockStyle.Top;
        tlpContent.Padding = new Padding(S(12), S(12), S(12), S(12));
        // Hẹp hơn mức này thì cuộn ngang thay vì bóp các ô tới mức chữ bị cắt.
        tlpContent.MinimumSize = new Size(S(MinContentWidth), 0);
        pnlScroll.Controls.Add(tlpContent);

        AddSection(BuildHeader(), first: true);
        AddSection(BuildTicketSection());
        AddSection(BuildFilesSection());
        AddSection(BuildPrinterAndFontSections());
        AddSection(BuildPageSection());
        AddSection(BuildPreviewSection());

        // Fill thêm trước, Bottom thêm sau: thanh thao tác được xếp trước và vùng cuộn lấy phần còn lại.
        pnlCard.Controls.Add(pnlScroll);
        pnlCard.Controls.Add(BuildFooter());

        pnlScroll.ClientSizeChanged += (_, _) => ApplyResponsiveLayout();
        // Xếp nút TRƯỚC khi card dock thanh thao tác + vùng cuộn (sự kiện Layout chạy
        // trước bộ dàn trang): card đo thanh thao tác theo số hàng nút mới. Xếp lại
        // sau đó (vd trong SizeChanged của thanh) thì card không đo lại — thanh giữ
        // chiều cao cũ: nút bị cắt, hoặc thừa khoảng trống lớn và bóp vùng nội dung
        // (rõ nhất khi thu nhỏ cửa sổ rồi mở lại: 7 nút xếp thành 7 hàng lúc 0×0).
        pnlCard.Layout += (_, _) => ArrangeActions(pnlCard.DisplayRectangle.Width - tlpFooter.Padding.Horizontal);
        RouteWheelToPage();

        ResumeLayout(true);
        ApplyResponsiveLayout();
    }

    private void AddSection(Control section, bool first = false)
    {
        section.Dock = DockStyle.Fill;
        section.Margin = new Padding(0, first ? 0 : S(SectionGap), 0, 0);
        tlpContent.Controls.Add(section);
    }

    /// <summary>Các control xếp chồng, mỗi control một hàng.</summary>
    private static TableLayoutPanel Stack(params Control[] rows)
    {
        TableLayoutPanel stack = NewTable(1);
        AddRows(stack, rows);
        return stack;
    }

    /// <summary>Đổ các hàng vào một khối dạng cột xếp chồng.</summary>
    private static void AddRows(TableLayoutPanel stack, params Control[] rows)
    {
        for (int i = 0; i < rows.Length; i++)
            stack.Controls.Add(rows[i], 0, stack.Controls.Count);
    }

    /// <summary>Lưới nhiều cột không gộp ô: <paramref name="cells"/> xếp lần lượt từ trái qua phải, hết cột thì xuống hàng.</summary>
    private static TableLayoutPanel Grid(ColumnStyle[] columns, params Control?[] cells)
    {
        TableLayoutPanel grid = NewTable(columns.Length);
        for (int c = 0; c < columns.Length; c++)
            grid.ColumnStyles[c] = columns[c];
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] is { } cell)
                grid.Controls.Add(cell, i % columns.Length, i / columns.Length);
        }
        return grid;
    }

    private static ColumnStyle Percent(float value) => new(SizeType.Percent, value);
    private static ColumnStyle AutoColumn() => new(SizeType.AutoSize);
    private ColumnStyle Fixed(int logical) => new(SizeType.Absolute, S(logical));

    private Control BuildHeader()
    {
        pnlIconBadge.Size = new Size(S(44), S(44));
        pnlIconBadge.Margin = new Padding(0, 0, S(12), 0);
        pnlIconBadge.Anchor = AnchorStyles.Left;
        pnlIconBadge.CornerRadius = 13;
        pnlIconBadge.FillColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.BorderThickness = 0;
        pnlIconBadge.ContainerColor = AppTheme.Colors.SurfaceElevated;

        iconHeader.Dock = DockStyle.Fill;
        iconHeader.Kind = IconGlyphs.Kind.Document;
        iconHeader.IconColor = AppTheme.Colors.Primary;
        iconHeader.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlIconBadge.Controls.Add(iconHeader);

        lblHeaderTitle.Text = "PHIẾU XUẤT KHO";
        lblHeaderTitle.Font = AppTheme.Fonts.SectionTitle;
        lblHeaderTitle.ForeColor = AppTheme.Colors.TextPrimary;
        lblHeaderTitle.AutoSize = true;
        lblHeaderTitle.Margin = Padding.Empty;

        lblHeaderSubtitle.Text = "Điền mã phiếu, tên hàng, đơn vị và số lượng vào chính file phiếu mẫu — giữ nguyên bố cục, định dạng và thiết lập in.";
        StyleOneLine(lblHeaderSubtitle, AppTheme.Fonts.Hint, AppTheme.Colors.TextMuted);

        pnlHeaderDivider.Height = 1;
        pnlHeaderDivider.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        pnlHeaderDivider.Margin = new Padding(0, S(18), 0, 0);
        pnlHeaderDivider.BackColor = AppTheme.Colors.Border;

        return Stack(Grid([AutoColumn(), Percent(100)], pnlIconBadge, Stack(lblHeaderTitle, lblHeaderSubtitle)), pnlHeaderDivider);
    }

    private Control BuildTicketSection()
    {
        StyleOverline(lblTicketTitle, "THÔNG TIN PHIẾU");
        StyleCaption(lblPrefix, "Tiền tố");
        StyleCaption(lblTicketCode, "Mã phiếu");

        StyleTextBox(txtPrefix, "XK");
        txtPrefix.TextChanged += (_, _) => OnTicketInputChanged();
        txtPrefix.Leave += (_, _) => SaveSettings();
        txtPrefix.Margin = new Padding(0, 0, S(ColumnGap), 0);

        StyleTextBox(txtTicketCode, "VD: 23609 hoặc 23609, 23610");
        txtTicketCode.TextChanged += (_, _) => OnTicketInputChanged();
        txtTicketCode.Margin = new Padding(0, 0, S(ColumnGap * 2), 0);

        StyleCaption(lblDocumentDate, "Ngày lập phiếu");
        dtpDocumentDate.Format = DateTimePickerFormat.Custom;
        dtpDocumentDate.CustomFormat = WarehouseDocumentDateService.DisplayFormat;
        dtpDocumentDate.MinDate = WarehouseDocumentDateService.MinDate;
        dtpDocumentDate.MaxDate = WarehouseDocumentDateService.MaxDate;
        dtpDocumentDate.Font = AppTheme.Fonts.Body;
        dtpDocumentDate.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        dtpDocumentDate.Margin = new Padding(0, 0, S(ColumnGap * 2), 0);
        SetDocumentDate(_today());
        dtpDocumentDate.ValueChanged += (_, _) => OnDocumentDateChanged();

        StyleOneLine(lblFullCode, AppTheme.Fonts.BodyBold, AppTheme.Colors.TextPrimary);
        StyleOneLine(lblDataState, AppTheme.Fonts.Hint, AppTheme.Colors.TextSecondary);

        // [Tiền tố] [Mã phiếu] [Ngày lập phiếu] [Mã hoàn chỉnh + trạng thái]
        return Stack(lblTicketTitle, Grid([Fixed(96 + ColumnGap), Fixed(200 + ColumnGap * 2), Fixed(150 + ColumnGap * 2), Percent(100)],
            lblPrefix, lblTicketCode, lblDocumentDate, null,
            txtPrefix, txtTicketCode, dtpDocumentDate, Stack(lblFullCode, lblDataState)));
    }

    private Control BuildFilesSection()
    {
        StyleOverline(lblFilesTitle, "FILE ĐẦU VÀO VÀ THƯ MỤC OUTPUT");

        StyleFileButton(btnChooseTemplate, "CHỌN FILE PHIẾU MẪU", IconGlyphs.Kind.Document, ChooseTemplate);
        StyleTextBox(txtTemplate, "Chưa chọn file phiếu xuất kho mẫu (.xlsx / .xls)");
        StyleFileButton(btnChooseGoods, "CHỌN FILE HÀNG HÓA", IconGlyphs.Kind.Document, ChooseGoods);
        StyleTextBox(txtGoods, "Chưa chọn file hàng hóa (file Chi tiết chuyển hàng từ KiotViet)");
        StyleFileButton(btnChooseOutput, "CHỌN THƯ MỤC OUTPUT", IconGlyphs.Kind.Folder, ChooseOutputFolder);
        StyleTextBox(txtOutput, "Chưa chọn thư mục lưu file phiếu");

        // Ba nút cùng độ rộng = nút có chữ dài nhất (đo theo font thật, không đặt cứng).
        int buttonWidth = new[] { btnChooseTemplate, btnChooseGoods, btnChooseOutput }.Max(b => b.GetContentWidth(S(16)));
        List<Control?> cells = new();
        foreach ((RoundedButton button, RoundedTextBox box, Label info) in new[]
                 {
                     (btnChooseTemplate, txtTemplate, lblTemplateInfo),
                     (btnChooseGoods, txtGoods, lblGoodsInfo),
                     (btnChooseOutput, txtOutput, lblOutputInfo)
                 })
        {
            button.Size = new Size(buttonWidth, S(ControlHeight));
            button.Margin = new Padding(0, 0, S(ColumnGap), 0);
            box.ReadOnly = true;
            StyleOneLine(info, AppTheme.Fonts.Hint, AppTheme.Colors.TextMuted);
            info.Margin = new Padding(S(4), S(3), 0, S(8));
            // [nút chọn] [đường dẫn] / [ ] [dòng thông tin]
            cells.AddRange([button, box, null, info]);
        }

        return Stack(lblFilesTitle, Grid([AutoColumn(), Percent(100)], cells.ToArray()));
    }

    private Control BuildPrinterAndFontSections()
    {
        BuildPrinterSection();
        BuildFontSection();
        tlpColumns.ColumnStyles[0] = Percent(PrinterColumnPercent);
        tlpColumns.ColumnStyles[1] = Percent(100 - PrinterColumnPercent);
        tlpColumns.Controls.Add(secPrinter, 0, 0);
        tlpColumns.Controls.Add(secFont, 1, 0);
        return tlpColumns;
    }

    private void BuildPrinterSection()
    {
        StyleOverline(lblPrinterTitle, "MÁY IN PHIẾU (RIÊNG, KHÔNG ẢNH HƯỞNG MÁY IN TEM)");

        StyleTextBox(txtPrinterName, "Chưa chọn máy in phiếu");
        txtPrinterName.ReadOnly = true;

        btnCheckPrinter.Text = "Kiểm tra kết nối";
        btnCheckPrinter.Icon = IconGlyphs.Kind.Refresh;
        btnCheckPrinter.Variant = ButtonVariant.Outline;
        btnCheckPrinter.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnCheckPrinter.Size = new Size(btnCheckPrinter.GetContentWidth(S(16)), S(ControlHeight));
        btnCheckPrinter.Margin = new Padding(0, S(8), S(ColumnGap), 0);
        btnCheckPrinter.Click += async (_, _) => await CheckPrinterAsync();

        btnChangePrinter.Text = "Đổi máy in";
        btnChangePrinter.Icon = IconGlyphs.Kind.Settings;
        btnChangePrinter.Variant = ButtonVariant.Outline;
        btnChangePrinter.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnChangePrinter.Size = new Size(btnChangePrinter.GetContentWidth(S(16)), S(ControlHeight));
        btnChangePrinter.Margin = new Padding(0, S(8), 0, 0);
        btnChangePrinter.Click += (_, _) => PrinterSettingsRequested?.Invoke(this, EventArgs.Empty);

        StyleWrapping(lblPrinterStatus);

        AddRows(secPrinter, lblPrinterTitle, txtPrinterName,
            Grid([AutoColumn(), AutoColumn(), Percent(100)], btnCheckPrinter, btnChangePrinter, null), lblPrinterStatus);
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

        cboFont.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (string family in _fontService.GetInstalledFamilies())
            cboFont.Items.Add(family);
        AppTheme.StyleComboBox(cboFont);
        cboFont.SelectedIndexChanged += (_, _) => OnFontChanged();

        nudFontSize.Minimum = (decimal)WarehouseFontSettings.MinFontSize;
        nudFontSize.Maximum = (decimal)WarehouseFontSettings.MaxFontSize;
        nudFontSize.Increment = 0.5m;
        nudFontSize.DecimalPlaces = 1;
        nudFontSize.Value = 12;
        nudFontSize.BorderStyle = BorderStyle.FixedSingle;
        nudFontSize.Font = AppTheme.Fonts.Body;
        nudFontSize.TextAlign = HorizontalAlignment.Right;
        nudFontSize.ValueChanged += (_, _) => OnFontChanged();

        StyleInputRow(cboScope, cboFont, nudFontSize);

        lblFontPreview.Text = "Giày nữ Pucini · Kính mắt · Đôi / Cái · ẮẰẲẴẶ ỨỪỬỮỰ ỳỹ";
        lblFontPreview.BackColor = AppTheme.Colors.GridHeaderBack;
        lblFontPreview.ForeColor = AppTheme.Colors.TextPrimary;
        lblFontPreview.TextAlign = ContentAlignment.MiddleLeft;
        lblFontPreview.Padding = new Padding(S(10), 0, S(10), 0);
        lblFontPreview.AutoEllipsis = true;
        lblFontPreview.AutoSize = false;
        lblFontPreview.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        lblFontPreview.Margin = new Padding(0, S(10), 0, S(4));

        StyleOneLine(lblFontWarning, AppTheme.Fonts.Hint, AppTheme.Colors.TextMuted);

        AddRows(secFont, lblFontTitle,
            Grid([Percent(44), Percent(36), Percent(20)], lblScope, lblFontFamily, lblFontSize, cboScope, cboFont, nudFontSize),
            lblFontPreview, lblFontWarning);
    }

    private Control BuildPageSection()
    {
        StyleOverline(lblPageTitle, "KHỔ GIẤY VÀ THIẾT LẬP IN");
        StyleCaption(lblPageMode, "Chế độ");
        StyleCaption(lblPaper, "Khổ giấy");
        StyleCaption(lblOrientation, "Hướng giấy");
        StyleCaption(lblScaleMode, "Tỷ lệ in");

        cboPageMode.DropDownStyle = ComboBoxStyle.DropDownList;
        cboPageMode.Items.AddRange(["Giữ nguyên thiết lập in của file mẫu", "Tùy chỉnh (chỉ đổi mục khác mẫu)"]);
        cboPageMode.SelectedIndex = 0;
        AppTheme.StyleComboBox(cboPageMode);
        cboPageMode.SelectedIndexChanged += (_, _) => OnPageSettingsChanged();

        foreach (ComboBox combo in new[] { cboPaper, cboOrientation, cboScaleMode })
        {
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            AppTheme.StyleComboBox(combo);
            combo.SelectedIndexChanged += (_, _) => OnPageSettingsChanged();
        }
        cboOrientation.Items.AddRange(["Dọc", "Ngang"]);

        nudScale.Value = 100;
        StyleNumber(nudScale, WarehousePageSettings.MinScalePercent, WarehousePageSettings.MaxScalePercent, 0, 5);
        StyleInputRow(cboPageMode, cboPaper, cboOrientation, cboScaleMode, nudScale);

        // Dòng 2: 4 ô lề + 2 nút lấy / khôi phục thiết lập mẫu (hẹp thì 2 nút xuống dòng dưới).
        StyleCaption(lblMargins, "Lề trang (cm): Trái · Phải · Trên · Dưới");
        lblMargins.Margin = new Padding(0, S(12), 0, S(4));

        foreach (NumericUpDown margin in new[] { nudMarginLeft, nudMarginRight, nudMarginTop, nudMarginBottom })
        {
            StyleNumber(margin, 0, 10, 2, 0.1m);
            margin.Width = S(86);
            margin.Margin = new Padding(0, 0, S(8), 0);
            flpMargins.Controls.Add(margin);
        }

        StyleAction(btnLoadPage, "LẤY THIẾT LẬP TỪ FILE MẪU", IconGlyphs.Kind.Refresh, ButtonVariant.Outline, async () => await LoadPageFromTemplateAsync());
        StyleAction(btnRestorePage, "KHÔI PHỤC THIẾT LẬP MẪU", IconGlyphs.Kind.ArrowLeft, ButtonVariant.Outline, RestoreTemplatePage);
        foreach (RoundedButton button in new[] { btnRestorePage, btnLoadPage })
        {
            button.Size = new Size(button.GetContentWidth(S(16)), S(ControlHeight));
            flpPageButtons.Controls.Add(button);
        }

        tlpPageActions.ColumnStyles[0] = AutoColumn();
        tlpPageActions.ColumnStyles[1] = Percent(100);
        flpMargins.Anchor = AnchorStyles.Left;
        tlpPageActions.Controls.Add(flpMargins, 0, 0);
        tlpPageActions.Controls.Add(flpPageButtons, 1, 0);

        StyleWrapping(lblPageInfo);
        lblPageInfo.Margin = new Padding(0, S(10), 0, S(2));
        StyleOneLine(lblPageWarning, AppTheme.Fonts.Hint, AppTheme.Colors.TextMuted);

        // Dòng 1: [Chế độ] [Khổ giấy] [Hướng] [Tỷ lệ] [%]
        AddRows(secPage, lblPageTitle,
            Grid([Percent(30), Percent(28), Percent(12), Percent(20), Percent(10)],
                lblPageMode, lblPaper, lblOrientation, lblScaleMode, null,
                cboPageMode, cboPaper, cboOrientation, cboScaleMode, nudScale),
            lblMargins, tlpPageActions, lblPageInfo, lblPageWarning);
        return secPage;
    }

    /// <summary>Một hàng ô nhập cạnh nhau: giãn theo cột, cách nhau một khoảng (ô cuối sát mép phải).</summary>
    private void StyleInputRow(params Control[] inputs)
    {
        for (int i = 0; i < inputs.Length; i++)
        {
            inputs[i].Anchor = AnchorStyles.Left | AnchorStyles.Right;
            // Đáy chừa 2px: ComboBox vẽ cao hơn chiều cao nó báo cho TableLayoutPanel một chút — không chừa thì mất viền dưới.
            inputs[i].Margin = new Padding(0, 0, i < inputs.Length - 1 ? S(ColumnGap) : 0, S(2));
        }
    }

    private Control BuildPreviewSection()
    {
        TableLayoutPanel section = Stack();

        StyleOverline(lblPreviewTitle, "XEM TRƯỚC DỮ LIỆU  (SỬA ĐƯỢC TÊN HÀNG MỚI, ĐƠN VỊ, SỐ LƯỢNG)");
        section.Controls.Add(lblPreviewTitle, 0, 0);

        // Chế độ nhiều phiếu: chọn phiếu đang xem / sửa. Ẩn ở chế độ một phiếu.
        StyleCaption(lblBatchTicket, "Phiếu đang xem (file có nhiều phiếu chuyển — mỗi phiếu xuất ra một file riêng)");
        cboBatchTicket.DropDownStyle = ComboBoxStyle.DropDownList;
        AppTheme.StyleComboBox(cboBatchTicket);
        cboBatchTicket.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        cboBatchTicket.Margin = new Padding(0, 0, 0, S(10));
        cboBatchTicket.SelectedIndexChanged += (_, _) => OnBatchTicketSelected();
        AddRows(pnlBatch, lblBatchTicket, cboBatchTicket);
        pnlBatch.Visible = false;
        section.Controls.Add(pnlBatch, 0, 1);

        StyleOneLine(lblSummary, AppTheme.Fonts.BodyBold, AppTheme.Colors.TextSecondary);
        lblSummary.Margin = new Padding(0, 0, 0, S(6));
        section.Controls.Add(lblSummary, 0, 2);

        dgvRows.AllowUserToAddRows = false;
        dgvRows.AllowUserToDeleteRows = false;
        dgvRows.AllowUserToResizeRows = false;
        dgvRows.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgvRows.MultiSelect = false;
        dgvRows.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        dgvRows.ShowCellToolTips = true;
        AppTheme.StyleGrid(dgvRows);

        // Cột lấp đầy chiều rộng nhưng không hẹp hơn mức đọc được: cửa sổ hẹp
        // thì hiện thanh cuộn ngang thay vì bóp tên hàng thành "...".
        dgvRows.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvRows.ScrollBars = ScrollBars.Both;
        // Tiêu đề cột xuống dòng khi cột hẹp (không bị cắt "..."); chiều cao tiêu đề theo chữ.
        dgvRows.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        dgvRows.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        // Tên hàng / trạng thái dài xuống dòng. Đo MỌI dòng: chế độ "chỉ dòng đang hiển thị"
        // coi dòng chưa hiện là cao 36px → bảng tưởng đã tới cuối, không cuộn xuống được
        // các dòng cuối. Lúc đổ dữ liệu tạm tắt đo (xem FillGrid) để không chậm.
        dgvRows.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
        dgvRows.RowTemplate.MinimumHeight = S(36);

        AddColumn("colSourceRow", "Dòng", 5, readOnly: true, minWidth: S(56));
        AddColumn("colCode", "Mã hàng", 10, readOnly: true, minWidth: S(96));
        AddColumn("colSourceName", "Tên hàng gốc", 26, readOnly: true, minWidth: S(170), wrap: true);
        AddColumn("colTicketName", "Tên hàng mới (ghi lên phiếu)", 26, readOnly: false, minWidth: S(170), wrap: true);
        DataGridViewComboBoxColumn unit = new()
        {
            Name = "colUnit",
            HeaderText = "Đơn vị",
            FillWeight = 8,
            MinimumWidth = S(78),
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        unit.Items.AddRange(WarehouseUnitService.PairUnit, WarehouseUnitService.PieceUnit);
        dgvRows.Columns.Add(unit);
        AddColumn("colRequested", "SL yêu cầu", 9, readOnly: false, numeric: true, minWidth: S(84));
        AddColumn("colActual", "SL thực xuất", 9, readOnly: false, numeric: true, minWidth: S(84));
        AddColumn("colStatus", "Trạng thái", 27, readOnly: true, minWidth: S(170), wrap: true);

        dgvRows.CellBeginEdit += (_, e) => _editingValue = dgvRows.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();
        dgvRows.CellEndEdit += Grid_CellEndEdit;
        dgvRows.CurrentCellDirtyStateChanged += (_, _) =>
        {
            // Combo đơn vị: ghi nhận ngay khi chọn, không đợi rời ô.
            if (dgvRows.IsCurrentCellDirty && dgvRows.CurrentCell is DataGridViewComboBoxCell)
                dgvRows.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        dgvRows.CellValueChanged += (_, e) =>
        {
            if (!_fillingGrid && e.RowIndex >= 0 && dgvRows.Columns[e.ColumnIndex].Name == "colUnit")
                ApplyCellEdit(e.RowIndex, e.ColumnIndex);
        };
        dgvRows.DataError += (_, e) =>
        {
            // Giá trị ô không hiển thị được (vd đơn vị lạ trong combo): không ném lỗi ra
            // hộp thoại thô của DataGridView, nhưng ghi log để còn lần ra.
            e.ThrowException = false;
            if (e.Exception != null)
                WarehouseLog.Error($"Bảng xem trước: ô dòng {e.RowIndex + 1}, cột {dgvRows.Columns[e.ColumnIndex].HeaderText}", e.Exception);
        };
        // Chiều cao bảng do GridHeight() quyết định — không để hàng của bảng kéo giãn.
        dgvRows.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
        dgvRows.Margin = Padding.Empty;
        section.Controls.Add(dgvRows, 0, 3);
        return section;
    }

    private void AddColumn(string name, string header, float weight, bool readOnly, int minWidth, bool numeric = false, bool wrap = false)
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
        column.DefaultCellStyle.WrapMode = wrap ? DataGridViewTriState.True : DataGridViewTriState.False;
        dgvRows.Columns.Add(column);
    }

    private Control BuildFooter()
    {
        tlpFooter.Dock = DockStyle.Bottom;
        tlpFooter.Padding = new Padding(S(12), 0, S(12), 0);
        tlpFooter.BackColor = AppTheme.Colors.SurfaceElevated;

        Panel divider = new()
        {
            Height = 1,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 0, 0, S(12)),
            BackColor = AppTheme.Colors.Border
        };
        tlpFooter.Controls.Add(divider, 0, 0);

        StyleAction(btnValidate, "KIỂM TRA DỮ LIỆU", IconGlyphs.Kind.ShieldCheck, ButtonVariant.Outline, ValidateAndShow);
        StyleAction(btnPreview, "XEM TRƯỚC DỮ LIỆU", IconGlyphs.Kind.Eye, ButtonVariant.Outline, async () => await LoadDocumentAsync(showErrors: true));
        StyleAction(btnExport, "XUẤT PHIẾU EXCEL", IconGlyphs.Kind.Document, ButtonVariant.Primary, async () => await ExportAsync());
        StyleAction(btnPrintPreview, "XEM TRƯỚC BẢN IN", IconGlyphs.Kind.Eye, ButtonVariant.Outline, async () => await PrintPreviewAsync());
        StyleAction(btnPrint, "IN PHIẾU", IconGlyphs.Kind.Printer, ButtonVariant.Outline, async () => await PrintAsync(FindForm(), null));
        StyleAction(btnClear, "XÓA DỮ LIỆU", IconGlyphs.Kind.Trash, ButtonVariant.Outline, ClearData);
        StyleAction(btnOpenFolder, "Mở thư mục", IconGlyphs.Kind.Folder, ButtonVariant.Outline, OpenOutputFolder);
        foreach (RoundedButton button in Actions)
        {
            button.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            button.Height = S(ActionHeight);
            tlpActions.Controls.Add(button);
        }
        tlpActions.Dock = DockStyle.Fill;
        tlpFooter.Controls.Add(tlpActions, 0, 1);

        StyleOneLine(lblStatus, AppTheme.Fonts.Hint, AppTheme.Colors.TextSecondary);
        lblStatus.Text = "Sẵn sàng.";
        lblStatus.Margin = new Padding(0, S(2), 0, 0);
        tlpFooter.Controls.Add(lblStatus, 0, 2);
        return tlpFooter;
    }
    #endregion

    #region Bố cục co giãn
    /// <summary>
    /// Những gì phụ thuộc kích thước vùng cuộn: xếp máy in / font cạnh nhau hay
    /// chồng lên nhau, 2 nút thiết lập in cùng dòng với ô lề hay xuống dòng,
    /// và chiều cao bảng (số nút thao tác mỗi hàng: xem <see cref="ArrangeActions"/>).
    /// Chỉ đổi khi thật sự cần (tránh dàn trang lại liên tục).
    /// </summary>
    private void ApplyResponsiveLayout()
    {
        int contentWidth = Math.Max(tlpContent.MinimumSize.Width, pnlScroll.ClientSize.Width) - tlpContent.Padding.Horizontal;
        if (_applyingLayout || contentWidth <= 0 || pnlScroll.ClientSize.Height <= 0)
            return;

        _applyingLayout = true;
        Size measuredFor = pnlScroll.ClientSize;
        pnlScroll.SuspendLayout();
        try
        {
            bool sideBySide = contentWidth >= S(SideBySideMinWidth);
            SetSideBySide(sideBySide);
            // Bề rộng xuống dòng tính TẠI ĐÂY (ngoài lượt dàn trang của các bảng con):
            // đổi kích thước nhãn giữa chừng lượt dàn trang của bảng cha thì bảng cha
            // không đo lại, giữ nguyên chiều cao cũ (từng thành dải trống hàng nghìn px).
            SetWrapWidth(lblPageInfo, contentWidth);
            SetWrapWidth(lblPrinterStatus, sideBySide ? (int)(contentWidth * PrinterColumnPercent / 100f) - secPrinter.Margin.Horizontal : contentWidth);
            SetPageButtonsInline(contentWidth >= flpMargins.PreferredSize.Width + flpPageButtons.PreferredSize.Width + S(ColumnGap * 2));
            dgvRows.Height = GridHeight();
        }
        finally
        {
            pnlScroll.ResumeLayout(true);
            _applyingLayout = false;
        }

        // Thanh cuộn dọc vừa hiện / ẩn trong lượt này (bề rộng đổi ~17px): tính lại một lần
        // cho đúng bề rộng mới. Chỉ một lần — không để hai trạng thái thanh cuộn đuổi nhau.
        if (pnlScroll.ClientSize != measuredFor && !_relayoutPending)
        {
            _relayoutPending = true;
            try
            {
                ApplyResponsiveLayout();
            }
            finally
            {
                _relayoutPending = false;
            }
        }
    }

    /// <summary>
    /// Bảng cao vừa đúng phần nhìn thấy của vùng cuộn (trừ tiêu đề + dòng tổng
    /// hợp phía trên bảng): cuộn trang xuống tới bảng là thấy trọn bảng, chỉ
    /// còn một thanh cuộn (của bảng) làm việc. Không thấp hơn vài dòng hàng.
    /// </summary>
    private int GridHeight()
    {
        int above = lblPreviewTitle.Height + lblPreviewTitle.Margin.Vertical + lblSummary.Height + lblSummary.Margin.Vertical +
                    (pnlBatch.Visible ? pnlBatch.PreferredSize.Height + pnlBatch.Margin.Vertical : 0);
        int available = pnlScroll.ClientSize.Height - tlpContent.Padding.Vertical - above;
        int minimum = dgvRows.ColumnHeadersHeight + dgvRows.RowTemplate.MinimumHeight * MinVisibleGridRows + S(2);
        return Math.Max(minimum, available);
    }

    private void SetSideBySide(bool sideBySide)
    {
        if (_sideBySide == sideBySide)
            return;
        _sideBySide = sideBySide;

        // Hẹp: font xuống dưới máy in, cột phải thu về 0 (đổi kiểu cột thay vì gộp ô).
        tlpColumns.ColumnStyles[0] = Percent(sideBySide ? PrinterColumnPercent : 100);
        tlpColumns.ColumnStyles[1] = sideBySide ? Percent(100 - PrinterColumnPercent) : new ColumnStyle(SizeType.Absolute, 0);
        secPrinter.Margin = new Padding(0, 0, sideBySide ? S(28) : 0, 0);
        secFont.Margin = new Padding(0, sideBySide ? 0 : S(SectionGap), 0, 0);
        tlpColumns.SetCellPosition(secFont, new TableLayoutPanelCellPosition(sideBySide ? 1 : 0, sideBySide ? 0 : 1));
    }

    private void SetPageButtonsInline(bool inline)
    {
        if (_pageButtonsInline == inline)
            return;
        _pageButtonsInline = inline;

        // Cùng dòng: 2 nút dồn sát phải. Xuống dòng: nằm dưới các ô lề, dồn trái,
        // "Lấy thiết lập" đứng trước "Khôi phục" như thứ tự đọc.
        flpPageButtons.FlowDirection = inline ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        flpPageButtons.Anchor = inline ? AnchorStyles.Right : AnchorStyles.Left;
        flpPageButtons.Margin = new Padding(0, inline ? 0 : S(10), 0, 0);
        foreach (RoundedButton button in new[] { btnRestorePage, btnLoadPage })
            button.Margin = inline ? new Padding(S(ColumnGap), 0, 0, 0) : new Padding(0, 0, S(ColumnGap), 0);
        flpPageButtons.Controls.SetChildIndex(btnLoadPage, inline ? 1 : 0);
        tlpPageActions.SetCellPosition(flpPageButtons, new TableLayoutPanelCellPosition(inline ? 1 : 0, inline ? 0 : 1));
    }

    /// <summary>
    /// Số nút thao tác mỗi hàng = bao nhiêu nút vừa mà chữ không bị cắt. Gọi
    /// từ sự kiện Layout của card (xem BuildUi), không gọi giữa lượt dàn trang.
    /// </summary>
    private void ArrangeActions(int width)
    {
        // Cửa sổ thu nhỏ xuống thanh tác vụ (card 0×0): giữ nguyên cách xếp hiện tại.
        if (width <= 0)
            return;

        RoundedButton[] actions = Actions;
        int gap = S(ColumnGap);
        int minButton = actions.Max(b => b.GetContentWidth(S(14)));
        int perRow = Math.Clamp((width + gap) / (minButton + gap), 1, actions.Length);
        // Chia đều hai hàng (4 + 3) thay vì 6 + 1 khi không vừa một hàng.
        int rows = (actions.Length + perRow - 1) / perRow;
        perRow = (actions.Length + rows - 1) / rows;
        if (perRow == _actionsPerRow)
            return;
        _actionsPerRow = perRow;

        tlpActions.SuspendLayout();
        tlpActions.ColumnStyles.Clear();
        tlpActions.ColumnCount = perRow;
        for (int c = 0; c < perRow; c++)
            tlpActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / perRow));
        tlpActions.RowCount = rows;
        for (int i = 0; i < actions.Length; i++)
        {
            int column = i % perRow;
            int row = i / perRow;
            actions[i].Margin = new Padding(0, 0, column < perRow - 1 ? gap : 0, row < rows - 1 ? S(10) : 0);
            tlpActions.SetCellPosition(actions[i], new TableLayoutPanelCellPosition(column, row));
        }
        tlpActions.ResumeLayout(true);
    }

    /// <summary>
    /// Lăn chuột trên ô chọn / ô số khi chúng chưa được bấm vào: cuộn trang,
    /// KHÔNG đổi giá trị. Nếu không, cuộn trang lướt qua sẽ âm thầm đổi máy
    /// in, font, khổ giấy, lề... (và làm bản đã xuất thành bản cũ). Lăn trên
    /// bảng đã cuộn tới đầu/cuối thì cuộn tiếp trang thay vì đứng yên.
    /// </summary>
    private void RouteWheelToPage()
    {
        foreach (Control control in new Control[]
                 {
                     dtpDocumentDate, cboBatchTicket, cboScope, cboFont, nudFontSize, cboPageMode, cboPaper, cboOrientation, cboScaleMode,
                     nudScale, nudMarginLeft, nudMarginRight, nudMarginTop, nudMarginBottom
                 })
        {
            control.MouseWheel += (_, e) =>
            {
                bool active = control is ComboBox combo ? combo.DroppedDown : control.ContainsFocus;
                if (active || e is not HandledMouseEventArgs handled)
                    return;
                handled.Handled = true;
                pnlScroll.ScrollByWheel(e.Delta);
            };
        }

        dgvRows.MouseWheel += (_, e) =>
        {
            if (GridCanScroll(e.Delta) || e is not HandledMouseEventArgs handled)
                return;
            handled.Handled = true;
            pnlScroll.ScrollByWheel(e.Delta);
        };
    }

    private bool GridCanScroll(int delta)
    {
        if (dgvRows.RowCount == 0 || dgvRows.FirstDisplayedScrollingRowIndex < 0)
            return false;
        if (delta > 0)
            return dgvRows.FirstDisplayedScrollingRowIndex > 0;
        return dgvRows.FirstDisplayedScrollingRowIndex + dgvRows.DisplayedRowCount(false) < dgvRows.RowCount;
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
    /// <summary>Mã phiếu đang hiển thị: chế độ nhiều phiếu lấy từ mã chuyển hàng của phiếu đang chọn.</summary>
    private TicketCodeResult CurrentTicketCode => _batchTicket != null
        ? _batchTicket.TicketCode(txtPrefix.Text)
        : WarehouseTicketCodeService.Normalize(txtTicketCode.Text, txtPrefix.Text);

    private void OnTicketInputChanged()
    {
        MarkAllStale();
        UpdateState();
    }
    #endregion

    #region Nhiều phiếu
    /// <summary>
    /// Vào / thoát chế độ nhiều phiếu. <paramref name="batch"/> = null: chế độ
    /// một phiếu với <paramref name="single"/>. Mọi trạng thái xuất cũ bị bỏ
    /// (dữ liệu vừa được đọc lại).
    /// </summary>
    private void SetDocuments(WarehouseDocument? single, List<WarehouseBatchTicket>? batch)
    {
        _singleExport.Reset();
        _batch = batch;
        _batchTicket = batch?[0];
        _document = _batchTicket?.Document ?? single;

        _fillingBatch = true;
        try
        {
            cboBatchTicket.Items.Clear();
            if (batch != null)
            {
                foreach (WarehouseBatchTicket ticket in batch)
                    cboBatchTicket.Items.Add(DescribeBatchTicket(ticket));
                cboBatchTicket.SelectedIndex = 0;
            }
        }
        finally
        {
            _fillingBatch = false;
        }

        if (pnlBatch.Visible != BatchMode)
        {
            pnlBatch.Visible = BatchMode;
            ApplyResponsiveLayout();
        }
    }

    private void OnBatchTicketSelected()
    {
        if (_fillingBatch || _batch == null)
            return;

        int index = cboBatchTicket.SelectedIndex;
        if (index < 0 || index >= _batch.Count || _batch[index] == _batchTicket)
            return;

        // Ô đang sửa dở thuộc phiếu cũ — ghi nhận vào phiếu cũ trước khi đổi bảng.
        dgvRows.EndEdit();
        _batchTicket = _batch[index];
        _document = _batchTicket.Document;
        FillGrid();
    }

    private void SelectBatchTicket(WarehouseBatchTicket ticket)
    {
        int index = _batch?.IndexOf(ticket) ?? -1;
        if (index >= 0)
            cboBatchTicket.SelectedIndex = index;
    }

    /// <summary>Dòng hiển thị của một phiếu trong ô chọn phiếu: mã · ghi chú · số dòng · tình trạng.</summary>
    private string DescribeBatchTicket(WarehouseBatchTicket ticket)
    {
        TicketCodeResult code = ticket.TicketCode(txtPrefix.Text);
        string name = TicketTag(ticket);
        string note = ticket.Note.Length > 0 ? $" · {ticket.Note}" : "";
        string rows = $" · {ticket.Document.Rows.Count} dòng, SL {ticket.TotalQuantity:0.###}";

        WarehouseExportState state = ticket.Export.Check();
        string status = !code.IsValid || ticket.Document.ErrorCount > 0
            ? $"✗ {(code.IsValid ? $"{ticket.Document.ErrorCount} dòng lỗi" : "mã phiếu lỗi")}"
            : state == WarehouseExportState.Current
                ? ticket.Export.WasPreviewed ? "✓ đã xuất, đã xem trước" : "đã xuất, CHƯA xem trước"
                : state == WarehouseExportState.None ? "chưa xuất" : "cần xuất lại";
        return $"{name}{note}{rows} — {status}";
    }

    /// <summary>Cập nhật chữ của các phiếu trong ô chọn (chỉ dòng nào đổi).</summary>
    private void RefreshBatchItems()
    {
        if (_batch == null)
            return;

        _fillingBatch = true;
        try
        {
            int selected = cboBatchTicket.SelectedIndex;
            for (int i = 0; i < _batch.Count && i < cboBatchTicket.Items.Count; i++)
            {
                string text = DescribeBatchTicket(_batch[i]);
                if (!string.Equals(cboBatchTicket.Items[i] as string, text, StringComparison.Ordinal))
                    cboBatchTicket.Items[i] = text;
            }
            if (cboBatchTicket.SelectedIndex != selected)
                cboBatchTicket.SelectedIndex = selected;
        }
        finally
        {
            _fillingBatch = false;
        }
    }

    /// <summary>Kiểm tra một phiếu của chế độ nhiều phiếu (mã lấy từ mã chuyển hàng).</summary>
    private WarehouseValidationReport BuildReport(WarehouseBatchTicket ticket) =>
        _documentService.Validate(ticket.TicketCode(txtPrefix.Text), SelectedDocumentDate, ticket.Document, txtOutput.Text.Trim(),
            CurrentFontSettings(), CurrentPageSettings());

    /// <summary>Gộp kết quả kiểm tra của mọi phiếu; mỗi lỗi / cảnh báo ghi rõ thuộc phiếu nào.</summary>
    private (WarehouseValidationReport Combined, List<(WarehouseBatchTicket Ticket, WarehouseValidationReport Report)> Each) BuildBatchReport()
    {
        List<(WarehouseBatchTicket Ticket, WarehouseValidationReport Report)> each =
            _batch!.Select(t => (t, BuildReport(t))).ToList();

        WarehouseValidationReport combined = new();
        Merge(r => r.Errors, combined.Errors);
        Merge(r => r.Warnings, combined.Warnings);
        return (combined, each);

        // Thông báo có ở MỌI phiếu (ngày, font, thư mục, thiết lập in...) là lỗi chung — ghi một
        // lần; còn lại ghi kèm mã phiếu để biết phải sửa phiếu nào.
        void Merge(Func<WarehouseValidationReport, List<string>> pick, List<string> target)
        {
            foreach ((WarehouseBatchTicket ticket, WarehouseValidationReport report) in each)
            {
                foreach (string message in pick(report))
                {
                    string text = each.All(e => pick(e.Report).Contains(message)) ? message : $"[{TicketTag(ticket)}] {message}";
                    if (!target.Contains(text))
                        target.Add(text);
                }
            }
        }
    }

    private string TicketTag(WarehouseBatchTicket ticket)
    {
        TicketCodeResult code = ticket.TicketCode(txtPrefix.Text);
        return code.IsValid ? code.Code! : ticket.TransferCode.Length > 0 ? ticket.TransferCode : "thiếu mã chuyển hàng";
    }

    /// <summary>
    /// Các phiếu (chế độ nhiều phiếu) chưa được xem trước bản in ở bản xuất hiện
    /// hành. Xem trước KHÔNG bắt buộc — danh sách này chỉ dùng để nhắc người
    /// dùng (hộp xác nhận in, dòng hướng dẫn), không dùng để chặn in.
    /// </summary>
    private List<WarehouseBatchTicket> UnpreviewedTickets() =>
        _batch?.Where(t => !(t.Export.Check() == WarehouseExportState.Current && t.Export.WasPreviewed)).ToList() ?? [];
    #endregion

    #region Ngày lập phiếu
    /// <summary>Ngày lập phiếu đang chọn (chỉ phần ngày) — giá trị DUY NHẤT được đưa vào kiểm tra và file xuất.</summary>
    internal DateTime SelectedDocumentDate => dtpDocumentDate.Value.Date;

    private void SetDocumentDate(DateTime date)
    {
        _settingDocumentDate = true;
        try
        {
            // Đồng hồ máy sai lệch quá xa (ngoài giới hạn của ô chọn) thì đưa về mốc gần nhất thay vì ném lỗi.
            dtpDocumentDate.Value = date.Date < dtpDocumentDate.MinDate ? dtpDocumentDate.MinDate
                : date.Date > dtpDocumentDate.MaxDate ? dtpDocumentDate.MaxDate
                : date.Date;
        }
        finally
        {
            _settingDocumentDate = false;
        }
    }

    private void OnDocumentDateChanged()
    {
        if (_settingDocumentDate)
            return;

        // Chọn lại đúng hôm nay = quay về mặc định (sang ngày mới lại đi theo ngày mới).
        _documentDateChosen = SelectedDocumentDate != _today().Date;
        MarkAllStale();
        UpdateState();
    }

    /// <summary>
    /// Ngày mặc định đi theo ngày hiện tại của máy: màn hình giữ nguyên khi
    /// chuyển qua lại trong FormMain, nên mỗi lần mở lại (hoặc xoá dữ liệu để
    /// làm phiếu mới) mà đã sang ngày mới thì đổi sang ngày mới. Ngày người dùng
    /// đã chọn thì không bao giờ bị thay.
    /// </summary>
    private void RefreshDefaultDocumentDate()
    {
        DateTime today = _today().Date;
        if (_documentDateChosen || SelectedDocumentDate == today)
            return;

        SetDocumentDate(today);
        // Bản đã xuất (nếu có) mang ngày cũ — không được in nữa.
        MarkAllStale();
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
        MarkAllStale();
        UpdateState();
    }

    /// <summary>Nạp file mẫu + file hàng hóa không qua hộp thoại chọn file (dùng cho test bố cục với dữ liệu thật).</summary>
    internal async Task LoadFilesAsync(string templatePath, string goodsPath)
    {
        txtGoods.Text = goodsPath;
        await SetTemplateAsync(templatePath, showErrors: false);
    }

    private static string? FolderOf(string path) =>
        string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(path);

    private async Task SetTemplateAsync(string path, bool showErrors)
    {
        if (_busy)
            return;

        txtTemplate.Text = path;
        _layout = null;
        _templatePage = null;
        MarkAllStale();
        SetDocuments(null, null);
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
        catch (Exception ex)
        {
            ReportUnexpected(null, "đọc file phiếu mẫu", ex, showErrors);
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
        if (_busy)
            return;

        string template = txtTemplate.Text;
        string goods = txtGoods.Text;
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(goods))
        {
            if (showErrors)
                MessageBox.Show("Hãy chọn cả file phiếu mẫu và file hàng hóa trước khi xem trước.", Title,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        dgvRows.CancelEdit();
        MarkAllStale();
        SetDocuments(null, null);
        string? pageNote = null;
        try
        {
            SetBusy(true, "Đang đọc và đối chiếu dữ liệu...");
            (WarehouseDocument loaded, List<WarehouseBatchTicket>? batch) = await Task.Run(() =>
            {
                WarehouseDocument document = _documentService.Load(template, goods);
                return (document, _documentService.SplitByTransfer(document));
            });
            SetDocuments(loaded, batch);
            _layout = loaded.Layout;

            // File chỉ có MỘT phiếu chuyển: điền sẵn mã phiếu từ mã chuyển hàng (không đè mã người dùng đã gõ).
            if (batch == null && txtTicketCode.Text.Trim().Length == 0 &&
                WarehouseDocumentService.SingleTransferCode(loaded.Goods) is { } transfer &&
                WarehouseTicketCodeService.FromTransferCode(transfer, "") is { IsValid: true } body)
                txtTicketCode.Text = body.Code!;

            // File mẫu vừa được đọc lại; nếu thiết lập in của nó đã khác lần đọc trước
            // (mẫu bị sửa bên ngoài) thì phần "chỉ đổi mục khác mẫu" đang so với số
            // liệu cũ — đưa về đúng thiết lập hiện tại của mẫu.
            if (_templatePage != _layout.Page)
            {
                bool hadTemplate = _templatePage != null;
                _templatePage = _layout.Page;
                FillPageControls(_templatePage, keepTemplate: true);
                if (hadTemplate)
                    pageNote = " Thiết lập in của file mẫu đã thay đổi — đã cập nhật theo file mẫu.";
            }
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Đọc dữ liệu phiếu: mẫu={template}, hàng hóa={goods}", ex);
            if (showErrors)
                MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            ReportUnexpected(null, "đọc dữ liệu hàng hóa", ex, showErrors);
        }
        finally
        {
            FillGrid();
            SetBusy(false, _batch != null
                ? $"Đã đọc {_batch.Count} phiếu chuyển ({_batch.Sum(t => t.Document.Rows.Count)} dòng hàng) từ {Path.GetFileName(goods)} — " +
                  "mỗi phiếu sẽ xuất ra một file riêng." + pageNote
                : _document != null
                    ? $"Đã đọc {_document.Rows.Count} dòng hàng từ {Path.GetFileName(goods)}." + pageNote
                    : "Chưa đọc được dữ liệu.");
        }
    }

    private void FillGrid()
    {
        _fillingGrid = true;
        dgvRows.SuspendLayout();
        // Thêm từng dòng mà vẫn bật tự đo thì mỗi lần thêm lại đo — tắt đi, bật lại cuối cùng (đo một lượt).
        dgvRows.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
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
            dgvRows.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
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

    /// <summary>Giá trị ô lúc bắt đầu sửa — để biết người dùng có thật sự đổi gì không.</summary>
    private string? _editingValue;

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        string? before = _editingValue;
        _editingValue = null;
        if (e.RowIndex < 0 || dgvRows.Columns[e.ColumnIndex].Name == "colUnit")
            return; // đơn vị đã xử lý ở CellValueChanged

        // Vào ô rồi thoát ra không sửa gì (Esc, bấm chỗ khác): bản đã xuất vẫn đúng, không bắt xuất lại.
        string after = dgvRows.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString() ?? "";
        if (before == after)
            return;

        ApplyCellEdit(e.RowIndex, e.ColumnIndex);
    }

    private void ApplyCellEdit(int rowIndex, int columnIndex)
    {
        if (dgvRows.Rows[rowIndex].Tag is not WarehouseProductRow item)
            return;

        DataGridViewRow gridRow = dgvRows.Rows[rowIndex];
        string column = dgvRows.Columns[columnIndex].Name;
        string text = gridRow.Cells[columnIndex].Value?.ToString() ?? "";

        switch (column)
        {
            case "colTicketName":
                item.TicketName = VietnameseText.CollapseSpaces(text);
                gridRow.Cells[columnIndex].Value = item.TicketName;
                break;
            case "colUnit":
                if (item.Unit == text)
                    return;
                item.Unit = text;
                break;
            case "colRequested":
                item.RequestedQuantity = ParseQuantity(text);
                gridRow.Cells[columnIndex].Value = FormatQuantity(item.RequestedQuantity) is { Length: > 0 } r ? r : text;
                break;
            case "colActual":
                item.ActualQuantity = ParseQuantity(text);
                gridRow.Cells[columnIndex].Value = FormatQuantity(item.ActualQuantity) is { Length: > 0 } a ? a : text;
                break;
            default:
                return;
        }

        WarehouseProductNameService.Revalidate(item);
        PaintRow(gridRow, item);
        // Chỉ phiếu đang hiển thị đổi dữ liệu — các phiếu khác (chế độ nhiều phiếu) vẫn giữ bản đã xuất.
        CurrentExport.MarkStale();
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
    /// <summary>
    /// Kiểm tra (lại) kết nối tới máy in đã chọn. Đang có lần kiểm tra cho
    /// đúng máy in này thì dùng chung, không mở thêm lệnh hỏi spooler.
    /// </summary>
    private Task CheckPrinterAsync()
    {
        if (_printerCheck is { IsCompleted: false } && _printerCheckFor == SelectedPrinter)
            return _printerCheck;

        _printerCheckFor = SelectedPrinter;
        return _printerCheck = RunPrinterCheckAsync(_printerCheckFor);
    }

    // Hỏi spooler ở luồng nền, tối đa SpoolerQuery.Timeout. Kết quả chỉ áp
    // dụng nếu máy in không bị đổi trong lúc chờ.
    private async Task RunPrinterCheckAsync(string printer)
    {
        if (printer.Length == 0)
        {
            _printerState = PrinterState.Unknown;
            UpdateState();
            return;
        }

        _printerState = PrinterState.Checking;
        UpdateState();

        PrinterState state;
        try
        {
            state = await SpoolerQuery.RunAsync(() => _printerService.CheckPrinter(printer)) switch
            {
                PrinterCheckResult.Ready => PrinterState.Ready,
                PrinterCheckResult.NotInstalled => PrinterState.NotInstalled,
                PrinterCheckResult.Invalid => PrinterState.Invalid,
                _ => PrinterState.Unknown,
            };
        }
        catch (TimeoutException ex)
        {
            WarehouseLog.Error($"Kiểm tra máy in \"{printer}\": spooler không phản hồi", ex);
            state = PrinterState.NotResponding;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or System.Drawing.Printing.InvalidPrinterException)
        {
            WarehouseLog.Error($"Kiểm tra máy in \"{printer}\"", ex);
            state = PrinterState.Invalid;
        }

        if (IsDisposed || printer != SelectedPrinter)
            return;

        _printerState = state;
        UpdateState();
    }

    private string DescribePrinterProblem(string printer) => _printerState switch
    {
        PrinterState.NotInstalled =>
            $"Máy in \"{printer}\" không còn trong Windows (đã bị gỡ hoặc đổi tên) — bấm Đổi máy in để chọn lại.",
        PrinterState.Invalid =>
            $"Windows không mở được máy in \"{printer}\" — kiểm tra máy in đã bật, cáp / mạng còn kết nối, rồi bấm Kiểm tra kết nối.",
        PrinterState.NotResponding =>
            "Windows không phản hồi khi hỏi máy in (Print Spooler đang treo, thường do một máy in bị lỗi). " +
            "Bật lại máy in, đợi một lát rồi bấm Kiểm tra kết nối; vẫn lỗi thì khởi động lại máy tính. Các chức năng khác vẫn dùng bình thường.",
        _ => $"Máy in \"{printer}\" chưa sẵn sàng.",
    };

    private string SelectedPrinter => _settings.PrinterName;
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

        MarkAllStale();
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
        if (_busy)
            return;

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
        catch (Exception ex)
        {
            ReportUnexpected(null, "đọc thiết lập in của file mẫu", ex, show: true);
        }
        finally
        {
            SetBusy(false, layout != null ? "Đã lấy thiết lập in từ file mẫu." : "Chưa đọc được thiết lập in của mẫu.");
        }

        if (layout == null)
            return;

        _templatePage = layout.Page;
        FillPageControls(layout.Page, keepTemplate: false);
        MarkAllStale();
        UpdateState();
    }

    private void RestoreTemplatePage()
    {
        if (_templatePage == null)
            return;

        FillPageControls(_templatePage, keepTemplate: true);
        MarkAllStale();
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
        MarkAllStale();
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

        Font desired = font ?? AppTheme.Fonts.Body;
        Font? old = _previewFont;
        lblFontPreview.Font = desired;
        if (ReferenceEquals(lblFontPreview.Font, desired))
        {
            _previewFont = font;
            if (old != null && !ReferenceEquals(old, desired))
                old.Dispose();
        }
        else
        {
            // Control.Font bỏ qua font "bằng" font đang dùng (cùng họ/cỡ/kiểu) và giữ
            // nguyên đối tượng cũ — khi đó phải giữ font cũ, bỏ font mới. Dispose font
            // cũ ở đây sẽ để ô mẫu chữ cầm một font đã giải phóng ("Parameter is not valid").
            font?.Dispose();
        }
        // Ô mẫu chữ cao theo cỡ chữ đang chọn — chữ to không bị cắt nửa dòng.
        lblFontPreview.Height = Math.Max(S(36), lblFontPreview.Font.Height + S(10));

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
        _documentService.Validate(txtTicketCode.Text, txtPrefix.Text, SelectedDocumentDate, _document, txtOutput.Text.Trim(),
            CurrentFontSettings(), CurrentPageSettings());

    private void ValidateAndShow()
    {
        dgvRows.EndEdit();
        if (BatchMode)
        {
            ValidateBatchAndShow();
            return;
        }

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

    private void ValidateBatchAndShow()
    {
        (WarehouseValidationReport report, var each) = BuildBatchReport();
        List<string> existing = each.Where(e => e.Report.ExistingOutputPath != null)
            .Select(e => Path.GetFileName(e.Report.ExistingOutputPath!)).ToList();
        string exists = existing.Count > 0
            ? $"\n\nLưu ý: {existing.Count} file đã có trong thư mục output ({string.Join(", ", existing)}) — khi xuất sẽ hỏi trước khi ghi đè."
            : "";
        RefreshBatchItems();

        if (report.CanExport && report.Warnings.Count == 0)
        {
            MessageBox.Show($"Dữ liệu hợp lệ — {_batch!.Count} phiếu, {_batch.Sum(t => t.Document.Rows.Count)} dòng hàng, sẵn sàng xuất {_batch.Count} file:\n" +
                            string.Join("\n", _batch.Select(t => "  • " + TicketTag(t))) + exists,
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show(FormatReport(report) + exists, Title, MessageBoxButtons.OK,
            report.CanExport ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    /// <summary>
    /// Chế độ nhiều phiếu: kiểm tra TẤT CẢ phiếu trước (một phiếu lỗi thì không
    /// xuất phiếu nào), hỏi ghi đè một lần cho mọi file đã có, rồi xuất lần
    /// lượt — mỗi phiếu một file, mỗi file được mở lại kiểm tra như khi xuất lẻ.
    /// </summary>
    private async Task ExportBatchAsync()
    {
        List<WarehouseBatchTicket> batch = _batch!;
        (WarehouseValidationReport report, var each) = BuildBatchReport();
        if (!report.CanExport)
        {
            MessageBox.Show(FormatReport(report) + "\n\nChưa xuất phiếu nào — chọn phiếu có lỗi ở ô \"Phiếu đang xem\" để sửa.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RefreshBatchItems();
            return;
        }

        if (report.Warnings.Count > 0 &&
            MessageBox.Show(FormatReport(report) + $"\n\nVẫn xuất {batch.Count} phiếu?", Title, MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        string folder = txtOutput.Text.Trim();
        HashSet<string> overwrite = each.Where(e => e.Report.ExistingOutputPath != null)
            .Select(e => Path.GetFullPath(e.Report.ExistingOutputPath!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (overwrite.Count > 0 &&
            MessageBox.Show($"{overwrite.Count} file đã có trong thư mục output:\n{string.Join("\n", overwrite.Select(f => "  • " + Path.GetFileName(f)))}\n\nGhi đè các file này?",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        // Ngày / font / thiết lập in chốt MỘT lần cho cả lô.
        DateTime date = SelectedDocumentDate;
        WarehouseFontSettings font = CurrentFontSettings();
        WarehousePageSettings page = CurrentPageSettings();
        string prefix = txtPrefix.Text;

        List<(string Code, WarehouseExportResult Result)> done = new();
        List<string> failed = new();
        try
        {
            SetBusy(true, $"Đang xuất {batch.Count} phiếu...");
            for (int i = 0; i < batch.Count; i++)
            {
                WarehouseBatchTicket ticket = batch[i];
                string code = ticket.TicketCode(prefix).Code!;
                string output = WarehouseDocumentService.BuildOutputPath(folder, code, ticket.Document.Layout.FileExtension);
                lblStatus.Text = $"Đang xuất phiếu {i + 1}/{batch.Count}: {code}...";

                WarehouseExportRequest request = new()
                {
                    TemplatePath = ticket.Document.TemplatePath,
                    OutputPath = output,
                    TicketCode = code,
                    DocumentDate = date,
                    Rows = ticket.Document.Rows,
                    Font = font,
                    Page = page,
                    // Chỉ ghi đè file người dùng vừa đồng ý; file mới xuất hiện sau đó thì Export báo lỗi.
                    OverwriteExisting = overwrite.Contains(Path.GetFullPath(output))
                };

                try
                {
                    WarehouseExportResult result = await Task.Run(() => _documentService.Export(request));
                    ticket.Export.Record(result);
                    done.Add((code, result));
                    WarehouseLog.Info($"Xuất phiếu (lô {i + 1}/{batch.Count}) {code} từ {ticket.TransferCode} ngày {date:dd/MM/yyyy}: {result.OutputPath} " +
                                      $"({result.RowCount} dòng, chênh {result.RowDelta}), font={font.Scope}, thiết lập in: {page.Describe()}");
                }
                catch (WarehouseException ex)
                {
                    WarehouseLog.Error($"Xuất phiếu (lô) {code} → {output}", ex);
                    failed.Add($"{code}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    WarehouseLog.Error($"Lỗi ngoài dự kiến khi xuất phiếu (lô) {code} → {output}", ex);
                    failed.Add($"{code}: lỗi ngoài dự kiến — {ex.Message}");
                }
            }
        }
        finally
        {
            SetBusy(false, failed.Count == 0
                ? $"Đã xuất và kiểm tra lại {done.Count} phiếu vào {folder}."
                : $"Đã xuất {done.Count}/{batch.Count} phiếu — {failed.Count} phiếu lỗi.");
            RefreshBatchItems();
        }

        string message = done.Count > 0
            ? $"Đã xuất {done.Count}/{batch.Count} phiếu (mỗi phiếu một file, đã mở lại từng file để kiểm tra):\n" +
              string.Join("\n", done.Select(d => $"  ✓ {Path.GetFileName(d.Result.OutputPath)} — {d.Result.RowCount} dòng hàng"))
            : "Chưa xuất được phiếu nào.";
        if (failed.Count > 0)
            message += "\n\nKHÔNG XUẤT ĐƯỢC:\n" + string.Join("\n", failed.Select(f => "  ✗ " + f));
        if (done.Count > 0)
            message += "\n\nBước tiếp theo: bấm IN PHIẾU để in tất cả phiếu, hoặc XEM TRƯỚC BẢN IN để kiểm tra từng trang trước (không bắt buộc).";
        MessageBox.Show(message, Title, MessageBoxButtons.OK, failed.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
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
        if (_busy)
            return;

        dgvRows.EndEdit();
        if (BatchMode)
        {
            await ExportBatchAsync();
            return;
        }

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
            DocumentDate = SelectedDocumentDate,
            Rows = document.Rows,
            Font = CurrentFontSettings(),
            Page = CurrentPageSettings(),
            OverwriteExisting = overwrite
        };

        WarehouseExportResult? result = null;
        try
        {
            SetBusy(true, $"Đang xuất phiếu {code}...");
            result = await Task.Run(() => _documentService.Export(request));
            CurrentExport.Record(result);
            WarehouseLog.Info($"Xuất phiếu {code} ngày {request.DocumentDate:dd/MM/yyyy}: {result.OutputPath} ({result.RowCount} dòng, chênh {result.RowDelta}), " +
                              $"font={request.Font.Scope}, thiết lập in: {request.Page.Describe()}");
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Xuất phiếu {code} → {output}", ex);
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            ReportUnexpected(null, $"xuất phiếu {code}", ex, show: true);
        }
        finally
        {
            SetBusy(false, result != null ? $"Đã xuất và kiểm tra lại: {result.OutputPath}" : "Chưa xuất được phiếu.");
        }

        // Thông báo thành công chỉ sau khi file đã được mở lại và kiểm tra (Export ném lỗi nếu không đạt).
        if (result != null)
        {
            MessageBox.Show(
                $"Đã xuất phiếu {code} ({result.RowCount} dòng hàng):\n{result.OutputPath}\n\n" +
                "Đã mở lại file và kiểm tra:\n" + string.Join("\n", result.VerifiedChecks.Select(c => "  ✓ " + c)) +
                "\n\nBước tiếp theo: bấm IN PHIẾU để in, hoặc XEM TRƯỚC BẢN IN để kiểm tra từng trang trước (không bắt buộc).",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    /// <summary>
    /// File phiếu mới nhất còn dùng được để xem trước / in: đã xuất, dữ liệu /
    /// font / thiết lập in chưa đổi sau lần xuất, file còn đó và chưa bị sửa
    /// hay ghi đè bên ngoài. <paramref name="reason"/> = lý do khi không dùng được.
    /// </summary>
    private bool IsExportCurrent(out string reason)
    {
        WarehouseExportState state = CurrentExport.Check();
        reason = CurrentExport.Describe(state);
        return state == WarehouseExportState.Current;
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
    /// đúng file đã xem. Chế độ nhiều phiếu: chuyển phiếu ngay trong cửa sổ
    /// (PHIẾU TRƯỚC / PHIẾU SAU); IN TẤT CẢ bật ngay khi máy in hợp lệ.
    /// Xem trước là bước TÙY CHỌN: không xem vẫn in được bằng nút IN PHIẾU
    /// (xem <see cref="PrintAsync"/>).
    /// </summary>
    private async Task PrintPreviewAsync()
    {
        if (_busy)
            return;

        WarehousePrintPreview? preview = await BuildCurrentPreviewAsync(null);
        if (preview == null)
            return;

        string printer = SelectedPrinter;
        bool printerOk = _printerState == PrinterState.Ready;
        // File PDF tạm của mọi phiếu đã xem trong cửa sổ — xoá khi đóng.
        List<string> pdfs = [preview.PdfPath];
        WarehousePreviewTickets? tickets = null;
        if (_batch is { } batch)
        {
            tickets = new WarehousePreviewTickets
            {
                Names = batch.Select(t => t.Note.Length > 0 ? $"{TicketTag(t)} · {t.Note}" : TicketTag(t)).ToList(),
                StartIndex = batch.IndexOf(_batchTicket!),
                Load = async (owner, index) =>
                {
                    SelectBatchTicket(batch[index]);
                    WarehousePrintPreview? next = await BuildCurrentPreviewAsync(owner);
                    if (next != null)
                        pdfs.Add(next.PdfPath);
                    return next;
                },
                // Không đòi xem đủ mọi phiếu: chỉ cần máy in hợp lệ (PrintBatchAsync vẫn kiểm tra bản xuất hiện hành).
                CanPrint = () => printerOk,
                Hint = () => printerOk ? BatchPreviewHint(printer) : NoPrinterHint
            };
        }

        string hint = printerOk
            ? $"Bấm IN để in {preview.RenderedPageCount} trang ra máy in: {printer}"
            : NoPrinterHint;
        try
        {
            // Nhiều phiếu: nút in của cửa sổ là IN TẤT CẢ (PrintAsync chuyển sang PrintBatchAsync, vẫn kiểm tra đủ điều kiện).
            using FormWarehousePrintPreview form = new(preview, printerOk ? owner => PrintAsync(owner, preview) : null, hint, tickets);
            form.ShowDialog(FindForm());
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Mở cửa sổ xem trước {preview.PdfPath}", ex);
            MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            // Người dùng chưa thấy trang nào — không tính là đã xem trước.
            CurrentExport.ForgetPreview();
        }
        finally
        {
            foreach (string pdf in pdfs)
                TryDeleteFile(pdf);
            RefreshBatchItems();
            UpdateState();
        }
    }

    /// <summary>Dòng hướng dẫn của cửa sổ xem trước khi máy in phiếu chưa hợp lệ (nút IN bị tắt).</summary>
    private const string NoPrinterHint =
        "Chưa chọn máy in hợp lệ nên chưa in được — đang phân trang theo máy in mặc định của Excel. Chọn máy in ở màn hình phiếu rồi xem trước lại.";

    /// <summary>Dòng hướng dẫn của cửa sổ xem trước ở chế độ nhiều phiếu: đã xem bao nhiêu phiếu, IN TẤT CẢ in những gì.</summary>
    private string BatchPreviewHint(string printer)
    {
        int total = _batch!.Count;
        int previewed = total - UnpreviewedTickets().Count;
        return previewed == total
            ? $"Đã xem trước đủ {total} phiếu — bấm IN TẤT CẢ để in ra máy in: {printer}"
            : $"Đã xem {previewed}/{total} phiếu — bấm PHIẾU SAU (Ctrl + PageDown) để xem tiếp, hoặc IN TẤT CẢ để in luôn {total} phiếu ra: {printer}";
    }

    /// <summary>
    /// Dựng bản xem trước của phiếu đang hiển thị (và ghi nhận phiếu đó đã được
    /// xem trước). null = không dựng được — đã báo lý do cho người dùng
    /// (trên <paramref name="owner"/> nếu có, vd cửa sổ xem trước đang mở).
    /// </summary>
    private async Task<WarehousePrintPreview?> BuildCurrentPreviewAsync(IWin32Window? owner)
    {
        // Ô đang sửa dở trong bảng được ghi nhận trước — nếu nó đổi dữ liệu thì bản đã xuất thành bản cũ.
        dgvRows.EndEdit();
        if (!IsExportCurrent(out string reason))
        {
            MessageBox.Show(owner, BatchMode ? $"Phiếu {TicketTag(_batchTicket!)}: {reason}" : reason, Title,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateState();
            return null;
        }

        if (!WarehousePrinterService.IsExcelAvailable())
        {
            MessageBox.Show(owner, "Xem trước bản in cần Microsoft Excel (dùng chính Excel để phân trang giống khi in), " +
                                   "nhưng máy này chưa cài Excel.\n\nFile phiếu đã xuất vẫn mở được bằng phần mềm bảng tính khác để xem và in.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        WarehouseExportResult export = CurrentExport.Last!;
        string printer = SelectedPrinter;
        await CheckPrinterAsync();
        bool printerOk = _printerState == PrinterState.Ready;
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
            CurrentExport.MarkPreviewed(preview.SourceStamp);
            WarehouseLog.Info($"Xem trước {export.OutputPath}: {preview.RenderedPageCount} trang PDF / Excel báo {preview.PageCount} " +
                              $"(mẫu {preview.TemplatePageCount?.ToString() ?? "?"}), máy in phân trang {preview.PaginationPrinter}, " +
                              $"{preview.WarningCount} cảnh báo bố cục");
        }
        catch (WarehouseException ex)
        {
            WarehouseLog.Error($"Xem trước bản in {export.OutputPath}", ex);
            MessageBox.Show(owner, ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            ReportUnexpected(owner, "dựng bản xem trước bản in", ex, show: true);
        }
        finally
        {
            SetBusy(false, preview != null ? DescribePreview(preview) : "Chưa dựng được bản xem trước.");
            RefreshBatchItems();
        }

        return preview;
    }

    /// <summary>Dòng trạng thái sau khi dựng xem trước — số trang là số trang THỰC TẾ của bản xem trước.</summary>
    private static string DescribePreview(WarehousePrintPreview preview) =>
        $"Bản xem trước: {preview.RenderedPageCount} trang" +
        (preview.RenderedPageCount != preview.PageCount ? $" (Excel báo {preview.PageCount} trang — xem cảnh báo)" : "") +
        $", {preview.WarningCount} cảnh báo bố cục.";

    /// <summary>
    /// In file phiếu mới nhất ra máy in đã chọn sau khi người dùng xác nhận.
    /// <paramref name="preview"/> != null: in từ cửa sổ xem trước — file phải
    /// đúng là bản vừa xem. <paramref name="preview"/> == null: in thẳng từ
    /// màn hình, KHÔNG cần xem trước (chưa xem thì chỉ nhắc trong hộp xác nhận).
    /// Trả về true nếu đã gửi lệnh in.
    /// </summary>
    private async Task<bool> PrintAsync(IWin32Window? owner, WarehousePrintPreview? preview)
    {
        if (_busy)
            return false;

        dgvRows.EndEdit();
        if (BatchMode)
            return await PrintBatchAsync(owner);

        if (!IsExportCurrent(out string reason))
        {
            MessageBox.Show(owner, reason, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateState();
            return false;
        }

        if (preview != null && !preview.IsStillCurrent())
        {
            MessageBox.Show(owner, "File phiếu đã thay đổi sau khi dựng bản xem trước này — hãy đóng và xem trước lại.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Kiểm tra trực tiếp (không dùng kết quả lưu sẵn): máy in có thể vừa bị gỡ / tắt.
        string printer = SelectedPrinter;
        await CheckPrinterAsync();
        if (_printerState != PrinterState.Ready)
        {
            UpdateState();
            MessageBox.Show(owner, printer.Length == 0
                    ? "Chưa chọn máy in cho phiếu xuất kho — bấm Đổi máy in để chọn trong Cấu hình."
                    : DescribePrinterProblem(printer),
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        WarehouseExportResult export = CurrentExport.Last!;
        string file = export.OutputPath;
        string pages = preview != null ? $" ({preview.RenderedPageCount} trang)" : "";
        // Chưa xem trước: chỉ nhắc (không chặn) — người dùng tự quyết in luôn hay xem trước.
        string notPreviewed = preview == null && !CurrentExport.WasPreviewed
            ? "\n\n(Chưa xem trước bản in — có thể bấm Không rồi XEM TRƯỚC BẢN IN nếu muốn kiểm tra từng trang trước.)"
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
        catch (Exception ex)
        {
            ReportUnexpected(owner, $"in phiếu ra máy in \"{printer}\"", ex, show: true);
        }
        finally
        {
            // "Đã gửi" = Excel đã giao lệnh in cho hàng đợi của máy in; giấy ra hay chưa do máy in quyết định.
            SetBusy(false, ok ? $"Đã gửi lệnh in {Path.GetFileName(file)} tới hàng đợi của {printer}." : "In chưa thành công.");
        }

        return ok;
    }

    /// <summary>
    /// Chế độ nhiều phiếu: in TẤT CẢ phiếu, chỉ khi mọi phiếu đã xuất (bản hiện
    /// hành). Xem trước bản in KHÔNG bắt buộc — phiếu nào chưa xem chỉ được liệt
    /// kê trong hộp xác nhận. Hỏi xác nhận một lần, in lần lượt; một phiếu in
    /// lỗi thì dừng (thường do máy in) và báo phiếu nào đã gửi.
    /// </summary>
    private async Task<bool> PrintBatchAsync(IWin32Window? owner)
    {
        List<WarehouseBatchTicket> batch = _batch!;
        RefreshBatchItems();

        // Điều kiện BẮT BUỘC duy nhất về dữ liệu: mọi phiếu là bản xuất hiện hành (in đúng dữ liệu đang thấy).
        List<string> notReady = new();
        foreach (WarehouseBatchTicket ticket in batch)
        {
            WarehouseExportState state = ticket.Export.Check();
            if (state != WarehouseExportState.Current)
                notReady.Add($"  • {TicketTag(ticket)}: {ticket.Export.Describe(state).Split('\n')[0]}");
        }

        if (notReady.Count > 0)
        {
            MessageBox.Show(owner, $"Chưa in được — {notReady.Count} phiếu chưa có bản xuất hiện hành:\n{string.Join("\n", notReady)}\n\n" +
                                   "Hãy bấm XUẤT PHIẾU EXCEL (xuất lại tất cả phiếu).",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateState();
            return false;
        }

        string printer = SelectedPrinter;
        await CheckPrinterAsync();
        if (_printerState != PrinterState.Ready)
        {
            UpdateState();
            MessageBox.Show(owner, printer.Length == 0
                    ? "Chưa chọn máy in cho phiếu xuất kho — bấm Đổi máy in để chọn trong Cấu hình."
                    : DescribePrinterProblem(printer),
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Phiếu chưa xem trước: chỉ nhắc trong hộp xác nhận, người dùng tự quyết in luôn hay xem trước.
        List<WarehouseBatchTicket> unpreviewed = UnpreviewedTickets();
        string notPreviewed = unpreviewed.Count > 0
            ? $"\n\n(Có {unpreviewed.Count}/{batch.Count} phiếu chưa xem trước bản in: {string.Join(", ", unpreviewed.Select(TicketTag))} — " +
              "có thể bấm Không rồi XEM TRƯỚC BẢN IN nếu muốn kiểm tra trước.)"
            : "";
        if (MessageBox.Show(owner, $"In {batch.Count} phiếu ra máy in:\n{printer}?\n\n" +
                                   string.Join("\n", batch.Select(t => "  • " + Path.GetFileName(t.Export.Last!.OutputPath))) + notPreviewed,
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return false;

        List<string> sent = new();
        string? failure = null;
        try
        {
            SetBusy(true, $"Đang gửi lệnh in {batch.Count} phiếu tới {printer}...");
            foreach (WarehouseBatchTicket ticket in batch)
            {
                WarehouseExportResult export = ticket.Export.Last!;
                string name = Path.GetFileName(export.OutputPath);
                // Kiểm tra lại ngay trước khi in từng file: file có thể vừa bị sửa bên ngoài.
                WarehouseExportState state = ticket.Export.Check();
                if (state != WarehouseExportState.Current)
                {
                    failure = $"{name}: {ticket.Export.Describe(state)}";
                    break;
                }

                lblStatus.Text = $"Đang gửi lệnh in phiếu {sent.Count + 1}/{batch.Count}: {name}...";
                try
                {
                    await _printerService.PrintWorkbookAsync(export.OutputPath, printer, sheetName: export.SheetName);
                    sent.Add(name);
                    WarehouseLog.Info($"In phiếu (lô {sent.Count}/{batch.Count}) {export.OutputPath} (sheet {export.SheetName}) → {printer}");
                }
                catch (WarehouseException ex)
                {
                    WarehouseLog.Error($"In phiếu (lô) {export.OutputPath} → {printer}", ex);
                    failure = $"{name}: {ex.Message}";
                    break;
                }
                catch (Exception ex)
                {
                    WarehouseLog.Error($"Lỗi ngoài dự kiến khi in phiếu (lô) {export.OutputPath} → {printer}", ex);
                    failure = $"{name}: lỗi ngoài dự kiến — {ex.Message}";
                    break;
                }
            }
        }
        finally
        {
            SetBusy(false, failure == null
                ? $"Đã gửi lệnh in {sent.Count} phiếu tới hàng đợi của {printer}."
                : $"Đã gửi {sent.Count}/{batch.Count} phiếu — dừng ở phiếu lỗi.");
        }

        if (failure != null)
        {
            List<string> unsent = batch.Select(t => Path.GetFileName(t.Export.Last!.OutputPath)).Except(sent).ToList();
            MessageBox.Show(owner, $"In bị dừng ở: {failure}\n\n" +
                                   (sent.Count > 0 ? $"Đã gửi lệnh in:\n{string.Join("\n", sent.Select(s => "  ✓ " + s))}\n\n" : "") +
                                   $"Chưa in:\n{string.Join("\n", unsent.Select(s => "  • " + s))}",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return failure == null;
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

    /// <summary>
    /// Lỗi ngoài dự kiến của một thao tác (không phải WarehouseException đã có
    /// thông báo sẵn): ghi log đầy đủ, báo người dùng thao tác nào hỏng và cách
    /// xử lý — không hiện stack trace, không để thao tác chạy nền âm thầm thất bại.
    /// </summary>
    private void ReportUnexpected(IWin32Window? owner, string action, Exception ex, bool show)
    {
        WarehouseLog.Error($"Lỗi ngoài dự kiến khi {action}", ex);
        if (show)
            MessageBox.Show(owner, $"Không {action} được do lỗi ngoài dự kiến:\n{ex.Message}\n\n" +
                                   "Hãy kiểm tra file đang không bị chương trình khác mở/khoá rồi thử lại. " +
                                   "Chi tiết kỹ thuật đã được ghi vào thư mục logs.",
                Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else
            lblStatus.Text = $"Không {action} được: {ex.Message}";
    }

    private void ClearData()
    {
        if ((_document != null || txtTicketCode.Text.Length > 0) &&
            MessageBox.Show("Xóa mã phiếu, file hàng hóa và bảng xem trước?\n\n(File mẫu, ngày lập phiếu, thư mục output, máy in và font vẫn được giữ.)",
                Title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        dgvRows.CancelEdit();
        txtTicketCode.Text = "";
        txtGoods.Text = "";
        SetDocuments(null, null);
        RefreshDefaultDocumentDate();
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
            string? file = CurrentExport.Last is { } last && File.Exists(last.OutputPath) ? last.OutputPath : null;
            Process.Start(new ProcessStartInfo("explorer.exe", file != null ? $"/select,\"{file}\"" : $"\"{folder}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            WarehouseLog.Error($"Mở thư mục {folder}", ex);
            MessageBox.Show($"Không mở được thư mục:\n{folder}", Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
        List<WarehouseBatchTicket> batch = _batch ?? [];
        bool batchCodesValid = batch.All(t => t.TicketCode(txtPrefix.Text).IsValid);
        if (BatchMode)
        {
            lblFullCode.Text = $"{batch.Count} phiếu (lấy từ Mã chuyển hàng): {string.Join(", ", batch.Select(TicketTag))}";
            lblFullCode.ForeColor = batchCodesValid ? AppTheme.Colors.TextPrimary : AppTheme.Colors.Danger;
        }
        else
        {
            lblFullCode.Text = code.IsValid ? $"Mã phiếu hoàn chỉnh: {code.Code}" : (txtTicketCode.Text.Length == 0 ? "Mã phiếu hoàn chỉnh: —" : "⚠ " + code.Error);
            lblFullCode.ForeColor = code.IsValid || txtTicketCode.Text.Length == 0 ? AppTheme.Colors.TextPrimary : AppTheme.Colors.Danger;
        }

        lblTemplateInfo.Text = _layout == null
            ? (txtTemplate.Text.Length == 0 ? "File gốc chỉ được đọc, không bao giờ bị sửa." : "File mẫu chưa hợp lệ — chọn lại file.")
            : $"Sheet \"{_layout.SheetName}\" · ô số phiếu {new CellReference(_layout.TicketCodeRow, _layout.TicketCodeColumn).FormatAsString()} " +
              $"({_layout.TicketCodeLabel}{_layout.TemplateTicketCode}) · ô ngày " +
              $"{new CellReference(_layout.DocumentDateRow, _layout.DocumentDateColumn).FormatAsString()} · bảng hàng từ dòng {_layout.DataFirstRow + 1}, " +
              $"{_layout.DataRowCount} dòng mẫu · {_layout.NameFontName} {_layout.NameFontSize:0.#}pt";
        lblTemplateInfo.ForeColor = _layout == null && txtTemplate.Text.Length > 0 ? AppTheme.Colors.Danger : AppTheme.Colors.TextMuted;

        lblGoodsInfo.Text = _document == null
            ? (txtGoods.Text.Length == 0 ? "Cần các cột: Mã hàng, Tên hàng, Số lượng chuyển (hoặc Số lượng)." : "Chưa đọc được file hàng hóa.")
            : $"Sheet \"{_document.Goods.SheetName}\" · tiêu đề dòng {_document.Goods.HeaderRowNumber} · " +
              $"số lượng lấy từ cột \"{_document.Goods.QuantityColumnHeader}\" · " +
              (BatchMode
                  ? $"{batch.Count} phiếu chuyển, {batch.Sum(t => t.Document.Rows.Count)} dòng hàng (tách theo cột Mã chuyển hàng)"
                  : $"{_document.Rows.Count} dòng hàng");

        string folder = txtOutput.Text.Trim();
        string extension = _layout?.FileExtension ?? ".xlsx";
        int existingFiles = folder.Length == 0 ? 0 : batch.Count(t => t.TicketCode(txtPrefix.Text) is { IsValid: true } c &&
                                                                      File.Exists(Path.Combine(folder, c.Code + extension)));
        lblOutputInfo.Text = folder.Length == 0
            ? "Thư mục chưa có sẽ được tạo khi xuất."
            : BatchMode
                ? $"Sẽ tạo {batch.Count} file riêng: " + string.Join(", ", batch.Select(t => TicketTag(t) + extension)) +
                  (existingFiles > 0 ? $"  ({existingFiles} file đã tồn tại — sẽ hỏi trước khi ghi đè)" : "") +
                  (Directory.Exists(folder) ? "" : "  · thư mục chưa có, sẽ được tạo")
            : code.IsValid
                ? $"Tên file dự kiến: {code.Code}{extension}" +
                  (File.Exists(Path.Combine(folder, code.Code + extension)) ? "  (đã tồn tại — sẽ hỏi trước khi ghi đè)" : "") +
                  (Directory.Exists(folder) ? "" : "  · thư mục chưa có, sẽ được tạo")
                : "Tên file dự kiến: <mã phiếu>.xlsx";

        string printer = SelectedPrinter;
        txtPrinterName.Text = printer;
        (lblPrinterStatus.Text, lblPrinterStatus.ForeColor) =
            printer.Length == 0
                ? ("Chưa chọn máy in phiếu — bấm Đổi máy in để chọn trong Cấu hình. (Không tự dùng máy in mặc định của Windows.)", AppTheme.Colors.Danger)
            : _printerState == PrinterState.Checking
                ? ("Đang kiểm tra kết nối máy in...", AppTheme.Colors.TextMuted)
            : _printerState == PrinterState.Ready
                ? ("✓ Máy in đã kết nối. In bằng Microsoft Excel, đúng khổ giấy/lề của file mẫu.", AppTheme.Colors.Success)
            : PrinterFailed
                ? ("✗ " + DescribePrinterProblem(printer), AppTheme.Colors.Danger)
                : ("In bằng Microsoft Excel, đúng khổ giấy/lề của file mẫu. Bấm Kiểm tra kết nối để thử máy in.", AppTheme.Colors.TextMuted);

        int errors = _document?.ErrorCount ?? 0;
        int warnings = _document?.WarningCount ?? 0;
        int total = _document?.Rows.Count ?? 0;
        int batchErrorTickets = batch.Count(t => t.Document.ErrorCount > 0);
        lblSummary.Text = _document == null
            ? "Chưa có dữ liệu — chọn file phiếu mẫu và file hàng hóa."
            : (BatchMode ? $"Phiếu {TicketTag(_batchTicket!)}: " : "") +
              $"{total} dòng hàng · {total - errors - warnings} hợp lệ · {warnings} cảnh báo · {errors} lỗi" +
              (errors > 0 ? " — sửa các dòng lỗi (đỏ) trước khi xuất." : "") +
              (BatchMode && batchErrorTickets > 0 && errors == 0 ? $" — còn {batchErrorTickets} phiếu khác có lỗi." : "");
        lblSummary.ForeColor = errors > 0 ? AppTheme.Colors.Danger : warnings > 0 ? WarningText : AppTheme.Colors.TextSecondary;

        WarehouseExportState exportState = CurrentExport.Check();
        bool exported = exportState == WarehouseExportState.Current;
        int batchExported = batch.Count(t => t.Export.Check() == WarehouseExportState.Current);
        int batchPreviewed = batch.Count(t => t.Export.Check() == WarehouseExportState.Current && t.Export.WasPreviewed);
        bool batchAllExported = BatchMode && batchExported == batch.Count;
        (lblDataState.Text, lblDataState.ForeColor) =
            _busy ? ("Đang xử lý...", AppTheme.Colors.TextSecondary)
            : BatchMode ? (batchExported == 0
                    ? (batchErrorTickets > 0 ? $"Trạng thái: {batchErrorTickets} phiếu còn dòng lỗi." : "Trạng thái: sẵn sàng xuất tất cả phiếu.",
                        batchErrorTickets > 0 ? AppTheme.Colors.Danger : AppTheme.Colors.TextSecondary)
                    : batchAllExported
                        ? ($"Trạng thái: đã xuất {batch.Count} phiếu (đã xem trước {batchPreviewed}/{batch.Count}) — có thể in tất cả.", AppTheme.Colors.Success)
                        : ($"Trạng thái: mới có {batchExported}/{batch.Count} phiếu là bản xuất hiện hành — cần xuất lại.", WarningText))
            : _document == null ? ("Trạng thái: chưa có dữ liệu.", AppTheme.Colors.TextSecondary)
            : exported ? ($"Trạng thái: đã xuất {Path.GetFileName(CurrentExport.Last!.OutputPath)}" +
                          (CurrentExport.WasPreviewed ? " — đã xem trước, có thể in." : " — có thể in (xem trước không bắt buộc)."), AppTheme.Colors.Success)
            : exportState == WarehouseExportState.Stale ? ("Trạng thái: bản đã xuất là bản CŨ (dữ liệu/thiết lập đã đổi) — cần xuất lại.", WarningText)
            : exportState == WarehouseExportState.Missing ? ("Trạng thái: file đã xuất không còn trên đĩa — cần xuất lại.", WarningText)
            : exportState == WarehouseExportState.ModifiedExternally ? ("Trạng thái: file đã xuất bị sửa bên ngoài — cần xuất lại.", WarningText)
            : errors > 0 ? ("Trạng thái: còn dòng lỗi.", AppTheme.Colors.Danger)
            : ("Trạng thái: sẵn sàng xuất.", AppTheme.Colors.TextSecondary);

        bool idle = !_busy;
        btnChooseTemplate.Enabled = idle;
        btnChooseGoods.Enabled = idle;
        btnChooseOutput.Enabled = idle;
        btnChangePrinter.Enabled = idle;
        btnCheckPrinter.Enabled = idle && printer.Length > 0 && _printerState != PrinterState.Checking;
        cboScope.Enabled = idle;
        txtPrefix.Enabled = idle;
        txtTicketCode.Enabled = idle && !BatchMode; // nhiều phiếu: mã lấy từ cột Mã chuyển hàng
        cboBatchTicket.Enabled = idle;
        // Mã (theo tiền tố), số dòng lỗi, đã xuất / đã xem trước của từng phiếu trong ô chọn phiếu.
        RefreshBatchItems();
        dtpDocumentDate.Enabled = idle;
        dgvRows.ReadOnly = !idle;
        btnValidate.Enabled = idle;
        btnPreview.Enabled = idle && txtTemplate.Text.Length > 0 && txtGoods.Text.Length > 0;
        btnExport.Enabled = BatchMode
            ? idle && folder.Length > 0 && batchCodesValid && batchErrorTickets == 0
            : idle && _document != null && code.IsValid && folder.Length > 0 && errors == 0;
        // XEM TRƯỚC BẢN IN và IN PHIẾU độc lập nhau: cả hai chỉ cần bản xuất hiện hành,
        // in KHÔNG phụ thuộc đã xem trước hay chưa. Nhiều phiếu: in bật khi mọi phiếu đã xuất.
        btnPrintPreview.Enabled = idle && exported;
        btnPrint.Enabled = idle && (BatchMode ? batchAllExported : exported) && printer.Length > 0 && !PrinterFailed;
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
    private static TableLayoutPanel NewTable(int columns)
    {
        TableLayoutPanel table = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            ColumnCount = columns
        };
        // Mặc định: cột đầu lấp phần còn lại, các cột sau vừa nội dung — nơi dựng đổi theo index khi cần.
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 1; i < columns; i++)
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        return table;
    }

    private static FlowLayoutPanel NewFlow() => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = false,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private void StyleOverline(Label label, string text)
    {
        label.Text = text;
        label.Font = AppTheme.Fonts.Overline;
        label.ForeColor = AppTheme.Colors.TextMuted;
        label.AutoSize = true;
        label.Margin = new Padding(0, 0, 0, S(8));
    }

    private void StyleCaption(Label label, string text)
    {
        label.Text = text;
        label.Font = AppTheme.Fonts.Hint;
        label.ForeColor = AppTheme.Colors.TextSecondary;
        label.AutoSize = true;
        label.Margin = new Padding(0, 0, 0, S(4));
    }

    /// <summary>Một dòng, cao đúng theo font (theo DPI), dài quá thì "..." và di chuột vào xem đủ.</summary>
    private void StyleOneLine(Label label, Font font, Color color)
    {
        label.Font = font;
        label.ForeColor = color;
        label.AutoSize = false;
        label.AutoEllipsis = true;
        // Chỉ giãn NGANG theo ô. Dock = Fill sẽ kéo cả chiều cao theo hàng của
        // TableLayoutPanel, rồi chính chiều cao đó lại được báo ngược lên làm
        // hàng không bao giờ co lại được (vùng trống lớn, nội dung dài mãi ra).
        label.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Height = font.Height + S(4);
        label.Margin = Padding.Empty;
    }

    /// <summary>
    /// Nhiều dòng: xuống dòng theo bề rộng đặt ở <see cref="SetWrapWidth"/>, cao
    /// đúng theo chữ (đo lại mỗi khi đổi chữ). Không dùng Label.AutoSize: nhãn
    /// AutoSize nằm vắt qua các cột % bị TableLayoutPanel đo ở bề rộng gần 0 —
    /// mỗi ký tự một dòng — và khối chứa nó cao lên hàng nghìn px.
    /// </summary>
    private void StyleWrapping(Label label)
    {
        label.Font = AppTheme.Fonts.Hint;
        label.ForeColor = AppTheme.Colors.TextMuted;
        label.AutoSize = false;
        // Bám đỉnh: khi khối bị kéo cao theo khối bên cạnh, chữ không trôi xuống giữa khoảng trống.
        label.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        label.Margin = new Padding(0, S(6), 0, 0);
        label.TextChanged += (_, _) => FitWrappedHeight(label);
    }

    private void SetWrapWidth(Label label, int width)
    {
        _wrapWidths[label] = Math.Max(1, width - label.Margin.Horizontal);
        FitWrappedHeight(label);
    }

    private void FitWrappedHeight(Label label)
    {
        if (!_wrapWidths.TryGetValue(label, out int width))
            return;
        int height = TextRenderer.MeasureText(label.Text.Length == 0 ? " " : label.Text, label.Font,
            new Size(width, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
        if (label.Height != height)
            label.Height = height;
    }

    private void StyleTextBox(RoundedTextBox box, string placeholder)
    {
        box.Height = S(ControlHeight);
        box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        box.Margin = Padding.Empty;
        box.ContainerColor = AppTheme.Colors.SurfaceElevated;
        box.PlaceholderText = placeholder;
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
    }

    private static void StyleFileButton(RoundedButton button, string text, IconGlyphs.Kind icon, Action onClick)
    {
        button.Text = text;
        button.Icon = icon;
        button.Variant = ButtonVariant.Outline;
        button.ContainerColor = AppTheme.Colors.SurfaceElevated;
        button.Click += (_, _) => onClick();
    }

    private static void StyleAction(RoundedButton button, string text, IconGlyphs.Kind icon, ButtonVariant variant, Action onClick)
    {
        button.Text = text;
        button.Icon = icon;
        button.Variant = variant;
        button.ContainerColor = AppTheme.Colors.SurfaceElevated;
        button.Click += (_, _) => onClick();
    }
    #endregion
}
