
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormMain : Form
{
    private readonly LabelService _labelService = new();
    private readonly LabelCatalogService _catalogService = new();

    // Header
    private readonly Panel pnlHeader = new();
    private readonly Button btnBack = new();
    private readonly PictureBox picLogo = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSubtitle = new();

    // Home / Category
    private readonly Panel pnlCategory = new();
    private readonly FlowLayoutPanel flpCategories = new();

    // Workspace
    private readonly Panel pnlWorkspace = new();
    private readonly Label lblCurrentCategory = new();

    private readonly TextBox txtExcelFile = new();
    private readonly TextBox txtEmployeeCode = new();

    private readonly Button btnChooseExcel = new();
    private readonly Button btnConfig = new();
    private readonly Button btnHistory = new();
     private readonly Button btnParserLab = new();
    private readonly Button btnPreview = new();
    private readonly Button btnPrint = new();

    private LabelDefinition? _selectedLabel;

    public FormMain()
    {
        Text = "KiotViet Label Printer";
        Width = 980;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.White;

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
        pnlHeader.Height = 110;
        pnlHeader.BackColor = Color.FromArgb(245, 247, 250);
        pnlHeader.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(pnlHeader);

        btnBack.Text = "← Back";
        btnBack.Left = 20;
        btnBack.Top = 20;
        btnBack.Width = 90;
        btnBack.Height = 32;
        btnBack.Visible = false;
        btnBack.Click += (_, _) => ShowHome();
        pnlHeader.Controls.Add(btnBack);

        picLogo.Left = 130;
        picLogo.Top = 18;
        picLogo.Width = 64;
        picLogo.Height = 64;
        picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        picLogo.BorderStyle = BorderStyle.FixedSingle;

        // Nếu có logo thì mở comment đoạn này:
        // string logoPath = Path.Combine(Application.StartupPath, "Assets", "logo.png");
        // if (File.Exists(logoPath))
        // {
        //     picLogo.Image = Image.FromFile(logoPath);
        // }

        pnlHeader.Controls.Add(picLogo);

        lblTitle.Text = "IN TEM";
        lblTitle.Left = 210;
        lblTitle.Top = 18;
        lblTitle.Width = 400;
        lblTitle.Font = new Font("Segoe UI", 20, FontStyle.Bold);
        pnlHeader.Controls.Add(lblTitle);

        lblSubtitle.Text = "Chọn danh mục tem để bắt đầu";
        lblSubtitle.Left = 212;
        lblSubtitle.Top = 58;
        lblSubtitle.Width = 600;
        lblSubtitle.Font = new Font("Segoe UI", 10, FontStyle.Regular);
        lblSubtitle.ForeColor = Color.DimGray;
        pnlHeader.Controls.Add(lblSubtitle);
    }
    #endregion

    #region Category panel
    private void BuildCategoryPanel()
    {
        pnlCategory.Left = 20;
        pnlCategory.Top = 130;
        pnlCategory.Width = 920;
        pnlCategory.Height = 420;
        pnlCategory.BackColor = Color.White;
        Controls.Add(pnlCategory);

        Label lblCategoryTitle = new()
        {
            Text = "DANH MỤC TEM",
            Left = 10,
            Top = 10,
            Width = 300,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };
        pnlCategory.Controls.Add(lblCategoryTitle);

        Label lblHint = new()
        {
            Text = "Chọn loại tem cần in. Danh sách này được lấy từ cấu hình.",
            Left = 10,
            Top = 40,
            Width = 700,
            ForeColor = Color.DimGray
        };
        pnlCategory.Controls.Add(lblHint);

        flpCategories.Left = 10;
        flpCategories.Top = 80;
        flpCategories.Width = 880;
        flpCategories.Height = 300;
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
                ForeColor = Color.DarkRed,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
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
        Panel card = new()
        {
            Width = 260,
            Height = 150,
            Margin = new Padding(12),
            BackColor = Color.FromArgb(248, 249, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };

        Label lblIcon = new()
        {
            Text = string.IsNullOrWhiteSpace(label.IconText) ? "🏷" : label.IconText,
            Left = 18,
            Top = 16,
            Width = 50,
            Height = 40,
            Font = new Font("Segoe UI Emoji", 20, FontStyle.Regular)
        };

        Label lblName = new()
        {
            Text = label.Name,
            Left = 18,
            Top = 60,
            Width = 220,
            Font = new Font("Segoe UI", 12, FontStyle.Bold)
        };

        Label lblDesc = new()
        {
            Text = label.Description,
            Left = 18,
            Top = 95,
            Width = 220,
            Height = 40,
            ForeColor = Color.DimGray
        };

        card.Controls.Add(lblIcon);
        card.Controls.Add(lblName);
        card.Controls.Add(lblDesc);

        void open(object? s, EventArgs e) => OpenLabelWorkspace(label);

        card.Click += open;
        lblIcon.Click += open;
        lblName.Click += open;
        lblDesc.Click += open;

        return card;
    }
    #endregion

    #region Workspace panel
    private void BuildWorkspacePanel()
    {
        pnlWorkspace.Left = 20;
        pnlWorkspace.Top = 130;
        pnlWorkspace.Width = 920;
        pnlWorkspace.Height = 420;
        pnlWorkspace.BackColor = Color.White;
        pnlWorkspace.Visible = false;
        Controls.Add(pnlWorkspace);

        lblCurrentCategory.Text = "Danh mục:";
        lblCurrentCategory.Left = 10;
        lblCurrentCategory.Top = 10;
        lblCurrentCategory.Width = 700;
        lblCurrentCategory.Font = new Font("Segoe UI", 12, FontStyle.Bold);
        pnlWorkspace.Controls.Add(lblCurrentCategory);

        Label lblExcel = new()
        {
            Text = "File Excel KiotViet",
            Left = 10,
            Top = 70,
            Width = 140
        };
        pnlWorkspace.Controls.Add(lblExcel);

        txtExcelFile.Left = 160;
        txtExcelFile.Top = 66;
        txtExcelFile.Width = 520;
        txtExcelFile.ReadOnly = true;
        pnlWorkspace.Controls.Add(txtExcelFile);

        btnChooseExcel.Text = "Chọn file";
        btnChooseExcel.Left = 700;
        btnChooseExcel.Top = 64;
        btnChooseExcel.Width = 110;
        btnChooseExcel.Height = 32;
        btnChooseExcel.Click += BtnChooseExcel_Click;
        pnlWorkspace.Controls.Add(btnChooseExcel);

        Label lblEmployee = new()
        {
            Name = "lblEmployee",
            Text = "Mã nhân viên",
            Left = 10,
            Top = 125,
            Width = 140
        };
        pnlWorkspace.Controls.Add(lblEmployee);

        txtEmployeeCode.Left = 160;
        txtEmployeeCode.Top = 121;
        txtEmployeeCode.Width = 300;
        pnlWorkspace.Controls.Add(txtEmployeeCode);

        Label lblEmployeeHint = new()
        {
            Name = "lblEmployeeHint",
            Text = "Ví dụ: H020 hoặc H020-K026",
            Left = 470,
            Top = 125,
            Width = 260,
            ForeColor = Color.DimGray
        };
        pnlWorkspace.Controls.Add(lblEmployeeHint);

        Panel line = new()
        {
            Left = 10,
            Top = 170,
            Width = 860,
            Height = 1,
            BackColor = Color.Gainsboro
        };
        pnlWorkspace.Controls.Add(line);

        btnConfig.Text = "Cấu hình";
        btnConfig.Left = 60;
        btnConfig.Top = 220;
        btnConfig.Width = 120;
        btnConfig.Height = 42;
        btnConfig.Click += BtnConfig_Click;
        pnlWorkspace.Controls.Add(btnConfig);

        btnHistory.Text = "Lịch sử";
        btnHistory.Left = 210;
        btnHistory.Top = 220;
        btnHistory.Width = 120;
        btnHistory.Height = 42;
        btnHistory.Click += BtnHistory_Click;
        pnlWorkspace.Controls.Add(btnHistory);


        btnParserLab.Text = "Parser Lab";
        btnParserLab.Left = 360;
        btnParserLab.Top = 280;
        btnParserLab.Width = 140;
        btnParserLab.Height = 42;
        btnParserLab.Click += BtnParserLab_Click;
        pnlWorkspace.Controls.Add(btnParserLab);

        btnPreview.Text = "Xem trước";
        btnPreview.Left = 360;
        btnPreview.Top = 220;
        btnPreview.Width = 140;
        btnPreview.Height = 42;
        btnPreview.Click += BtnPreview_Click;
        pnlWorkspace.Controls.Add(btnPreview);

        btnPrint.Text = "IN TEM";
        btnPrint.Left = 530;
        btnPrint.Top = 220;
        btnPrint.Width = 160;
        btnPrint.Height = 42;
        btnPrint.Font = new Font("Segoe UI", 10, FontStyle.Bold);
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
        lblCurrentCategory.Text = $"Danh mục: {label.Name} ({label.Code})";

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
    txtEmployeeCode.BackColor = showInput ? Color.White : Color.Gainsboro;

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
    private void btnParserLab_Click(
    object sender,
    EventArgs e)
{
    FormParserLab form = new();

    form.ShowDialog();
}
    #endregion
}