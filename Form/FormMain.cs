
using System.Globalization;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;
using KiotVietLabelPrinter.Views;

namespace KiotVietLabelPrinter.Forms;

public class FormMain : Form
{
    private readonly LabelService _labelService = new();
    private readonly LabelCatalogService _catalogService = new();
    private readonly ImageToolCatalogService _toolCatalogService = new();

    // Header
    private readonly Panel pnlHeader = new();
    private readonly RoundedButton btnBack = new();
    private readonly RoundedPanel pnlLogoBadge = new();
    private readonly IconGlyph iconLogo = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSubtitle = new();
    private readonly Label lblVersion = new();

    // Home / Category
    private readonly Panel pnlCategory = new();
    private readonly SmoothFlowLayoutPanel flpCategories = new();
    private readonly Label lblToolsTitle = new();
    private readonly Label lblToolsHint = new();
    private readonly SmoothFlowLayoutPanel flpTools = new();

    // Công cụ hình ảnh (nhúng như một "workspace" khác — cùng cơ chế
    // show/hide với pnlWorkspace, xem ShowHome/OpenBackgroundRemover).
    private readonly BackgroundRemoverView bgRemoverView = new();
    private readonly ImageCompressorView imageCompressorView = new();

    // Workspace
    private readonly RoundedPanel pnlWorkspace = new();
    private readonly RoundedPanel pnlWorkspaceIcon = new();
    private readonly IconGlyph iconWorkspace = new();
    private readonly Label lblCurrentCategory = new();
    private readonly Label lblCurrentCategoryCode = new();

    private readonly RoundedTextBox txtExcelFile = new();
    private readonly RoundedTextBox txtEmployeeCode = new();

    // Nhập giá bán - chỉ hiện với Tem đầy đủ (HandlerType == "FULL").
    // Xem ApplyPriceEditMode/UpdatePriceModeUi.
    private readonly Label lblPriceMode = new();
    private readonly ComboBox cboPriceMode = new();
    private readonly RoundedTextBox txtUniformPrice = new();
    private readonly RoundedButton btnEditPrices = new();
    private readonly Label lblPriceStatus = new();

    private PriceOverrideMode _priceMode = PriceOverrideMode.Keep;
    private readonly Dictionary<string, double> _productPriceOverrides = new(StringComparer.OrdinalIgnoreCase);
    private bool _formattingUniformPrice;

    // Sửa tên hàng - chỉ hiện với Tem đầy đủ, ngay dưới hàng giá bán. Hoàn
    // toàn độc lập với state/luồng dữ liệu giá bán ở trên (xem region "Sửa
    // tên hàng (Tem đầy đủ)").
    private readonly RoundedButton btnEditNames = new();
    private readonly Label lblNameStatus = new();
    private readonly Panel pnlDivider2 = new();
    private readonly Label lblSectionActionsTitle = new();
    private readonly Dictionary<string, string> _productNameOverrides = new(StringComparer.OrdinalIgnoreCase);

    private readonly RoundedButton btnChooseExcel = new();
    private readonly SmoothFlowLayoutPanel flpActions = new();
    private readonly RoundedButton btnConfig = new();
    private readonly RoundedButton btnHistory = new();
    private readonly RoundedButton btnParserLab = new();
    private readonly RoundedButton btnPreview = new();
    private readonly RoundedButton btnCheckParse = new();
    private readonly RoundedButton btnPrint = new();

    private LabelDefinition? _selectedLabel;

    public FormMain()
    {
        Text = "KiotViet Label Printer";
        Width = 1060;
        Height = 815;
        // Tall enough that the detail card (icon/header + up to 3 field rows -
        // the 3rd row "Sửa tên hàng" only shows for Tem đầy đủ - + up to two
        // wrapped rows of action buttons) never gets clipped by the card's own
        // bottom-anchored edge at the smallest allowed window size.
        MinimumSize = new Size(940, 815);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        CheckConfigOnStart();
        ShowHome();

        // pnlBack anchors to the right edge and the header paints its own soft
        // accent shapes behind it; force a clean repaint on resize so neither
        // leaves stale pixels behind as the button relocates.
        Resize += (_, _) => pnlHeader.Invalidate();

        // FlowLayoutPanel doesn't reliably re-evaluate WrapContents when its
        // width only changes via Anchor (no direct resize of its own) — without
        // this, buttons overflow past the card edge instead of wrapping.
        Resize += (_, _) => flpActions.PerformLayout();
        Resize += (_, _) => flpCategories.PerformLayout();
        Resize += (_, _) => flpTools.PerformLayout();
    }

    private void BuildUi()
    {
        BuildHeader();
        BuildCategoryPanel();
        BuildWorkspacePanel();
        BuildBackgroundRemoverPanel();
        BuildImageCompressorPanel();
    }

