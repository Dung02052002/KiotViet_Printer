using System.ComponentModel;
using System.Drawing.Printing;
using KiotVietLabelPrinter.Infrastructure.BarTender;
using KiotVietLabelPrinter.Infrastructure.Diagnostics;
using KiotVietLabelPrinter.Infrastructure.Printing;
using KiotVietLabelPrinter.Infrastructure.Storage;
using KiotVietLabelPrinter.Models.Labels;
using KiotVietLabelPrinter.Models.Warehouse;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.UI.Controls;
using KiotVietLabelPrinter.UI.Theme;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormConfig : Form
{
    private readonly RoundedTextBox txtBarTender = new();
    private readonly ComboBox cboPrinter = new();
    private readonly ComboBox cboWarehousePrinter = new();

    // Máy in Phiếu xuất kho nằm ở file thiết lập riêng của chức năng đó
    // (warehouse-export.json), không phải config.json.
    private readonly WarehouseSettingsStore _warehouseStore = new();
    private string _savedWarehousePrinter = "";
    private readonly ToggleSwitch chkRememberEmployee = new();
    private readonly RoundedTextBox txtDefaultEmployee = new();

    private readonly RoundedButton btnBrowseBarTender = new();
    private readonly Label lblCapabilityStatus = new();
    private readonly RoundedButton btnResetCapability = new();
    private readonly RoundedButton btnReloadPrinters = new();
    private readonly Label lblWarehousePrinterHint = new();
    private bool _loadingPrinters;
    private readonly RoundedButton btnSave = new();
    private readonly RoundedButton btnAddLabel = new();
    private readonly RoundedButton btnDeleteLabel = new();

    private readonly SmoothDataGridView dgvLabels = new();

    private BindingList<LabelDefinition> _labels = new();

    public FormConfig()
    {
        Text = $"Cấu hình phần mềm — {AppInfo.ShortLabel}";
        Width = 1280;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(1050, 680);
        MaximizeBox = true;
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
            Width = ClientSize.Width - 40,
            Height = 290,
            CornerRadius = 18,
            FillColor = AppTheme.Colors.SurfaceElevated,
            BorderColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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
            Width = grpGeneral.Width - 48,
            Height = 1,
            BackColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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

        txtBarTender.SetBounds(160, 62, grpGeneral.Width - 248, 38);
        txtBarTender.Font = AppTheme.Fonts.Body;
        txtBarTender.ContainerColor = AppTheme.Colors.Surface;
        txtBarTender.PlaceholderText = "Đường dẫn đến BarTender.exe";
        txtBarTender.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpGeneral.Controls.Add(txtBarTender);

        btnBrowseBarTender.Text = "...";
        btnBrowseBarTender.SetBounds(grpGeneral.Width - 74, 62, 50, 38);
        btnBrowseBarTender.Variant = ButtonVariant.Outline;
        btnBrowseBarTender.ContainerColor = AppTheme.Colors.Surface;
        btnBrowseBarTender.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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

        cboPrinter.SetBounds(160, 108, 500, 34);
        cboPrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        AppTheme.StyleComboBox(cboPrinter);
        grpGeneral.Controls.Add(cboPrinter);

        Label lblPrinterHint = new()
        {
            Text = "Cố định máy in để tránh bị đổi sang máy in khác khi in.",
            Left = 676,
            Top = 112,
            Width = grpGeneral.Width - 700,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        grpGeneral.Controls.Add(lblPrinterHint);

        Label lblWarehousePrinter = new()
        {
            Text = "Máy in phiếu XK",
            Left = 24,
            Top = 158,
            Width = 130,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpGeneral.Controls.Add(lblWarehousePrinter);

        cboWarehousePrinter.SetBounds(160, 154, 500, 34);
        cboWarehousePrinter.DropDownStyle = ComboBoxStyle.DropDownList;
        AppTheme.StyleComboBox(cboWarehousePrinter);
        grpGeneral.Controls.Add(cboWarehousePrinter);

        // Đọc lại danh sách máy in (vừa cắm / bật lại máy in, hoặc lần đọc
        // trước Windows không phản hồi) — cập nhật cả hai ô máy in.
        btnReloadPrinters.Text = "Tải lại";
        btnReloadPrinters.SetBounds(676, 154, 100, 34);
        btnReloadPrinters.Variant = ButtonVariant.Outline;
        btnReloadPrinters.ContainerColor = AppTheme.Colors.Surface;
        btnReloadPrinters.Click += async (_, _) => await LoadPrintersAsync();
        grpGeneral.Controls.Add(btnReloadPrinters);

        lblWarehousePrinterHint.Text = WarehousePrinterHint;
        lblWarehousePrinterHint.Left = 792;
        lblWarehousePrinterHint.Top = 152;
        lblWarehousePrinterHint.Width = grpGeneral.Width - 816;
        lblWarehousePrinterHint.Height = 40;
        lblWarehousePrinterHint.Font = AppTheme.Fonts.Hint;
        lblWarehousePrinterHint.ForeColor = AppTheme.Colors.TextMuted;
        lblWarehousePrinterHint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpGeneral.Controls.Add(lblWarehousePrinterHint);

        chkRememberEmployee.Text = "Ghi nhớ mã nhân viên mặc định";
        chkRememberEmployee.Left = 24;
        chkRememberEmployee.Top = 204;
        chkRememberEmployee.Width = 280;
        chkRememberEmployee.Font = AppTheme.Fonts.Body;
        chkRememberEmployee.ContainerColor = AppTheme.Colors.Surface;
        chkRememberEmployee.CheckedChanged += (_, _) =>
            txtDefaultEmployee.Enabled = chkRememberEmployee.Checked;
        grpGeneral.Controls.Add(chkRememberEmployee);

        Label lblDefaultEmployee = new()
        {
            Text = "Mã NV mặc định",
            Left = 332,
            Top = 206,
            Width = 130,
            Font = AppTheme.Fonts.Body,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        grpGeneral.Controls.Add(lblDefaultEmployee);

        txtDefaultEmployee.SetBounds(468, 197, 220, 38);
        txtDefaultEmployee.Font = AppTheme.Fonts.Body;
        txtDefaultEmployee.ContainerColor = AppTheme.Colors.Surface;
        txtDefaultEmployee.PlaceholderText = "Ví dụ: H020";
        grpGeneral.Controls.Add(txtDefaultEmployee);

        Label lblHint = new()
        {
            Text = "Ví dụ: H020 hoặc H020-K026",
            Left = 700,
            Top = 206,
            Width = 260,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted
        };
        grpGeneral.Controls.Add(lblHint);

        lblCapabilityStatus.Left = 24;
        lblCapabilityStatus.Top = 248;
        lblCapabilityStatus.Width = grpGeneral.Width - 210;
        lblCapabilityStatus.Height = 32;
        lblCapabilityStatus.Font = AppTheme.Fonts.Hint;
        lblCapabilityStatus.ForeColor = AppTheme.Colors.TextMuted;
        lblCapabilityStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        grpGeneral.Controls.Add(lblCapabilityStatus);

        btnResetCapability.Text = "Kiểm tra lại";
        btnResetCapability.SetBounds(grpGeneral.Width - 174, 242, 150, 32);
        btnResetCapability.Variant = ButtonVariant.Outline;
        btnResetCapability.ContainerColor = AppTheme.Colors.Surface;
        btnResetCapability.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnResetCapability.Click += (_, _) =>
        {
            BarTenderCapabilityService.Instance.Reset();
            RefreshCapabilityStatus();
            ToastForm.ShowSuccess("Đã đặt lại — lần in tiếp theo sẽ tự kiểm tra lại BarTender.");
        };
        grpGeneral.Controls.Add(btnResetCapability);
    }

    private void RefreshCapabilityStatus()
    {
        string exePath = txtBarTender.Text.Trim();
        string? version = BarTenderCapabilityService.TryGetVersion(exePath);
        BarTenderCapabilityService cap = BarTenderCapabilityService.Instance;

        string versionText = string.IsNullOrWhiteSpace(version)
            ? "(chưa xác định được version)"
            : $"version {version}";

        cap.EnsureProbed(exePath);

        string statusText = cap.XmlScriptSupported switch
        {
            true => "hỗ trợ Automation nâng cao (XMLScript) — Tem kính in được.",
            false => "KHÔNG hỗ trợ Automation nâng cao. Tem đầy đủ / Tem mã vạch " +
                     "vẫn in bình thường (command line chuẩn); Tem kính cần nâng " +
                     "cấp BarTender lên Enterprise Automation.",
            _ => "chưa xác định — Tem đầy đủ / Tem mã vạch in bình thường; Tem kính " +
                 "sẽ tự kiểm tra ở lần in đầu."
        };

        string editionText = string.IsNullOrWhiteSpace(cap.DetectedEdition)
            ? ""
            : $" · Edition: {cap.DetectedEdition}";

        lblCapabilityStatus.Text = $"BarTender {versionText}{editionText} — {statusText}";
    }

    // Đọc danh sách máy in ở luồng nền sau khi form đã hiện (SpoolerQuery):
    // spooler treo thì form vẫn dùng được, máy in đã lưu (LoadConfig thêm sẵn
    // vào danh sách) vẫn được giữ nguyên.
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await LoadPrintersAsync();
    }

    private const string WarehousePrinterHint = "Riêng cho Phiếu xuất kho, không ảnh hưởng máy in tem.";

    private async Task LoadPrintersAsync()
    {
        if (_loadingPrinters)
            return;

        _loadingPrinters = true;
        btnReloadPrinters.Enabled = false;
        btnReloadPrinters.Text = "Đang tải...";
        try
        {
            List<string> printers = await SpoolerQuery.RunAsync(() => PrinterSettings.InstalledPrinters.Cast<string>().ToList());
            if (IsDisposed)
                return;

            FillPrinters(cboPrinter, printers, selectFirstWhenEmpty: true);
            // Máy in phiếu không bắt buộc: chưa chọn thì để trống, không tự chọn.
            FillPrinters(cboWarehousePrinter, printers, selectFirstWhenEmpty: false);
            SetPrinterHint(WarehousePrinterHint, AppTheme.Colors.TextMuted);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception)
        {
            PrintDiagnosticsLog.Write($"CONFIG load printers failed: {ex}");
            if (!IsDisposed)
                SetPrinterHint(ex is TimeoutException
                        ? "Windows không phản hồi (máy in lỗi?) — bật lại máy in rồi bấm Tải lại."
                        : $"Không đọc được danh sách máy in: {ex.Message}",
                    AppTheme.Colors.Danger);
        }
        finally
        {
            _loadingPrinters = false;
            if (!IsDisposed)
            {
                btnReloadPrinters.Text = "Tải lại";
                btnReloadPrinters.Enabled = true;
            }
        }
    }

    private void SetPrinterHint(string text, Color color)
    {
        lblWarehousePrinterHint.Text = text;
        lblWarehousePrinterHint.ForeColor = color;
    }

    private static void FillPrinters(ComboBox combo, List<string> printers, bool selectFirstWhenEmpty)
    {
        string? selected = combo.SelectedItem as string;
        combo.BeginUpdate();
        try
        {
            combo.Items.Clear();
            foreach (string printer in printers)
                combo.Items.Add(printer);

            if (selected != null && !combo.Items.Contains(selected))
                combo.Items.Add(selected);

            if (selected != null)
                combo.SelectedItem = selected;
            else if (selectFirstWhenEmpty && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }
        finally
        {
            combo.EndUpdate();
        }
    }
    #endregion

    #region UI - Label grid
    private void BuildLabelGridSection()
    {
        RoundedPanel grpLabels = new()
        {
            Left = 20,
            Top = 326,
            Width = ClientSize.Width - 40,
            Height = ClientSize.Height - 406,
            CornerRadius = 18,
            FillColor = AppTheme.Colors.SurfaceElevated,
            BorderColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
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
            Width = grpLabels.Width - 48,
            Height = 1,
            BackColor = AppTheme.Colors.Border,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        grpLabels.Controls.Add(divider);

        btnAddLabel.Text = "Thêm tem";
        btnAddLabel.Icon = IconGlyphs.Kind.Plus;
        btnAddLabel.SetBounds(24, 60, 130, 38);
        btnAddLabel.Variant = ButtonVariant.Outline;
        btnAddLabel.ContainerColor = AppTheme.Colors.Surface;
        btnAddLabel.Click += BtnAddLabel_Click;
        grpLabels.Controls.Add(btnAddLabel);

        btnDeleteLabel.Text = "Xóa tem";
        btnDeleteLabel.Icon = IconGlyphs.Kind.Trash;
        btnDeleteLabel.SetBounds(162, 60, 130, 38);
        btnDeleteLabel.Variant = ButtonVariant.Danger;
        btnDeleteLabel.ContainerColor = AppTheme.Colors.Surface;
        btnDeleteLabel.Click += BtnDeleteLabel_Click;
        grpLabels.Controls.Add(btnDeleteLabel);

        Label lblGridHint = new()
        {
            Text = "Mỗi dòng là 1 loại tem. Có thể sửa trực tiếp trong bảng rồi bấm Lưu cấu hình.",
            Left = 312,
            Top = 68,
            Width = grpLabels.Width - 336,
            Font = AppTheme.Fonts.Hint,
            ForeColor = AppTheme.Colors.TextMuted,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        grpLabels.Controls.Add(lblGridHint);

        dgvLabels.Left = 24;
        dgvLabels.Top = 108;
        dgvLabels.Width = grpLabels.Width - 48;
        dgvLabels.Height = grpLabels.Height - 132;
        dgvLabels.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        dgvLabels.AllowUserToAddRows = false;
        dgvLabels.AllowUserToDeleteRows = false;
        dgvLabels.AutoGenerateColumns = false;
        dgvLabels.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvLabels.MultiSelect = false;
        dgvLabels.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        dgvLabels.EditMode = DataGridViewEditMode.EditOnEnter;
        dgvLabels.DataBindingComplete += (_, _) =>
        {
            dgvLabels.ClearSelection();
            dgvLabels.CurrentCell = null;
        };

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
        btnSave.Text = "Lưu cấu hình";
        btnSave.Icon = IconGlyphs.Kind.Check;
        btnSave.SetBounds(ClientSize.Width - 200, ClientSize.Height - 62, 180, 44);
        btnSave.Variant = ButtonVariant.Primary;
        btnSave.ContainerColor = AppTheme.Colors.Background;
        btnSave.Font = AppTheme.Fonts.ButtonLarge;
        btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnSave.Click += BtnSave_Click;
        Controls.Add(btnSave);
    }
    #endregion

    #region Load / Save
    private void LoadConfig()
    {
        AppConfig config = ConfigService.Instance.Config;

        txtBarTender.Text = config.BarTenderExe;
        RefreshCapabilityStatus();
        chkRememberEmployee.Checked = config.RememberEmployee;
        txtDefaultEmployee.Text = config.DefaultEmployee;
        txtDefaultEmployee.Enabled = chkRememberEmployee.Checked;

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

        _savedWarehousePrinter = _warehouseStore.Load().PrinterName;
        if (!string.IsNullOrWhiteSpace(_savedWarehousePrinter))
        {
            cboWarehousePrinter.Items.Add(_savedWarehousePrinter);
            cboWarehousePrinter.SelectedItem = _savedWarehousePrinter;
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
            SaveWarehousePrinter();

            ToastForm.ShowSuccess("Đã lưu cấu hình.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi cấu hình");
        }
    }

    // Chỉ ghi khi đổi máy in, và chỉ đổi đúng trường máy in — các thiết lập
    // khác của Phiếu xuất kho (font, khổ giấy, thư mục...) giữ nguyên.
    private void SaveWarehousePrinter()
    {
        string printer = cboWarehousePrinter.SelectedItem as string ?? "";
        if (string.Equals(printer, _savedWarehousePrinter, StringComparison.Ordinal))
            return;

        WarehouseExportSettings settings = _warehouseStore.Load();
        settings.PrinterName = printer;
        _warehouseStore.Save(settings);
        _savedWarehousePrinter = printer;
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
    private void BrowseFile(RoundedTextBox target, string filter)
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
