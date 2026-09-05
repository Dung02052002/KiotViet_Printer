
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormMain : Form
{
    private readonly LabelService _labelService = new();
    private readonly LabelCatalogService _catalogService = new();

    // Header
    private readonly Panel pnlHeader = new();
    private readonly RoundedButton btnBack = new();
    private readonly RoundedPanel pnlLogoBadge = new();
    private readonly IconGlyph iconLogo = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSubtitle = new();

    // Home / Category
    private readonly Panel pnlCategory = new();
    private readonly SmoothFlowLayoutPanel flpCategories = new();

    // Workspace
    private readonly RoundedPanel pnlWorkspace = new();
    private readonly RoundedPanel pnlWorkspaceIcon = new();
    private readonly IconGlyph iconWorkspace = new();
    private readonly Label lblCurrentCategory = new();
    private readonly Label lblCurrentCategoryCode = new();

    private readonly RoundedTextBox txtExcelFile = new();
    private readonly RoundedTextBox txtEmployeeCode = new();

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
        Height = 760;
        // Tall enough that the detail card (icon/header + 2 field rows + up to
        // two wrapped rows of action buttons) never gets clipped by the card's
        // own bottom-anchored edge at the smallest allowed window size.
        MinimumSize = new Size(940, 760);
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
    }

    private void BuildUi()
    {
        BuildHeader();
        BuildCategoryPanel();
        BuildWorkspacePanel();
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
        flpCategories.Width = pnlCategory.Width;
        flpCategories.Height = pnlCategory.Height - 72;
        flpCategories.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        flpCategories.BackColor = AppTheme.Colors.Background;
        flpCategories.AutoScroll = true;
        flpCategories.WrapContents = true;
        flpCategories.FlowDirection = FlowDirection.LeftToRight;
        pnlCategory.Controls.Add(flpCategories);
    }

    private void ReloadCategories()
    {
        flpCategories.SuspendLayout();

        try
        {
            flpCategories.Controls.Clear();

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
                return;
            }

            foreach (LabelDefinition label in labels)
                flpCategories.Controls.Add(CreateCategoryCard(label));
        }
        finally
        {
            flpCategories.ResumeLayout(true);
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
            AccessibleName = label.Name
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

        IconGlyphs.Kind? homeIcon = ResolveHomeIcon(label.HandlerType);

        if (homeIcon.HasValue)
        {
            IconGlyph icon = new()
            {
                Dock = DockStyle.Fill,
                Kind = homeIcon.Value,
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
                Text = string.IsNullOrWhiteSpace(label.IconText) ? "🏷" : label.IconText,
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
            Text = label.Name,
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
            Text = label.Description,
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

        void open(object? s, EventArgs e) => OpenLabelWorkspace(label);

        card.Click += open;
        lblName.Click += open;
        lblDesc.Click += open;
        arrowBadge.Click += open;
        arrowIcon.Click += open;

        return card;
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

        Panel line2 = new()
        {
            Left = 32,
            Top = 254,
            Width = pnlWorkspace.Width - 64,
            Height = 1,
            BackColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        pnlWorkspace.Controls.Add(line2);

        Label lblSectionActions = new()
        {
            Text = "THAO TÁC",
            Left = 32,
            Top = 274,
            Width = 300,
            Font = AppTheme.Fonts.Overline,
            ForeColor = AppTheme.Colors.TextMuted
        };
        pnlWorkspace.Controls.Add(lblSectionActions);

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
    #endregion

    #region Navigation
    private void ShowHome()
    {
        _selectedLabel = null;

        pnlWorkspace.Visible = false;
        btnBack.Visible = false;

        lblSubtitle.Text = "Chọn danh mục tem để bắt đầu";

        ReloadCategories();
        UiMotion.SlideIn(pnlCategory, 32, -14);
    }

    private void OpenLabelWorkspace(LabelDefinition label)
    {
        _selectedLabel = label;

        pnlCategory.Visible = false;
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

            using FormPreview preview = new(
                txtExcelFile.Text.Trim(),
                _selectedLabel!.Code,
                txtEmployeeCode.Text.Trim());

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
                    employeeCode));

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
    #endregion
}