    #region Header
    private void BuildHeader()
    {
        pnlHeader.Left = 0;
        pnlHeader.Top = 0;
        pnlHeader.Width = ClientSize.Width;
        pnlHeader.Height = 188;
        pnlHeader.BackColor = AppTheme.Colors.Background;
        pnlHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlHeader.Paint += PaintHeaderAccent;
        Controls.Add(pnlHeader);

        Panel headerDivider = new()
        {
            Left = 0,
            Top = pnlHeader.Height - 1,
            Width = pnlHeader.Width,
            Height = 1,
            BackColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlHeader.Controls.Add(headerDivider);

        pnlLogoBadge.Left = 32;
        pnlLogoBadge.Top = 40;
        pnlLogoBadge.Width = 64;
        pnlLogoBadge.Height = 64;
        pnlLogoBadge.CornerRadius = 18;
        pnlLogoBadge.FillColor = AppTheme.Colors.PrimaryLight;
        pnlLogoBadge.BorderThickness = 0;
        pnlLogoBadge.ContainerColor = AppTheme.Colors.Background;
        pnlHeader.Controls.Add(pnlLogoBadge);

        iconLogo.Kind = IconGlyphs.Kind.Tag;
        iconLogo.IconColor = AppTheme.Colors.Primary;
        iconLogo.Dock = DockStyle.Fill;
        iconLogo.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlLogoBadge.Controls.Add(iconLogo);

        lblTitle.Text = "IN TEM";
        lblTitle.Left = 112;
        lblTitle.Top = 40;
        lblTitle.Width = 440;
        lblTitle.Height = 40;
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlHeader.Controls.Add(lblTitle);

        lblSubtitle.Text = "Chọn danh mục tem để bắt đầu";
        lblSubtitle.Left = 114;
        lblSubtitle.Top = 82;
        lblSubtitle.Width = 640;
        lblSubtitle.Font = AppTheme.Fonts.Subtitle;
        lblSubtitle.ForeColor = AppTheme.Colors.TextSecondary;
        pnlHeader.Controls.Add(lblSubtitle);

        // Phiên bản build — để xác nhận máy đang chạy đúng bản .exe mới.
        lblVersion.Text = Services.AppInfo.ShortLabel;
        lblVersion.Left = 114;
        lblVersion.Top = 150;
        lblVersion.Width = 640;
        lblVersion.Height = 18;
        lblVersion.Font = AppTheme.Fonts.Overline;
        lblVersion.ForeColor = AppTheme.Colors.TextMuted;
        pnlHeader.Controls.Add(lblVersion);

        btnBack.Text = "Quay lại";
        btnBack.Icon = IconGlyphs.Kind.ArrowLeft;
        btnBack.Width = 128;
        btnBack.Height = 40;
        btnBack.Top = 40;
        btnBack.Left = pnlHeader.Width - btnBack.Width - 32;
        btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBack.Variant = ButtonVariant.Secondary;
        btnBack.ContainerColor = AppTheme.Colors.Background;
        btnBack.Font = AppTheme.Fonts.ButtonRegular;
        btnBack.Visible = false;
        btnBack.Click += (_, _) => ShowHome();
        pnlHeader.Controls.Add(btnBack);
    }

    // Very soft, low-contrast blue shapes on the right of the header — just
    // enough to avoid a flat/empty look, never strong enough to distract.
    private void PaintHeaderAccent(object? sender, PaintEventArgs e)
    {
        AppTheme.PrepareSmoothing(e.Graphics);

        using SolidBrush outer = new(Color.FromArgb(14, AppTheme.Colors.Primary));
        using SolidBrush inner = new(Color.FromArgb(10, AppTheme.Colors.Primary));

        e.Graphics.FillEllipse(outer, pnlHeader.Width - 300, -140, 360, 360);
        e.Graphics.FillEllipse(inner, pnlHeader.Width - 160, 30, 220, 220);
    }
    #endregion

    #region Category panel
    private void BuildCategoryPanel()
    {
        pnlCategory.Left = 32;
        pnlCategory.Top = 204;
        pnlCategory.Width = ClientSize.Width - 64;
        pnlCategory.Height = ClientSize.Height - 236;
        pnlCategory.BackColor = AppTheme.Colors.Background;
        pnlCategory.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        // Cần cuộn dọc: bên dưới DANH MỤC TEM giờ có thêm section CÔNG CỤ HÌNH
        // ẢNH, tổng chiều cao có thể vượt vùng hiển thị trên màn hình nhỏ.
        pnlCategory.AutoScroll = true;
        Controls.Add(pnlCategory);

        Label lblCategoryTitle = new()
        {
            Text = "DANH MỤC TEM",
            Left = 4,
            Top = 4,
            Width = 400,
            Font = AppTheme.Fonts.SectionTitle,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlCategory.Controls.Add(lblCategoryTitle);

        Label lblHint = new()
        {
            Text = "Chọn loại tem cần in. Danh sách này được lấy từ cấu hình.",
            Left = 4,
            Top = 36,
            Width = 700,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextSecondary
        };
        pnlCategory.Controls.Add(lblHint);

        flpCategories.Left = 0;
        flpCategories.Top = 72;
        // -24 chừa chỗ cho scrollbar dọc của pnlCategory (AutoScroll) khi nội
        // dung hai section cộng lại cao hơn vùng hiển thị.
        flpCategories.Width = pnlCategory.Width - 24;
        flpCategories.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        flpCategories.BackColor = AppTheme.Colors.Background;
        // AutoSize (không Dock/neo Bottom) thay vì tự cuộn: panel co theo đúng
        // số hàng card đang có, để section CÔNG CỤ HÌNH ẢNH có thể nằm ngay bên
        // dưới thay vì bị đẩy xuống cuối vùng hiển thị cố định.
        flpCategories.AutoScroll = false;
        flpCategories.AutoSize = true;
        flpCategories.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flpCategories.WrapContents = true;
        flpCategories.FlowDirection = FlowDirection.LeftToRight;
        pnlCategory.Controls.Add(flpCategories);

        lblToolsTitle.Text = "CÔNG CỤ HÌNH ẢNH";
        lblToolsTitle.Left = 4;
        lblToolsTitle.Width = 400;
        lblToolsTitle.Font = AppTheme.Fonts.SectionTitle;
        lblToolsTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlCategory.Controls.Add(lblToolsTitle);

        lblToolsHint.Text = "Xử lý ảnh sản phẩm trực tiếp trên máy.";
        lblToolsHint.Left = 4;
        lblToolsHint.Width = 700;
        lblToolsHint.Font = AppTheme.Fonts.Body;
        lblToolsHint.ForeColor = AppTheme.Colors.TextSecondary;
        pnlCategory.Controls.Add(lblToolsHint);

        flpTools.Left = 0;
        flpTools.Width = flpCategories.Width;
        flpTools.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        flpTools.BackColor = AppTheme.Colors.Background;
        flpTools.AutoScroll = false;
        flpTools.AutoSize = true;
        flpTools.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        flpTools.WrapContents = true;
        flpTools.FlowDirection = FlowDirection.LeftToRight;
        pnlCategory.Controls.Add(flpTools);

        // Chiều cao flpCategories chỉ biết được sau khi có card (và đổi lại mỗi
        // khi cửa sổ resize làm card xuống dòng khác) — dời section thứ hai mỗi
        // khi đó thay vì đặt cứng một toạ độ Top.
        flpCategories.SizeChanged += (_, _) => RepositionToolsSection();
        RepositionToolsSection();
    }

    private void RepositionToolsSection()
    {
        lblToolsTitle.Top = flpCategories.Bottom + 40;
        lblToolsHint.Top = lblToolsTitle.Bottom + 8;
        flpTools.Top = lblToolsHint.Bottom + 16;
    }

    // Controls.Clear() chỉ gỡ quan hệ cha-con, KHÔNG Dispose() control cũ — để
    // lại HWND + Timer nội bộ của RoundedPanel/RoundedButton (xem Dispose
    // override của chúng) sống tới khi GC finalize. ReloadCategories() được
    // gọi lại mỗi lần "Quay lại" Home nên rò rỉ cộng dồn dần theo phiên làm
    // việc. Dispose từng control cũ trước (tự cascade xuống toàn bộ con cháu
    // và tự gỡ khỏi Controls) để giải phóng ngay, không đổi giao diện hiển thị.
    private static void ClearAndDispose(Control.ControlCollection controls)
    {
        while (controls.Count > 0)
            controls[0].Dispose();
    }

    private void ReloadCategories()
    {
        flpCategories.SuspendLayout();
        flpTools.SuspendLayout();

        try
        {
            ClearAndDispose(flpCategories.Controls);

            List<LabelDefinition> labels = _catalogService.GetAllEnabled();

            if (labels.Count == 0)
            {
                Label empty = new()
                {
                    Text = "Chưa có loại tem nào được bật trong cấu hình.",
                    AutoSize = true,
                    ForeColor = AppTheme.Colors.Danger,
                    Font = AppTheme.Fonts.BodyBold,
                    Margin = new Padding(20)
                };

                flpCategories.Controls.Add(empty);
            }
            else
            {
                foreach (LabelDefinition label in labels)
                    flpCategories.Controls.Add(CreateCategoryCard(label));
            }

            ClearAndDispose(flpTools.Controls);

            List<ToolDefinition> tools = _toolCatalogService.GetAllEnabled();
            lblToolsTitle.Visible = tools.Count > 0;
            lblToolsHint.Visible = tools.Count > 0;

            foreach (ToolDefinition tool in tools)
                flpTools.Controls.Add(CreateToolCard(tool));
        }
        finally
        {
            flpCategories.ResumeLayout(true);
            flpTools.ResumeLayout(true);
            RepositionToolsSection();
        }
    }

    // Ba loại tem có sẵn (FULL/GLASSES/BARCODE) dùng line icon đồng bộ; loại tem
    // tuỳ biến thêm qua màn Cấu hình vẫn hiển thị đúng IconText người dùng nhập,
    // không phá vỡ khả năng tuỳ biến hiện có.
    private static IconGlyphs.Kind? ResolveHomeIcon(string handlerType) => handlerType switch
    {
        "FULL" => IconGlyphs.Kind.Tag,
        "GLASSES" => IconGlyphs.Kind.Glasses,
        "BARCODE" => IconGlyphs.Kind.Barcode,
        _ => null
    };

    private static IconGlyphs.Kind? ResolveDetailIcon(string handlerType) => handlerType switch
    {
        "FULL" => IconGlyphs.Kind.Document,
        "GLASSES" => IconGlyphs.Kind.Glasses,
        "BARCODE" => IconGlyphs.Kind.Tag,
        _ => null
    };

    private Control CreateCategoryCard(LabelDefinition label)
    {
        IconGlyphs.Kind? homeIcon = ResolveHomeIcon(label.HandlerType);
        return BuildCard(label.Name, label.Description, homeIcon, label.IconText, (_, _) => OpenLabelWorkspace(label));
    }

    private Control CreateToolCard(ToolDefinition tool)
    {
        IconGlyphs.Kind icon = tool.Code == "IMAGE_COMPRESS" ? IconGlyphs.Kind.Compress : IconGlyphs.Kind.Image;
        return BuildCard(tool.Name, tool.Description, icon, null, (_, _) => OpenTool(tool));
    }

    private void OpenTool(ToolDefinition tool)
    {
        if (tool.Code == "IMAGE_COMPRESS")
            OpenImageCompressor(tool);
        else
            OpenBackgroundRemover(tool);
    }

    // Dùng chung cho mọi card ở màn hình chính (DANH MỤC TEM lẫn CÔNG CỤ HÌNH
    // ẢNH) để đảm bảo cùng kích thước/bo góc/border/shadow/typography/spacing/
    // hover — không tạo một style card thứ hai khác biệt.
    private Control BuildCard(string name, string description, IconGlyphs.Kind? iconKind, string? iconTextFallback, EventHandler onClick)
    {
        RoundedPanel card = new()
        {
            Width = 300,
            Height = 168,
            Margin = new Padding(0, 0, 20, 20),
            CornerRadius = 22,
            FillColor = AppTheme.Colors.SurfaceElevated,
            BorderColor = AppTheme.Colors.Border,
            BorderThickness = 1,
            ShadowEnabled = true,
            HoverEffect = true,
            HoverFillColor = AppTheme.Colors.SurfaceElevated,
            HoverBorderColor = AppTheme.Colors.Primary,
            ContainerColor = AppTheme.Colors.Background,
            Cursor = Cursors.Hand,
            AccessibleRole = AccessibleRole.PushButton,
            AccessibleName = name
        };

        RoundedPanel iconBadge = new()
        {
            Left = 22,
            Top = 22,
            Width = 48,
            Height = 48,
            CornerRadius = 14,
            FillColor = AppTheme.Colors.PrimaryLight,
            BorderThickness = 0,
            ContainerColor = AppTheme.Colors.Surface,
            Cursor = Cursors.Hand
        };

        if (iconKind.HasValue)
        {
            IconGlyph icon = new()
            {
                Dock = DockStyle.Fill,
                Kind = iconKind.Value,
                IconColor = AppTheme.Colors.Primary,
                ContainerColor = AppTheme.Colors.PrimaryLight,
                Cursor = Cursors.Hand
            };
            iconBadge.Controls.Add(icon);
        }
        else
        {
            Label lblIcon = new()
            {
                Text = string.IsNullOrWhiteSpace(iconTextFallback) ? "🏷" : iconTextFallback,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = AppTheme.Fonts.IconSmall,
                ForeColor = AppTheme.Colors.Primary,
                Cursor = Cursors.Hand
            };
            iconBadge.Controls.Add(lblIcon);
        }

        RoundedPanel arrowBadge = new()
        {
            Left = card.Width - 22 - 34,
            Top = 22,
            Width = 34,
            Height = 34,
            CornerRadius = 17,
            FillColor = AppTheme.Colors.Surface,
            BorderColor = AppTheme.Colors.Border,
            BorderThickness = 1,
            ContainerColor = AppTheme.Colors.SurfaceElevated,
            Cursor = Cursors.Hand
        };

        IconGlyph arrowIcon = new()
        {
            Dock = DockStyle.Fill,
            Kind = IconGlyphs.Kind.ArrowRight,
            IconColor = AppTheme.Colors.TextSecondary,
            StrokeWidth = 1.6f,
            ContainerColor = AppTheme.Colors.Surface,
            Cursor = Cursors.Hand
        };
        arrowBadge.Controls.Add(arrowIcon);

        Label lblName = new()
        {
            Text = name,
            Left = 22,
            Top = 84,
            Width = card.Width - 44,
            Height = 26,
            Font = AppTheme.Fonts.BodyBold,
            ForeColor = AppTheme.Colors.TextPrimary,
            Cursor = Cursors.Hand
        };

        Label lblDesc = new()
        {
            Text = description,
            Left = 22,
            Top = 112,
            Width = card.Width - 44,
            Height = 42,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextSecondary,
            Cursor = Cursors.Hand
        };

        card.Controls.Add(iconBadge);
        card.Controls.Add(arrowBadge);
        card.Controls.Add(lblName);
        card.Controls.Add(lblDesc);

        card.Click += onClick;
        lblName.Click += onClick;
        lblDesc.Click += onClick;
        arrowBadge.Click += onClick;
        arrowIcon.Click += onClick;

        return card;
    }
    #endregion

    #region Background remover panel
    // Nhúng cùng vị trí/kích thước với pnlWorkspace — show/hide y hệt cơ chế
    // hiện có (xem ShowHome/OpenBackgroundRemover), không tạo Form/dialog mới.
    private void BuildBackgroundRemoverPanel()
    {
        bgRemoverView.Left = 32;
        bgRemoverView.Top = 204;
        bgRemoverView.Width = ClientSize.Width - 64;
        bgRemoverView.Height = ClientSize.Height - 236;
        bgRemoverView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        bgRemoverView.Visible = false;
        Controls.Add(bgRemoverView);
    }
    #endregion

    #region Image compressor panel
    // Nhúng cùng vị trí/kích thước với pnlWorkspace/bgRemoverView — cùng cơ chế
    // show/hide (xem ShowHome/OpenImageCompressor), không tạo Form/dialog mới.
    private void BuildImageCompressorPanel()
    {
        imageCompressorView.Left = 32;
        imageCompressorView.Top = 204;
        imageCompressorView.Width = ClientSize.Width - 64;
        imageCompressorView.Height = ClientSize.Height - 236;
        imageCompressorView.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        imageCompressorView.Visible = false;
        Controls.Add(imageCompressorView);
    }
    #endregion

    #region Workspace panel
    private void BuildWorkspacePanel()
    {
        pnlWorkspace.Left = 32;
        pnlWorkspace.Top = 204;
        pnlWorkspace.Width = ClientSize.Width - 64;
        pnlWorkspace.Height = ClientSize.Height - 236;
        pnlWorkspace.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        pnlWorkspace.CornerRadius = 22;
        pnlWorkspace.FillColor = AppTheme.Colors.SurfaceElevated;
        pnlWorkspace.BorderColor = AppTheme.Colors.Border;
        pnlWorkspace.BorderThickness = 1;
        pnlWorkspace.ShadowEnabled = true;
        pnlWorkspace.Visible = false;
        Controls.Add(pnlWorkspace);

        pnlWorkspaceIcon.Left = 32;
        pnlWorkspaceIcon.Top = 32;
        pnlWorkspaceIcon.Width = 44;
        pnlWorkspaceIcon.Height = 44;
        pnlWorkspaceIcon.CornerRadius = 13;
        pnlWorkspaceIcon.FillColor = AppTheme.Colors.PrimaryLight;
        pnlWorkspaceIcon.BorderThickness = 0;
        pnlWorkspaceIcon.ContainerColor = AppTheme.Colors.SurfaceElevated;
        pnlWorkspace.Controls.Add(pnlWorkspaceIcon);

        iconWorkspace.Dock = DockStyle.Fill;
        iconWorkspace.Kind = IconGlyphs.Kind.Document;
        iconWorkspace.IconColor = AppTheme.Colors.Primary;
        iconWorkspace.ContainerColor = AppTheme.Colors.PrimaryLight;
        pnlWorkspaceIcon.Controls.Add(iconWorkspace);

        lblCurrentCategory.Text = "Danh mục";
        lblCurrentCategory.Left = 88;
        lblCurrentCategory.Top = 32;
        lblCurrentCategory.Width = 500;
        lblCurrentCategory.Height = 26;
        lblCurrentCategory.Font = AppTheme.Fonts.SectionTitle;
        lblCurrentCategory.ForeColor = AppTheme.Colors.TextPrimary;
        pnlWorkspace.Controls.Add(lblCurrentCategory);

        lblCurrentCategoryCode.Text = "";
        lblCurrentCategoryCode.Left = 88;
        lblCurrentCategoryCode.Top = 58;
        lblCurrentCategoryCode.Width = 500;
        lblCurrentCategoryCode.Height = 18;
        lblCurrentCategoryCode.Font = AppTheme.Fonts.Overline;
        lblCurrentCategoryCode.ForeColor = AppTheme.Colors.TextMuted;
        pnlWorkspace.Controls.Add(lblCurrentCategoryCode);

        Panel line1 = new()
        {
            Left = 32,
            Top = 96,
            Width = pnlWorkspace.Width - 64,
            Height = 1,
            BackColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlWorkspace.Controls.Add(line1);

        Label lblSectionSource = new()
        {
            Text = "NGUỒN DỮ LIỆU",
            Left = 32,
            Top = 116,
            Width = 300,
            Font = AppTheme.Fonts.Overline,
            ForeColor = AppTheme.Colors.TextMuted
        };
        pnlWorkspace.Controls.Add(lblSectionSource);

        Label lblExcel = new()
        {
            Text = "File Excel KiotViet",
            Left = 32,
            Top = 152,
            Width = 150,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlWorkspace.Controls.Add(lblExcel);

        txtExcelFile.Left = 190;
        txtExcelFile.Top = 144;
        txtExcelFile.Width = pnlWorkspace.Width - 190 - 32 - 140 - 12;
        txtExcelFile.Height = 42;
        txtExcelFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        txtExcelFile.Font = AppTheme.Fonts.Body;
        txtExcelFile.ReadOnly = true;
        txtExcelFile.PlaceholderText = "Chọn tệp dữ liệu Excel";
        txtExcelFile.ContainerColor = AppTheme.Colors.Surface;
        pnlWorkspace.Controls.Add(txtExcelFile);

        btnChooseExcel.Text = "Chọn file";
        btnChooseExcel.Icon = IconGlyphs.Kind.Folder;
        btnChooseExcel.Left = pnlWorkspace.Width - 32 - 140;
        btnChooseExcel.Top = 144;
        btnChooseExcel.Width = 140;
        btnChooseExcel.Height = 42;
        btnChooseExcel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnChooseExcel.Variant = ButtonVariant.Outline;
        btnChooseExcel.ContainerColor = AppTheme.Colors.Surface;
        btnChooseExcel.Click += BtnChooseExcel_Click;
        pnlWorkspace.Controls.Add(btnChooseExcel);

        Label lblEmployee = new()
        {
            Name = "lblEmployee",
            Text = "Mã nhân viên",
            Left = 32,
            Top = 202,
            Width = 150,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlWorkspace.Controls.Add(lblEmployee);

        txtEmployeeCode.Left = 190;
        txtEmployeeCode.Top = 194;
        txtEmployeeCode.Width = 320;
        txtEmployeeCode.Height = 42;
        txtEmployeeCode.Font = AppTheme.Fonts.Body;
        txtEmployeeCode.PlaceholderText = "Nhập mã";
        txtEmployeeCode.ContainerColor = AppTheme.Colors.Surface;
        pnlWorkspace.Controls.Add(txtEmployeeCode);

        Label lblEmployeeHint = new()
        {
            Name = "lblEmployeeHint",
            Text = "Ví dụ: H020 hoặc H020-K026",
            Left = 526,
            Top = 208,
            Width = 320,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        pnlWorkspace.Controls.Add(lblEmployeeHint);

        BuildPriceModeRow();
        BuildNameEditRow();

        pnlDivider2.Left = 32;
        pnlDivider2.Top = 254;
        pnlDivider2.Width = pnlWorkspace.Width - 64;
        pnlDivider2.Height = 1;
        pnlDivider2.BackColor = AppTheme.Colors.Border;
        pnlDivider2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        pnlWorkspace.Controls.Add(pnlDivider2);

        lblSectionActionsTitle.Text = "THAO TÁC";
        lblSectionActionsTitle.Left = 32;
        lblSectionActionsTitle.Top = 274;
        lblSectionActionsTitle.Width = 300;
        lblSectionActionsTitle.Font = AppTheme.Fonts.Overline;
        lblSectionActionsTitle.ForeColor = AppTheme.Colors.TextMuted;
        pnlWorkspace.Controls.Add(lblSectionActionsTitle);

        const int actionHeight = 44;

        flpActions.Left = 28;
        flpActions.Top = 306;
        flpActions.Width = pnlWorkspace.Width - 56;
        // Fixed width (driven by the Anchor below, not AutoSize — AutoSize on a
        // FlowLayoutPanel recomputes width from unwrapped content and fights the
        // anchor-driven shrink, which cut buttons off instead of wrapping them).
        // Fixed height generous enough for two wrapped rows.
        flpActions.Height = (actionHeight + 12) * 2;
        flpActions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        flpActions.BackColor = AppTheme.Colors.SurfaceElevated;
        flpActions.WrapContents = true;
        flpActions.FlowDirection = FlowDirection.LeftToRight;
        pnlWorkspace.Controls.Add(flpActions);

        Padding actionMargin = new(0, 0, 12, 12);

        btnConfig.Text = "Cấu hình";
        btnConfig.Icon = IconGlyphs.Kind.Settings;
        btnConfig.Size = new Size(128, actionHeight);
        btnConfig.Margin = actionMargin;
        btnConfig.Variant = ButtonVariant.Outline;
        btnConfig.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnConfig.Click += BtnConfig_Click;
        flpActions.Controls.Add(btnConfig);

        btnHistory.Text = "Lịch sử";
        btnHistory.Icon = IconGlyphs.Kind.Clock;
        btnHistory.Size = new Size(120, actionHeight);
        btnHistory.Margin = actionMargin;
        btnHistory.Variant = ButtonVariant.Outline;
        btnHistory.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnHistory.Click += BtnHistory_Click;
        flpActions.Controls.Add(btnHistory);

        btnPreview.Text = "Xem trước";
        btnPreview.Icon = IconGlyphs.Kind.Eye;
        btnPreview.Size = new Size(140, actionHeight);
        btnPreview.Margin = actionMargin;
        btnPreview.Variant = ButtonVariant.Outline;
        btnPreview.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnPreview.Click += BtnPreview_Click;
        flpActions.Controls.Add(btnPreview);

        btnCheckParse.Text = "Kiểm tra mã";
        btnCheckParse.Icon = IconGlyphs.Kind.ShieldCheck;
        btnCheckParse.Size = new Size(158, actionHeight);
        btnCheckParse.Margin = actionMargin;
        btnCheckParse.Variant = ButtonVariant.Outline;
        btnCheckParse.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnCheckParse.Click += BtnCheckParse_Click;
        flpActions.Controls.Add(btnCheckParse);

        btnParserLab.Text = "Parser Lab";
        btnParserLab.Icon = IconGlyphs.Kind.Code;
        btnParserLab.Size = new Size(140, actionHeight);
        btnParserLab.Margin = actionMargin;
        // Same Outline style as the other secondary buttons — no more Ghost
        // variant / no more its own font weight, so it's visually identical
        // to Cấu hình / Lịch sử / Xem trước / Kiểm tra mã.
        btnParserLab.Variant = ButtonVariant.Outline;
        btnParserLab.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnParserLab.Click += BtnParserLab_Click;
        flpActions.Controls.Add(btnParserLab);

        btnPrint.Text = "IN TEM";
        btnPrint.Icon = IconGlyphs.Kind.Printer;
        btnPrint.Size = new Size(180, actionHeight);
        btnPrint.Margin = new Padding(0, 0, 0, 12);
        btnPrint.Variant = ButtonVariant.Primary;
        btnPrint.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnPrint.Font = AppTheme.Fonts.Button;
        btnPrint.Click += BtnPrint_Click;
        flpActions.Controls.Add(btnPrint);
    }

    // Cùng hàng/toạ độ với "Mã nhân viên" (lblEmployee/txtEmployeeCode): hai
    // nhóm control này không bao giờ hiện cùng lúc (FULL không dùng mã nhân
    // viên - xem ApplyEmployeeCodeMode), nên dùng chung chỗ trống đó thay vì
    // đẩy layout của THAO TÁC/IN TEM xuống.
    private void BuildPriceModeRow()
    {
        lblPriceMode.Text = "Nhập giá bán";
        lblPriceMode.Left = 32;
        lblPriceMode.Top = 202;
        lblPriceMode.Width = 150;
        lblPriceMode.Font = AppTheme.Fonts.Body;
        lblPriceMode.ForeColor = AppTheme.Colors.TextPrimary;
        pnlWorkspace.Controls.Add(lblPriceMode);

        cboPriceMode.Left = 190;
        cboPriceMode.Top = 194;
        cboPriceMode.Width = 280;
        cboPriceMode.Height = 42;
        cboPriceMode.DropDownStyle = ComboBoxStyle.DropDownList;
        AppTheme.StyleComboBox(cboPriceMode);
        cboPriceMode.Items.Add("Giữ nguyên giá");
        cboPriceMode.Items.Add("Sửa tất cả cùng 1 giá");
        cboPriceMode.Items.Add("Sửa giá một vài sản phẩm");
        cboPriceMode.SelectedIndex = (int)PriceOverrideMode.Keep;
        cboPriceMode.SelectedIndexChanged += CboPriceMode_SelectedIndexChanged;
        pnlWorkspace.Controls.Add(cboPriceMode);

        txtUniformPrice.Left = 486;
        txtUniformPrice.Top = 194;
        txtUniformPrice.Width = 200;
        txtUniformPrice.Height = 42;
        txtUniformPrice.Font = AppTheme.Fonts.Body;
        txtUniformPrice.TextAlign = HorizontalAlignment.Right;
        txtUniformPrice.PlaceholderText = "Nhập giá bán (VNĐ)";
        txtUniformPrice.ContainerColor = AppTheme.Colors.Surface;
        txtUniformPrice.Visible = false;
        txtUniformPrice.TextChanged += TxtUniformPrice_TextChanged;
        pnlWorkspace.Controls.Add(txtUniformPrice);

        btnEditPrices.Text = "Sửa giá...";
        btnEditPrices.Icon = IconGlyphs.Kind.Code;
        btnEditPrices.Left = 486;
        btnEditPrices.Top = 194;
        btnEditPrices.Width = 170;
        btnEditPrices.Height = 42;
        btnEditPrices.Variant = ButtonVariant.Outline;
        btnEditPrices.ContainerColor = AppTheme.Colors.Surface;
        btnEditPrices.Visible = false;
        btnEditPrices.Click += (_, _) => OpenPerProductPriceEditor();
        pnlWorkspace.Controls.Add(btnEditPrices);

        lblPriceStatus.Left = 666;
        lblPriceStatus.Top = 208;
        lblPriceStatus.Width = 280;
        lblPriceStatus.Height = 18;
        lblPriceStatus.Font = AppTheme.Fonts.Hint;
        lblPriceStatus.ForeColor = AppTheme.Colors.TextMuted;
        lblPriceStatus.Visible = false;
        pnlWorkspace.Controls.Add(lblPriceStatus);
    }

    // Hàng riêng ngay dưới hàng giá bán, chỉ hiện với Tem đầy đủ (xem
    // ApplyPriceEditMode). Không đụng tới control/logic của hàng giá bán ở
    // trên - đây là control mới, độc lập hoàn toàn.
    private void BuildNameEditRow()
    {
        btnEditNames.Text = "Sửa tên hàng";
        btnEditNames.Icon = IconGlyphs.Kind.Code;
        btnEditNames.Left = 32;
        btnEditNames.Top = 246;
        btnEditNames.Width = 170;
        btnEditNames.Height = 42;
        btnEditNames.Variant = ButtonVariant.Outline;
        btnEditNames.ContainerColor = AppTheme.Colors.SurfaceElevated;
        btnEditNames.Visible = false;
        btnEditNames.Click += (_, _) => OpenNameEditor();
        pnlWorkspace.Controls.Add(btnEditNames);

        lblNameStatus.Left = 212;
        lblNameStatus.Top = 260;
        lblNameStatus.Width = 500;
        lblNameStatus.Height = 18;
        lblNameStatus.Font = AppTheme.Fonts.Hint;
        lblNameStatus.ForeColor = AppTheme.Colors.TextMuted;
        lblNameStatus.Visible = false;
        pnlWorkspace.Controls.Add(lblNameStatus);
    }
    #endregion

    #region Giá bán (Tem đầy đủ)
    private void CboPriceMode_SelectedIndexChanged(object? sender, EventArgs e)
    {
        _priceMode = (PriceOverrideMode)cboPriceMode.SelectedIndex;
        UpdatePriceModeUi();

        if (_priceMode == PriceOverrideMode.PerProduct)
            OpenPerProductPriceEditor();
    }

    private void OpenPerProductPriceEditor()
    {
        List<ProductRow> products;

        try
        {
            if (string.IsNullOrWhiteSpace(txtExcelFile.Text) || !File.Exists(txtExcelFile.Text.Trim()))
                throw new Exception("Vui lòng chọn file Excel KiotViet trước khi sửa giá từng sản phẩm.");

            products = _labelService.ReadProducts(txtExcelFile.Text.Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi");
            return;
        }

        using FormEditPrices form = new(products, _productPriceOverrides);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _productPriceOverrides.Clear();
            foreach (KeyValuePair<string, double> kv in form.ResultOverrides)
                _productPriceOverrides[kv.Key] = kv.Value;
        }

        UpdatePriceModeUi();
    }

    // Tự định dạng dấu phân cách nghìn (vi-VN) trong lúc gõ, giữ nguyên vị trí
    // con trỏ - vì RoundedTextBox không lộ ra KeyPress để chặn ký tự không
    // phải số, nên lọc/format lại toàn bộ Text mỗi lần thay đổi là đơn giản và
    // an toàn hơn (không thể gõ được ký tự âm/chữ vì luôn bị lọc bỏ).
    private void TxtUniformPrice_TextChanged(object? sender, EventArgs e)
    {
        if (_formattingUniformPrice)
            return;

        string raw = txtUniformPrice.Text;
        int caret = Math.Clamp(txtUniformPrice.SelectionStart, 0, raw.Length);
        int digitsBeforeCaret = raw.Take(caret).Count(char.IsDigit);

        string digits = new(raw.Where(char.IsDigit).ToArray());
        if (digits.Length > 15)
            digits = digits[..15];

        string formatted = digits.Length == 0 || !long.TryParse(digits, out long value)
            ? ""
            : value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"));

        _formattingUniformPrice = true;
        txtUniformPrice.Text = formatted;

        int newCaret = 0;
        int seen = 0;
        while (newCaret < formatted.Length && seen < digitsBeforeCaret)
        {
            if (char.IsDigit(formatted[newCaret]))
                seen++;
            newCaret++;
        }
        txtUniformPrice.SelectionStart = Math.Clamp(newCaret, 0, formatted.Length);
        _formattingUniformPrice = false;
    }

    private bool TryParseUniformPrice(out double price)
    {
        string digits = new(txtUniformPrice.Text.Where(char.IsDigit).ToArray());

        if (digits.Length == 0 || !long.TryParse(digits, out long value) || value < 0)
        {
            price = 0;
            return false;
        }

        price = value;
        return true;
    }

    // Gọi mỗi khi vào workspace của một danh mục tem và mỗi khi đổi file Excel
    // nguồn: chỉ Tem đầy đủ (HandlerType == "FULL") mới hiện các control này,
    // và các mã sản phẩm đã sửa giá gắn với một file Excel cụ thể nên không
    // nên giữ lại khi người dùng đổi sang file khác.
    private void ApplyPriceEditMode(LabelDefinition label)
    {
        bool isFull = label.HandlerType == "FULL";

        lblPriceMode.Visible = isFull;
        cboPriceMode.Visible = isFull;

        UpdatePriceModeUi();

        // Sửa tên hàng dùng chung điều kiện hiển thị (chỉ Tem đầy đủ) nhưng là
        // control/state riêng - xem region "Sửa tên hàng (Tem đầy đủ)".
        btnEditNames.Visible = isFull;
        UpdateNameEditUi();
        RepositionActionsSection();
    }

    private void ResetPriceState()
    {
        _priceMode = PriceOverrideMode.Keep;
        _productPriceOverrides.Clear();

        cboPriceMode.SelectedIndexChanged -= CboPriceMode_SelectedIndexChanged;
        cboPriceMode.SelectedIndex = (int)PriceOverrideMode.Keep;
        cboPriceMode.SelectedIndexChanged += CboPriceMode_SelectedIndexChanged;

        txtUniformPrice.Clear();

        UpdatePriceModeUi();
    }

    private void UpdatePriceModeUi()
    {
        bool isFull = cboPriceMode.Visible;
        bool isKeep = isFull && _priceMode == PriceOverrideMode.Keep;
        bool isUniform = isFull && _priceMode == PriceOverrideMode.Uniform;
        bool isPerProduct = isFull && _priceMode == PriceOverrideMode.PerProduct;

        // Giữ nguyên giá: hiện ô giá nhưng khoá lại (disable), không cho nhập -
        // chỉ còn dòng chữ mờ (placeholder) nhắc là đang dùng nguyên giá Excel.
        txtUniformPrice.Visible = isKeep || isUniform;
        txtUniformPrice.Enabled = isUniform;

        if (isKeep)
        {
            txtUniformPrice.PlaceholderText = "Giữ nguyên giá trong file Excel";
            txtUniformPrice.Clear();
        }
        else if (isUniform)
        {
            txtUniformPrice.PlaceholderText = "Nhập giá bán (VNĐ)";
        }

        btnEditPrices.Visible = isPerProduct;
        lblPriceStatus.Visible = isPerProduct;

        if (isPerProduct)
        {
            lblPriceStatus.Text = _productPriceOverrides.Count == 0
                ? "Chưa sửa giá sản phẩm nào"
                : $"Đã sửa giá {_productPriceOverrides.Count} sản phẩm";
        }
    }
    #endregion

    #region Sửa tên hàng (Tem đầy đủ)
    // Hoàn toàn độc lập với region "Giá bán (Tem đầy đủ)" ở trên - không đọc,
    // không ghi _priceMode/_productPriceOverrides, và ngược lại không region
    // nào ở trên đọc/ghi _productNameOverrides.
    private void OpenNameEditor()
    {
        List<ProductRow> products;

        try
        {
            if (string.IsNullOrWhiteSpace(txtExcelFile.Text) || !File.Exists(txtExcelFile.Text.Trim()))
                throw new Exception("Vui lòng chọn file Excel KiotViet trước khi sửa tên hàng.");

            products = _labelService.ReadProducts(txtExcelFile.Text.Trim());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi");
            return;
        }

        using FormEditNames form = new(products, _productNameOverrides);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _productNameOverrides.Clear();
            foreach (KeyValuePair<string, string> kv in form.ResultOverrides)
                _productNameOverrides[kv.Key] = kv.Value;
        }

        UpdateNameEditUi();
    }

    private void ResetNameState()
    {
        _productNameOverrides.Clear();
        UpdateNameEditUi();
    }

    private void UpdateNameEditUi()
    {
        bool hasOverrides = _productNameOverrides.Count > 0;
        lblNameStatus.Visible = hasOverrides;

        if (hasOverrides)
            lblNameStatus.Text = $"Đã sửa tên {_productNameOverrides.Count} sản phẩm";
    }

    // Chỉ Tem đầy đủ mới có sửa tên hàng - các loại tem khác luôn in bằng
    // đúng tên trong file Excel (trả về null, giống BuildPriceOverrideOrThrow).
    private Dictionary<string, string>? BuildNameOverrideOrNull()
    {
        if (_selectedLabel!.HandlerType != "FULL" || _productNameOverrides.Count == 0)
            return null;

        return new Dictionary<string, string>(_productNameOverrides, StringComparer.OrdinalIgnoreCase);
    }

    // Đẩy phần "THAO TÁC"/IN TEM xuống khi hàng "Sửa tên hàng" đang hiển thị
    // (chỉ Tem đầy đủ), để không đè lên nút/trạng thái của hàng đó. Gọi lại
    // mỗi khi đổi danh mục tem (xem ApplyPriceEditMode).
    private void RepositionActionsSection()
    {
        int extra = btnEditNames.Visible ? 52 : 0;

        pnlDivider2.Top = 254 + extra;
        lblSectionActionsTitle.Top = pnlDivider2.Top + 20;
        flpActions.Top = lblSectionActionsTitle.Top + 32;
    }
    #endregion

    #region Navigation
    private void ShowHome()
    {
        _selectedLabel = null;

        pnlWorkspace.Visible = false;
        bgRemoverView.Visible = false;
        imageCompressorView.Visible = false;
        btnBack.Visible = false;

        lblSubtitle.Text = "Chọn danh mục tem để bắt đầu";

        ReloadCategories();
        UiMotion.SlideIn(pnlCategory, 32, -14);
    }

    private void OpenBackgroundRemover(ToolDefinition tool)
    {
        pnlCategory.Visible = false;
        pnlWorkspace.Visible = false;
        imageCompressorView.Visible = false;
        btnBack.Visible = true;

        UiMotion.SlideIn(bgRemoverView, 32, 14);

        lblSubtitle.Text = $"Công cụ: {tool.Name}";
    }

    private void OpenImageCompressor(ToolDefinition tool)
    {
        pnlCategory.Visible = false;
        pnlWorkspace.Visible = false;
        bgRemoverView.Visible = false;
        btnBack.Visible = true;

        UiMotion.SlideIn(imageCompressorView, 32, 14);

        lblSubtitle.Text = $"Công cụ: {tool.Name}";
    }

    private void OpenLabelWorkspace(LabelDefinition label)
    {
        _selectedLabel = label;

        pnlCategory.Visible = false;
        bgRemoverView.Visible = false;
        imageCompressorView.Visible = false;
        btnBack.Visible = true;

        UiMotion.SlideIn(pnlWorkspace, 32, 14);

        lblSubtitle.Text = $"Danh mục: {label.Name}";

        iconWorkspace.Kind = ResolveDetailIcon(label.HandlerType) ?? IconGlyphs.Kind.Tag;
        lblCurrentCategory.Text = label.Name;
        lblCurrentCategoryCode.Text = $"{label.Code}";

        if (string.IsNullOrWhiteSpace(txtExcelFile.Text) &&
            !string.IsNullOrWhiteSpace(ConfigService.Instance.Config.LastExcelFile) &&
            File.Exists(ConfigService.Instance.Config.LastExcelFile))
        {
            txtExcelFile.Text = ConfigService.Instance.Config.LastExcelFile;
        }

        ApplyEmployeeCodeMode(label);
        ApplyPriceEditMode(label);
    }

    private void ApplyEmployeeCodeMode(LabelDefinition label)
    {
        Control? lblEmployee = pnlWorkspace.Controls["lblEmployee"];
        Control? lblEmployeeHint = pnlWorkspace.Controls["lblEmployeeHint"];

        bool isBarcode = label.HandlerType == "BARCODE";
        bool isGlasses = label.HandlerType == "GLASSES";

        bool showInput = label.AppendEmployeeCode || isBarcode || isGlasses;

        if (lblEmployee != null)
        {
            lblEmployee.Visible = showInput;

            if (isGlasses)
                lblEmployee.Text = "Mã màu";
            else
                lblEmployee.Text = "Mã nhân viên";
        }

        if (lblEmployeeHint != null)
        {
            lblEmployeeHint.Visible = showInput;

            if (isGlasses)
                lblEmployeeHint.Text = "Ví dụ: -1, -2, -3";
            else
                lblEmployeeHint.Text = "Ví dụ: H020 hoặc H020-K026";
        }

        txtEmployeeCode.Visible = showInput;
        txtEmployeeCode.Enabled = showInput;

        if (isGlasses)
        {
            // Tem kính: ô này là mã màu, không load default employee
            txtEmployeeCode.Text = "";
            return;
        }

        if (showInput)
        {
            if (ConfigService.Instance.Config.RememberEmployee &&
                !string.IsNullOrWhiteSpace(ConfigService.Instance.Config.DefaultEmployee) &&
                string.IsNullOrWhiteSpace(txtEmployeeCode.Text))
            {
                txtEmployeeCode.Text = ConfigService.Instance.Config.DefaultEmployee;
            }
        }
        else
        {
            txtEmployeeCode.Text = "";
        }
    }
    #endregion

    #region Events
    private void CheckConfigOnStart()
    {
        if (!ConfigService.Instance.IsConfigured())
        {
            MessageBox.Show("Phần mềm chưa được cấu hình đầy đủ. Vui lòng kiểm tra cấu hình trước khi sử dụng.");

            try
            {
                using FormConfig formConfig = new();
                formConfig.ShowDialog();
            }
            catch
            {
                // Nếu FormConfig chưa refactor xong thì bỏ qua để app vẫn mở được
            }
        }
    }

    private void BtnChooseExcel_Click(object? sender, EventArgs e)
    {
        string initialDir = "";

        if (ConfigService.Instance.Config.AutoOpenLastFolder &&
            !string.IsNullOrWhiteSpace(ConfigService.Instance.Config.LastFolder) &&
            Directory.Exists(ConfigService.Instance.Config.LastFolder))
        {
            initialDir = ConfigService.Instance.Config.LastFolder;
        }

        using OpenFileDialog dialog = new()
        {
            Filter = "Excel Files|*.xls;*.xlsx"
        };

        if (!string.IsNullOrWhiteSpace(initialDir))
            dialog.InitialDirectory = initialDir;

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtExcelFile.Text = dialog.FileName;

            // Giá đã sửa/chọn gắn với sản phẩm của file cũ - đổi file khác thì
            // reset để tránh áp nhầm giá lên sản phẩm không liên quan.
            ResetPriceState();

            // Tương tự cho tên hàng đã sửa - cũng gắn với mã sản phẩm của file
            // cũ, nên reset khi đổi file (độc lập với ResetPriceState ở trên).
            ResetNameState();

            ConfigService.Instance.Config.LastExcelFile = dialog.FileName;

            string? folder = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                ConfigService.Instance.Config.LastFolder = folder;
                ConfigService.Instance.Save();
            }
        }
    }

    private void BtnConfig_Click(object? sender, EventArgs e)
    {
        try
        {
            using FormConfig formConfig = new();
            formConfig.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Form cấu hình chưa sẵn sàng hoặc đang lỗi:\n{ex.Message}", "Thông báo");
        }

        ShowHome();
    }

    private void BtnHistory_Click(object? sender, EventArgs e)
    {
        using FormHistory history = new();
        history.ShowDialog();
    }

    private void BtnParserLab_Click(object? sender, EventArgs e)
    {
        using FormParserLab form = new();
        form.ShowDialog();
    }

    private void BtnPreview_Click(object? sender, EventArgs e)
    {
        try
        {
            EnsureReadyToProcess();

            PriceOverride? priceOverride = BuildPriceOverrideOrThrow();
            Dictionary<string, string>? nameOverrides = BuildNameOverrideOrNull();

            using FormPreview preview = new(
                txtExcelFile.Text.Trim(),
                _selectedLabel!.Code,
                txtEmployeeCode.Text.Trim(),
                priceOverride,
                nameOverrides);

            preview.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi");
        }
    }

    private void BtnCheckParse_Click(object? sender, EventArgs e)
    {
        try
        {
            EnsureReadyToProcess();

            using FormParseCheck check = new(
                txtExcelFile.Text.Trim(),
                _selectedLabel!.Code,
                txtEmployeeCode.Text.Trim());

            check.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi");
        }
    }

    private async void BtnPrint_Click(object? sender, EventArgs e)
    {
        try
        {
            EnsureReadyToProcess();

            string sourceExcelFile = txtExcelFile.Text.Trim();
            string labelCode = _selectedLabel!.Code;
            string employeeCode = txtEmployeeCode.Text.Trim();
            PriceOverride? priceOverride = BuildPriceOverrideOrThrow();
            Dictionary<string, string>? nameOverrides = BuildNameOverrideOrNull();

            // In số lượng lớn có thể mất nhiều phút (phải chờ máy in xử lý
            // xong từng mã trước khi in mã kế tiếp — xem BarTenderService).
            // Chạy trên UI thread sẽ làm app "Not Responding", khiến người
            // dùng tưởng treo rồi tắt app giữa chừng → mất tem đã in dở.
            btnPrint.Enabled = false;
            string originalText = btnPrint.Text;
            btnPrint.Text = "Đang in...";
            Cursor = Cursors.WaitCursor;

            try
            {
                int productCount = await Task.Run(() => _labelService.Print(
                    sourceExcelFile,
                    labelCode,
                    employeeCode,
                    priceOverride,
                    nameOverrides));

                ToastForm.ShowSuccess($"In thành công. Số sản phẩm: {productCount}");
            }
            finally
            {
                Cursor = Cursors.Default;
                btnPrint.Text = originalText;
                btnPrint.Enabled = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi in tem");
        }
    }

    private void EnsureReadyToProcess()
    {
        if (_selectedLabel == null)
            throw new Exception("Vui lòng chọn danh mục tem.");

        if (string.IsNullOrWhiteSpace(txtExcelFile.Text))
            throw new Exception("Vui lòng chọn file Excel KiotViet.");

        if (!File.Exists(txtExcelFile.Text.Trim()))
            throw new Exception("Không tìm thấy file Excel đã chọn.");

        if (!ConfigService.Instance.IsConfigured())
            throw new Exception("Cấu hình chưa đầy đủ.");
    }

    // Chỉ Tem đầy đủ mới có chỉnh giá bán - các loại tem khác luôn in bằng
    // đúng giá trong file Excel (trả về null).
    private PriceOverride? BuildPriceOverrideOrThrow()
    {
        if (_selectedLabel!.HandlerType != "FULL")
            return null;

        switch (_priceMode)
        {
            case PriceOverrideMode.Uniform:
                if (!TryParseUniformPrice(out double uniformPrice))
                    throw new Exception("Vui lòng nhập giá bán hợp lệ (không được để trống hoặc âm).");

                return new PriceOverride { Mode = PriceOverrideMode.Uniform, UniformPrice = uniformPrice };

            case PriceOverrideMode.PerProduct:
                return new PriceOverride
                {
                    Mode = PriceOverrideMode.PerProduct,
                    ProductOverrides = new Dictionary<string, double>(_productPriceOverrides, StringComparer.OrdinalIgnoreCase)
                };

            default:
                return null;
        }
    }
    #endregion
}
