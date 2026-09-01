using System.ComponentModel;
using System.Drawing.Printing;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormConfig : Form
{
    private readonly TextBox txtBarTender = new();
    private readonly ComboBox cboPrinter = new();
    private readonly CheckBox chkRememberEmployee = new();
    private readonly TextBox txtDefaultEmployee = new();

    private readonly RoundedButton btnBrowseBarTender = new();
    private readonly RoundedButton btnSave = new();
    private readonly RoundedButton btnAddLabel = new();
    private readonly RoundedButton btnDeleteLabel = new();

    private readonly DataGridView dgvLabels = new();

    private BindingList<LabelDefinition> _labels = new();

    public FormConfig()
    {
        Text = "Cấu hình phần mềm";
        Width = 1400;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        LoadConfig();
    }

    private void BuildUi()
    {
        BuildGeneralConfigSection();
        BuildLabelGridSection();
        BuildBottomButtons();
    }

    #region UI - General config
    private void BuildGeneralConfigSection()
    {
        RoundedPanel grpGeneral = new()
        {
            Left = 20,
            Top = 20,
            Width = 1340,
            Height = 204,
            CornerRadius = 14,
            FillColor = AppTheme.Colors.Surface,
            BorderColor = AppTheme.Colors.Border
        };
        Controls.Add(grpGeneral);

        Label lblSectionTitle = new()
        {
            Text = "Cấu hình chung",
            Left = 24,
            Top = 14,
            Width = 400,
            Font = AppTheme.Fonts.SectionTitle,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpGeneral.Controls.Add(lblSectionTitle);

        Panel divider = new()
        {
            Left = 24,
            Top = 46,
            Width = 1292,
            Height = 1,
            BackColor = AppTheme.Colors.Border
        };
        grpGeneral.Controls.Add(divider);

        Label lblBarTender = new()
        {
            Text = "BarTender.exe",
            Left = 24,
            Top = 68,
            Width = 120,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpGeneral.Controls.Add(lblBarTender);

        txtBarTender.SetBounds(160, 64, 1092, 30);
        txtBarTender.Font = AppTheme.Fonts.Body;
        grpGeneral.Controls.Add(txtBarTender);

        btnBrowseBarTender.Text = "...";
        btnBrowseBarTender.SetBounds(1266, 64, 50, 30);
        btnBrowseBarTender.Variant = ButtonVariant.Outline;
        btnBrowseBarTender.ContainerColor = AppTheme.Colors.Surface;
        btnBrowseBarTender.Click += (_, _) => BrowseFile(txtBarTender, "Executable|*.exe");
        grpGeneral.Controls.Add(btnBrowseBarTender);

        Label lblPrinter = new()
        {
            Text = "Máy in tem",
            Left = 24,
            Top = 112,
            Width = 120,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpGeneral.Controls.Add(lblPrinter);

        cboPrinter.SetBounds(160, 108, 500, 30);
        cboPrinter.Font = AppTheme.Fonts.Body;
        cboPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        LoadPrinterList();
        grpGeneral.Controls.Add(cboPrinter);

        Label lblPrinterHint = new()
        {
            Text = "Cố định máy in để tránh bị đổi sang máy in khác khi in.",
            Left = 676,
            Top = 112,
            Width = 420,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        grpGeneral.Controls.Add(lblPrinterHint);

        chkRememberEmployee.Text = "Ghi nhớ mã nhân viên mặc định";
        chkRememberEmployee.Left = 24;
        chkRememberEmployee.Top = 158;
        chkRememberEmployee.Width = 280;
        chkRememberEmployee.Font = AppTheme.Fonts.Body;
        grpGeneral.Controls.Add(chkRememberEmployee);

        Label lblDefaultEmployee = new()
        {
            Text = "Mã NV mặc định",
            Left = 332,
            Top = 160,
            Width = 130,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpGeneral.Controls.Add(lblDefaultEmployee);

        txtDefaultEmployee.SetBounds(468, 156, 220, 30);
        txtDefaultEmployee.Font = AppTheme.Fonts.Body;
        grpGeneral.Controls.Add(txtDefaultEmployee);

        Label lblHint = new()
        {
            Text = "Ví dụ: H020 hoặc H020-K026",
            Left = 700,
            Top = 160,
            Width = 260,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        grpGeneral.Controls.Add(lblHint);
    }

    private void LoadPrinterList()
    {
        cboPrinter.Items.Clear();

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            cboPrinter.Items.Add(printer);
        }
    }
    #endregion

    #region UI - Label grid
    private void BuildLabelGridSection()
    {
        RoundedPanel grpLabels = new()
        {
            Left = 20,
            Top = 240,
            Width = 1340,
            Height = 460,
            CornerRadius = 14,
            FillColor = AppTheme.Colors.Surface,
            BorderColor = AppTheme.Colors.Border
        };
        Controls.Add(grpLabels);

        Label lblSectionTitle = new()
        {
            Text = "Danh sách loại tem",
            Left = 24,
            Top = 14,
            Width = 400,
            Font = AppTheme.Fonts.SectionTitle,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpLabels.Controls.Add(lblSectionTitle);

        Panel divider = new()
        {
            Left = 24,
            Top = 46,
            Width = 1292,
            Height = 1,
            BackColor = AppTheme.Colors.Border
        };
        grpLabels.Controls.Add(divider);

        btnAddLabel.Text = "+ Thêm tem";
        btnAddLabel.SetBounds(24, 60, 120, 34);
        btnAddLabel.Variant = ButtonVariant.Outline;
        btnAddLabel.ContainerColor = AppTheme.Colors.Surface;
        btnAddLabel.Click += BtnAddLabel_Click;
        grpLabels.Controls.Add(btnAddLabel);

        btnDeleteLabel.Text = "🗑 Xóa tem";
        btnDeleteLabel.SetBounds(152, 60, 120, 34);
        btnDeleteLabel.Variant = ButtonVariant.Danger;
        btnDeleteLabel.ContainerColor = AppTheme.Colors.Surface;
        btnDeleteLabel.Click += BtnDeleteLabel_Click;
        grpLabels.Controls.Add(btnDeleteLabel);

        Label lblGridHint = new()
        {
            Text = "Mỗi dòng là 1 loại tem. Có thể sửa trực tiếp trong bảng rồi bấm Lưu cấu hình.",
            Left = 292,
            Top = 68,
            Width = 700,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        grpLabels.Controls.Add(lblGridHint);

        dgvLabels.Left = 24;
        dgvLabels.Top = 104;
        dgvLabels.Width = 1292;
        dgvLabels.Height = 332;
        dgvLabels.AllowUserToAddRows = false;
        dgvLabels.AllowUserToDeleteRows = false;
        dgvLabels.AutoGenerateColumns = false;
        dgvLabels.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvLabels.MultiSelect = false;
        dgvLabels.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgvLabels.EditMode = DataGridViewEditMode.EditOnEnter;

        BuildLabelColumns();
        AppTheme.StyleGrid(dgvLabels);
        grpLabels.Controls.Add(dgvLabels);
    }

    private void BuildLabelColumns()
    {
        dgvLabels.Columns.Clear();

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Code",
            HeaderText = "Code",
            Width = 90
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Name",
            HeaderText = "Tên tem",
            Width = 150
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "Description",
            HeaderText = "Mô tả",
            Width = 220
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "IconText",
            HeaderText = "Icon",
            Width = 70
        });

        dgvLabels.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "IsEnabled",
            HeaderText = "Bật",
            Width = 55
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "HandlerType",
            HeaderText = "Handler",
            Width = 90
        });

        dgvLabels.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "RequiresEmployeeCode",
            HeaderText = "Cần mã NV",
            Width = 85
        });

        dgvLabels.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "UseBarcodeParser",
            HeaderText = "Parse mã",
            Width = 80
        });

        dgvLabels.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "AppendEmployeeCode",
            HeaderText = "Nối mã NV",
            Width = 80
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "TargetNameColumnIndex",
            HeaderText = "Cột đích",
            Width = 65
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "TemplatePath",
            HeaderText = "Đường dẫn template .btw",
            Width = 280
        });

        dgvLabels.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "DataFilePath",
            HeaderText = "Đường dẫn file data",
            Width = 280
        });
    }
    #endregion

    #region UI - Bottom buttons
    private void BuildBottomButtons()
    {
        btnSave.Text = "💾 Lưu cấu hình";
        btnSave.SetBounds(600, 715, 180, 44);
        btnSave.Variant = ButtonVariant.Primary;
        btnSave.Font = new Font(AppTheme.Fonts.Button.FontFamily, 10.5f, FontStyle.Bold);
        btnSave.Click += BtnSave_Click;
        Controls.Add(btnSave);
    }
    #endregion

    #region Load / Save
    private void LoadConfig()
    {
        AppConfig config = ConfigService.Instance.Config;

        txtBarTender.Text = config.BarTenderExe;
        chkRememberEmployee.Checked = config.RememberEmployee;
        txtDefaultEmployee.Text = config.DefaultEmployee;

        if (!string.IsNullOrWhiteSpace(config.PrinterName))
        {
            if (!cboPrinter.Items.Contains(config.PrinterName))
                cboPrinter.Items.Add(config.PrinterName);

            cboPrinter.SelectedItem = config.PrinterName;
        }
        else if (cboPrinter.Items.Count > 0)
        {
            cboPrinter.SelectedIndex = 0;
        }

        _labels = new BindingList<LabelDefinition>(
            config.Labels
                .Select(x => new LabelDefinition
                {
                    Code = x.Code,
                    Name = x.Name,
                    Description = x.Description,
                    IconText = x.IconText,
                    IsEnabled = x.IsEnabled,
                    TemplatePath = x.TemplatePath,
                    DataFilePath = x.DataFilePath,
                    HandlerType = x.HandlerType,
                    RequiresEmployeeCode = x.RequiresEmployeeCode,
                    UseBarcodeParser = x.UseBarcodeParser,
                    AppendEmployeeCode = x.AppendEmployeeCode,
                    TargetNameColumnIndex = x.TargetNameColumnIndex
                })
                .ToList());

        dgvLabels.DataSource = _labels;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        try
        {
            ValidateBeforeSave();

            AppConfig config = ConfigService.Instance.Config;

            config.BarTenderExe = txtBarTender.Text.Trim();
            config.PrinterName = cboPrinter.SelectedItem as string ?? "";
            config.RememberEmployee = chkRememberEmployee.Checked;
            config.DefaultEmployee = txtDefaultEmployee.Text.Trim();

            config.Labels = _labels
                .Select(x => new LabelDefinition
                {
                    Code = x.Code.Trim(),
                    Name = x.Name.Trim(),
                    Description = x.Description?.Trim() ?? "",
                    IconText = x.IconText?.Trim() ?? "",
                    IsEnabled = x.IsEnabled,
                    TemplatePath = x.TemplatePath?.Trim() ?? "",
                    DataFilePath = x.DataFilePath?.Trim() ?? "",
                    HandlerType = x.HandlerType?.Trim().ToUpper() ?? "GENERIC",
                    RequiresEmployeeCode = x.RequiresEmployeeCode,
                    UseBarcodeParser = x.UseBarcodeParser,
                    AppendEmployeeCode = x.AppendEmployeeCode,
                    TargetNameColumnIndex = x.TargetNameColumnIndex <= 0 ? 5 : x.TargetNameColumnIndex
                })
                .ToList();

            ConfigService.Instance.Save();

            ToastForm.ShowSuccess("Đã lưu cấu hình.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi cấu hình");
        }
    }

    private void ValidateBeforeSave()
    {
        if (string.IsNullOrWhiteSpace(txtBarTender.Text))
            throw new Exception("Vui lòng nhập đường dẫn BarTender.exe.");

        if (cboPrinter.SelectedItem is not string)
            throw new Exception("Vui lòng chọn máy in tem.");

        if (_labels.Count == 0)
            throw new Exception("Phải có ít nhất 1 loại tem.");

        foreach (LabelDefinition label in _labels)
        {
            if (string.IsNullOrWhiteSpace(label.Code))
                throw new Exception("Mỗi loại tem phải có Code.");

            if (string.IsNullOrWhiteSpace(label.Name))
                throw new Exception($"Tem [{label.Code}] chưa có Tên tem.");

            if (string.IsNullOrWhiteSpace(label.HandlerType))
                throw new Exception($"Tem [{label.Code}] chưa có HandlerType.");

            if (string.IsNullOrWhiteSpace(label.TemplatePath))
                throw new Exception($"Tem [{label.Code}] chưa có đường dẫn template.");

            if (string.IsNullOrWhiteSpace(label.DataFilePath))
                throw new Exception($"Tem [{label.Code}] chưa có đường dẫn file data.");
        }

        List<string> duplicateCodes = _labels
            .GroupBy(x => x.Code.Trim().ToUpper())
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateCodes.Count > 0)
            throw new Exception("Code tem bị trùng: " + string.Join(", ", duplicateCodes));
    }
    #endregion

    #region Label actions
    private void BtnAddLabel_Click(object? sender, EventArgs e)
    {
        _labels.Add(new LabelDefinition
        {
            Code = "NEW_LABEL",
            Name = "Tem mới",
            Description = "",
            IconText = "🏷",
            IsEnabled = true,
            TemplatePath = "",
            DataFilePath = "",
            HandlerType = "GENERIC",
            RequiresEmployeeCode = false,
            UseBarcodeParser = false,
            AppendEmployeeCode = false,
            TargetNameColumnIndex = 5
        });
    }

    private void BtnDeleteLabel_Click(object? sender, EventArgs e)
    {
        if (dgvLabels.CurrentRow == null)
            return;

        if (dgvLabels.CurrentRow.DataBoundItem is not LabelDefinition selected)
            return;

        DialogResult rs = MessageBox.Show(
            $"Xóa loại tem [{selected.Code}] - {selected.Name} ?",
            "Xác nhận",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (rs == DialogResult.Yes)
        {
            _labels.Remove(selected);
        }
    }
    #endregion

    #region Helpers
    private void BrowseFile(TextBox target, string filter)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = filter
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }
    #endregion
}