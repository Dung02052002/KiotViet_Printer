
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
    private readonly Label lblLogoIcon = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSubtitle = new();

    // Home / Category
    private readonly Panel pnlCategory = new();
    private readonly FlowLayoutPanel flpCategories = new();

    // Workspace
    private readonly RoundedPanel pnlWorkspace = new();
    private readonly Label lblCurrentCategory = new();

    private readonly TextBox txtExcelFile = new();
    private readonly TextBox txtEmployeeCode = new();

    private readonly RoundedButton btnChooseExcel = new();
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
        Width = 980;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        CheckConfigOnStart();
        ShowHome();
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
        pnlHeader.Height = 104;
        pnlHeader.BackColor = AppTheme.Colors.Surface;
        pnlHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
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

        pnlLogoBadge.Left = 24;
        pnlLogoBadge.Top = 24;
        pnlLogoBadge.Width = 56;
        pnlLogoBadge.Height = 56;
        pnlLogoBadge.CornerRadius = 16;
        pnlLogoBadge.FillColor = AppTheme.Colors.PrimaryLight;
        pnlLogoBadge.BorderThickness = 0;
        pnlLogoBadge.ContainerColor = AppTheme.Colors.Surface;
        pnlHeader.Controls.Add(pnlLogoBadge);

        lblLogoIcon.Text = "🏷";
        lblLogoIcon.Dock = DockStyle.Fill;
        lblLogoIcon.TextAlign = ContentAlignment.MiddleCenter;
        lblLogoIcon.Font = AppTheme.Fonts.Icon;
        lblLogoIcon.ForeColor = AppTheme.Colors.Primary;
        pnlLogoBadge.Controls.Add(lblLogoIcon);

        lblTitle.Text = "IN TEM";
        lblTitle.Left = 96;
        lblTitle.Top = 20;
        lblTitle.Width = 400;
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        pnlHeader.Controls.Add(lblTitle);

        lblSubtitle.Text = "Chọn danh mục tem để bắt đầu";
        lblSubtitle.Left = 98;
        lblSubtitle.Top = 58;
        lblSubtitle.Width = 600;
        lblSubtitle.Font = AppTheme.Fonts.Subtitle;
        lblSubtitle.ForeColor = AppTheme.Colors.TextSecondary;
        pnlHeader.Controls.Add(lblSubtitle);

        btnBack.Text = "← Quay lại";
        btnBack.Width = 112;
        btnBack.Height = 36;
        btnBack.Top = 34;
        btnBack.Left = pnlHeader.Width - btnBack.Width - 24;
        btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnBack.Variant = ButtonVariant.Ghost;
        btnBack.ContainerColor = AppTheme.Colors.Surface;
        btnBack.Font = AppTheme.Fonts.ButtonRegular;
        btnBack.Visible = false;
        btnBack.Click += (_, _) => ShowHome();
        pnlHeader.Controls.Add(btnBack);
    }
    #endregion

    #region Category panel
    private void BuildCategoryPanel()
    {
        pnlCategory.Left = 20;
        pnlCategory.Top = 128;
        pnlCategory.Width = 924;
        pnlCategory.Height = 420;
        pnlCategory.BackColor = AppTheme.Colors.Background;
        Controls.Add(pnlCategory);

        Label lblCategoryTitle = new()
        {
            Text = "DANH MỤC TEM",
            Left = 4,
            Top = 6,
            Width = 300,
            Font = AppTheme.Fonts.SectionTitle,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlCategory.Controls.Add(lblCategoryTitle);

        Label lblHint = new()
        {
            Text = "Chọn loại tem cần in. Danh sách này được lấy từ cấu hình.",
            Left = 4,
            Top = 34,
            Width = 700,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextSecondary
        };
        pnlCategory.Controls.Add(lblHint);

        flpCategories.Left = 0;
        flpCategories.Top = 68;
        flpCategories.Width = 924;
        flpCategories.Height = 350;
        flpCategories.BackColor = AppTheme.Colors.Background;
        flpCategories.AutoScroll = true;
        flpCategories.WrapContents = true;
        flpCategories.FlowDirection = FlowDirection.LeftToRight;
        pnlCategory.Controls.Add(flpCategories);
    }

    private void ReloadCategories()
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
        {
            flpCategories.Controls.Add(CreateCategoryCard(label));
        }
    }

    private Control CreateCategoryCard(LabelDefinition label)
    {
        RoundedPanel card = new()
        {
            Width = 280,
            Height = 152,
            Margin = new Padding(0, 0, 16, 16),
            CornerRadius = 14,
            FillColor = AppTheme.Colors.Surface,
            BorderColor = AppTheme.Colors.Border,
            BorderThickness = 1,
            HoverEffect = true,
            HoverFillColor = AppTheme.Colors.Surface,
            HoverBorderColor = AppTheme.Colors.Primary,
            Cursor = Cursors.Hand
        };

        RoundedPanel iconBadge = new()
        {
            Left = 18,
            Top = 18,
            Width = 44,
            Height = 44,
            CornerRadius = 12,
            FillColor = AppTheme.Colors.PrimaryLight,
            BorderThickness = 0,
            ContainerColor = AppTheme.Colors.Surface,
            Cursor = Cursors.Hand
        };

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

        Label lblName = new()
        {
            Text = label.Name,
            Left = 18,
            Top = 74,
            Width = 244,
            Height = 24,
            Font = AppTheme.Fonts.BodyBold,
            ForeColor = AppTheme.Colors.TextPrimary,
            Cursor = Cursors.Hand
        };

        Label lblDesc = new()
        {
            Text = label.Description,
            Left = 18,
            Top = 100,
            Width = 244,
            Height = 40,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextSecondary,
            Cursor = Cursors.Hand
        };

        Label lblArrow = new()
        {
            Text = "→",
            Left = 244,
            Top = 16,
            Width = 20,
            Height = 20,
            Font = AppTheme.Fonts.BodyBold,
            ForeColor = AppTheme.Colors.TextMuted,
            Cursor = Cursors.Hand
        };

        card.Controls.Add(iconBadge);
        card.Controls.Add(lblName);
        card.Controls.Add(lblDesc);
        card.Controls.Add(lblArrow);

        void open(object? s, EventArgs e) => OpenLabelWorkspace(label);

        card.Click += open;
        lblIcon.Click += open;
        lblName.Click += open;
        lblDesc.Click += open;
        lblArrow.Click += open;

        return card;
    }
    #endregion

    #region Workspace panel
    private void BuildWorkspacePanel()
    {
        pnlWorkspace.Left = 20;
        pnlWorkspace.Top = 128;
        pnlWorkspace.Width = 924;
        pnlWorkspace.Height = 360;
        pnlWorkspace.CornerRadius = 14;
        pnlWorkspace.FillColor = AppTheme.Colors.Surface;
        pnlWorkspace.BorderColor = AppTheme.Colors.Border;
        pnlWorkspace.BorderThickness = 1;
        pnlWorkspace.Visible = false;
        Controls.Add(pnlWorkspace);

        lblCurrentCategory.Text = "Danh mục:";
        lblCurrentCategory.Left = 28;
        lblCurrentCategory.Top = 22;
        lblCurrentCategory.Width = 760;
        lblCurrentCategory.Height = 28;
        lblCurrentCategory.Font = AppTheme.Fonts.SectionTitle;
        lblCurrentCategory.ForeColor = AppTheme.Colors.TextPrimary;
        pnlWorkspace.Controls.Add(lblCurrentCategory);

        Panel line1 = new()
        {
            Left = 28,
            Top = 62,
            Width = 868,
            Height = 1,
            BackColor = AppTheme.Colors.Border
        };
        pnlWorkspace.Controls.Add(line1);

        Label lblSectionSource = new()
        {
            Text = "NGUỒN DỮ LIỆU",
            Left = 28,
            Top = 78,
            Width = 300,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        pnlWorkspace.Controls.Add(lblSectionSource);

        Label lblExcel = new()
        {
            Text = "File Excel KiotViet",
            Left = 28,
            Top = 108,
            Width = 140,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlWorkspace.Controls.Add(lblExcel);

        txtExcelFile.Left = 174;
        txtExcelFile.Top = 104;
        txtExcelFile.Width = 526;
        txtExcelFile.Height = 32;
        txtExcelFile.Font = AppTheme.Fonts.Body;
        txtExcelFile.ReadOnly = true;
        pnlWorkspace.Controls.Add(txtExcelFile);

        btnChooseExcel.Text = "Chọn file";
        btnChooseExcel.Left = 710;
        btnChooseExcel.Top = 102;
        btnChooseExcel.Width = 110;
        btnChooseExcel.Height = 34;
        btnChooseExcel.Variant = ButtonVariant.Outline;
        btnChooseExcel.ContainerColor = AppTheme.Colors.Surface;
        btnChooseExcel.Click += BtnChooseExcel_Click;
        pnlWorkspace.Controls.Add(btnChooseExcel);

        Label lblEmployee = new()
        {
            Name = "lblEmployee",
            Text = "Mã nhân viên",
            Left = 28,
            Top = 154,
            Width = 140,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        pnlWorkspace.Controls.Add(lblEmployee);

        txtEmployeeCode.Left = 174;
        txtEmployeeCode.Top = 148;
        txtEmployeeCode.Width = 300;
        txtEmployeeCode.Height = 32;
        txtEmployeeCode.Font = AppTheme.Fonts.Body;
        pnlWorkspace.Controls.Add(txtEmployeeCode);

        Label lblEmployeeHint = new()
        {
            Name = "lblEmployeeHint",
            Text = "Ví dụ: H020 hoặc H020-K026",
            Left = 488,
            Top = 154,
            Width = 300,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        pnlWorkspace.Controls.Add(lblEmployeeHint);

        Panel line2 = new()
        {
            Left = 28,
            Top = 198,
            Width = 868,
            Height = 1,
            BackColor = AppTheme.Colors.Border
        };
        pnlWorkspace.Controls.Add(line2);

        Label lblSectionActions = new()
        {
            Text = "THAO TÁC",
            Left = 28,
            Top = 214,
            Width = 300,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        pnlWorkspace.Controls.Add(lblSectionActions);

        int actionsTop = 244;
        int actionsHeight = 42;

        btnConfig.Text = "⚙ Cấu hình";
        btnConfig.Left = 28;
        btnConfig.Top = actionsTop;
        btnConfig.Width = 116;
        btnConfig.Height = actionsHeight;
        btnConfig.Variant = ButtonVariant.Outline;
        btnConfig.ContainerColor = AppTheme.Colors.Surface;
        btnConfig.Click += BtnConfig_Click;
        pnlWorkspace.Controls.Add(btnConfig);

        btnHistory.Text = "🕒 Lịch sử";
        btnHistory.Left = 156;
        btnHistory.Top = actionsTop;
        btnHistory.Width = 110;
        btnHistory.Height = actionsHeight;
        btnHistory.Variant = ButtonVariant.Outline;
        btnHistory.ContainerColor = AppTheme.Colors.Surface;
        btnHistory.Click += BtnHistory_Click;
        pnlWorkspace.Controls.Add(btnHistory);

        btnPreview.Text = "👁 Xem trước";
        btnPreview.Left = 278;
        btnPreview.Top = actionsTop;
        btnPreview.Width = 136;
        btnPreview.Height = actionsHeight;
        btnPreview.Variant = ButtonVariant.Outline;
        btnPreview.ContainerColor = AppTheme.Colors.Surface;
        btnPreview.Click += BtnPreview_Click;
        pnlWorkspace.Controls.Add(btnPreview);

        btnCheckParse.Text = "✓ Kiểm tra mã";
        btnCheckParse.Left = 426;
        btnCheckParse.Top = actionsTop;
        btnCheckParse.Width = 150;
        btnCheckParse.Height = actionsHeight;
        btnCheckParse.Variant = ButtonVariant.Outline;
        btnCheckParse.ContainerColor = AppTheme.Colors.Surface;
        btnCheckParse.Click += BtnCheckParse_Click;
        pnlWorkspace.Controls.Add(btnCheckParse);

        btnParserLab.Text = "Parser Lab";
        btnParserLab.Left = 588;
        btnParserLab.Top = actionsTop;
        btnParserLab.Width = 110;
        btnParserLab.Height = actionsHeight;
        btnParserLab.Variant = ButtonVariant.Ghost;
        btnParserLab.ContainerColor = AppTheme.Colors.Surface;
        btnParserLab.Font = AppTheme.Fonts.ButtonRegular;
        btnParserLab.Click += BtnParserLab_Click;
        pnlWorkspace.Controls.Add(btnParserLab);

        btnPrint.Text = "🖨 IN TEM";
        btnPrint.Left = 706;
        btnPrint.Top = actionsTop - 2;
        btnPrint.Width = 190;
        btnPrint.Height = actionsHeight + 4;
        btnPrint.Variant = ButtonVariant.Primary;
        btnPrint.ContainerColor = AppTheme.Colors.Surface;
        btnPrint.Font = new Font(AppTheme.Fonts.Button.FontFamily, 10.5f, FontStyle.Bold);
        btnPrint.Click += BtnPrint_Click;
        pnlWorkspace.Controls.Add(btnPrint);
    }
    #endregion

    #region Navigation
    private void ShowHome()
    {
        _selectedLabel = null;

        pnlCategory.Visible = true;
        pnlWorkspace.Visible = false;
        btnBack.Visible = false;

        lblSubtitle.Text = "Chọn danh mục tem để bắt đầu";

        ReloadCategories();
    }

    private void OpenLabelWorkspace(LabelDefinition label)
    {
        _selectedLabel = label;

        pnlCategory.Visible = false;
        pnlWorkspace.Visible = true;
        btnBack.Visible = true;

        lblSubtitle.Text = $"Danh mục: {label.Name}";

        string icon = string.IsNullOrWhiteSpace(label.IconText) ? "🏷" : label.IconText;
        lblCurrentCategory.Text = $"{icon}  {label.Name}   ·   {label.Code}";

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
        txtEmployeeCode.BackColor = showInput ? AppTheme.Colors.Surface : AppTheme.Colors.Disabled;

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
