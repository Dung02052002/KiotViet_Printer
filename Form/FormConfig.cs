using System.ComponentModel;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormConfig : Form
{
    private readonly TextBox txtBarTender = new();
    private readonly CheckBox chkRememberEmployee = new();
    private readonly TextBox txtDefaultEmployee = new();

    private readonly Button btnBrowseBarTender = new();
    private readonly Button btnSave = new();
    private readonly Button btnAddLabel = new();
    private readonly Button btnDeleteLabel = new();

    private readonly DataGridView dgvLabels = new();

    private BindingList<LabelDefinition> _labels = new();

    public FormConfig()
    {
        Text = "Cấu hình phần mềm";
        Width = 1400;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

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
        GroupBox grpGeneral = new()
        {
            Text = "Cấu hình chung",
            Left = 20,
            Top = 20,
            Width = 1340,
            Height = 150
        };
        Controls.Add(grpGeneral);

        Label lblBarTender = new()
        {
            Text = "BarTender.exe",
            Left = 20,
            Top = 35,
            Width = 120
        };
        grpGeneral.Controls.Add(lblBarTender);

        txtBarTender.SetBounds(150, 30, 1000, 28);
        grpGeneral.Controls.Add(txtBarTender);

        btnBrowseBarTender.Text = "...";
        btnBrowseBarTender.SetBounds(1170, 30, 50, 28);
        btnBrowseBarTender.Click += (_, _) => BrowseFile(txtBarTender, "Executable|*.exe");
        grpGeneral.Controls.Add(btnBrowseBarTender);

        chkRememberEmployee.Text = "Ghi nhớ mã nhân viên mặc định";
        chkRememberEmployee.Left = 20;
        chkRememberEmployee.Top = 80;
        chkRememberEmployee.Width = 260;
        grpGeneral.Controls.Add(chkRememberEmployee);

        Label lblDefaultEmployee = new()
        {
            Text = "Mã NV mặc định",
            Left = 320,
            Top = 82,
            Width = 120
        };
        grpGeneral.Controls.Add(lblDefaultEmployee);

        txtDefaultEmployee.SetBounds(450, 78, 220, 28);
        grpGeneral.Controls.Add(txtDefaultEmployee);

        Label lblHint = new()
        {
            Text = "Ví dụ: H020 hoặc H020-K026",
            Left = 690,
            Top = 82,
            Width = 250,
            ForeColor = Color.DimGray
        };
        grpGeneral.Controls.Add(lblHint);
    }
    #endregion

    #region UI - Label grid
    private void BuildLabelGridSection()
    {
        GroupBox grpLabels = new()
        {
            Text = "Danh sách loại tem",
            Left = 20,
            Top = 190,
            Width = 1340,
            Height = 470
        };
        Controls.Add(grpLabels);

        btnAddLabel.Text = "Thêm tem";
        btnAddLabel.SetBounds(20, 30, 110, 32);
        btnAddLabel.Click += BtnAddLabel_Click;
        grpLabels.Controls.Add(btnAddLabel);

        btnDeleteLabel.Text = "Xóa tem";
        btnDeleteLabel.SetBounds(145, 30, 110, 32);
        btnDeleteLabel.Click += BtnDeleteLabel_Click;
        grpLabels.Controls.Add(btnDeleteLabel);

        Label lblGridHint = new()
        {
            Text = "Mỗi dòng là 1 loại tem. Có thể sửa trực tiếp trong bảng rồi bấm Lưu cấu hình.",
            Left = 280,
            Top = 37,
            Width = 700,
            ForeColor = Color.DimGray
        };
        grpLabels.Controls.Add(lblGridHint);

        dgvLabels.Left = 20;
        dgvLabels.Top = 80;
        dgvLabels.Width = 1290;
        dgvLabels.Height = 360;
        dgvLabels.AllowUserToAddRows = false;
        dgvLabels.AllowUserToDeleteRows = false;
        dgvLabels.AutoGenerateColumns = false;
        dgvLabels.RowHeadersVisible = false;
        dgvLabels.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvLabels.MultiSelect = false;
        dgvLabels.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgvLabels.EditMode = DataGridViewEditMode.EditOnEnter;

        BuildLabelColumns();
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
        btnSave.Text = "Lưu cấu hình";
        btnSave.SetBounds(590, 675, 180, 42);
        btnSave.Font = new Font("Segoe UI", 10, FontStyle.Bold);
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