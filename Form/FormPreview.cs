using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;
public class FormPreview : Form
{
    private readonly DataGridView dgvPreview = new();
    private readonly Label lblSummary = new();
    private readonly Button btnPrint = new();
    private readonly Button btnClose = new();

    private readonly LabelService _labelService = new();
    private readonly LabelCatalogService _catalogService = new();

    private readonly string _sourceExcelFile;
    private readonly string _labelCode;
    private readonly string _employeeCode;

    public FormPreview(
        string sourceExcelFile,
        string labelCode,
        string employeeCode)
    {
        _sourceExcelFile = sourceExcelFile;
        _labelCode = labelCode;
        _employeeCode = employeeCode;

        Text = "Xem trước dữ liệu in tem";
        Width = 1250;
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        BuildUi();
        LoadPreview();
    }

    private void BuildUi()
    {
        lblSummary.Left = 20;
        lblSummary.Top = 15;
        lblSummary.Width = 1150;
        lblSummary.Height = 25;
        lblSummary.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Controls.Add(lblSummary);

        dgvPreview.Left = 20;
        dgvPreview.Top = 50;
        dgvPreview.Width = 1190;
        dgvPreview.Height = 540;
        dgvPreview.ReadOnly = true;
        dgvPreview.AllowUserToAddRows = false;
        dgvPreview.AllowUserToDeleteRows = false;
        dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvPreview.MultiSelect = false;
        dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvPreview.RowHeadersVisible = false;
        Controls.Add(dgvPreview);

        btnPrint.Text = "IN TEM";
        btnPrint.Left = 860;
        btnPrint.Top = 610;
        btnPrint.Width = 160;
        btnPrint.Height = 40;
        btnPrint.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnPrint.Click += BtnPrint_Click;
        Controls.Add(btnPrint);

        btnClose.Text = "Đóng";
        btnClose.Left = 1040;
        btnClose.Top = 610;
        btnClose.Width = 120;
        btnClose.Height = 40;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }

    private void LoadPreview()
    {
        try
        {
            List<PreviewRow> rows = _labelService.BuildPreview(
                _sourceExcelFile,
                _labelCode,
                _employeeCode);

            dgvPreview.DataSource = null;
            dgvPreview.DataSource = rows;

            FormatGrid();

            int totalProducts = rows.Count;
            double totalLabels = rows.Sum(x => x.Quantity);

            string labelName = _labelCode;
            try
            {
                labelName = _catalogService.GetByCode(_labelCode).Name;
            }
            catch
            {
                // fallback nếu chưa tìm thấy cấu hình
            }

            lblSummary.Text =
                $"Loại tem: {labelName} ({_labelCode})   |   " +
                $"Sản phẩm: {totalProducts}   |   " +
                $"Tổng tem theo số lượng: {totalLabels}   |   " +
                $"Mã NV: {(_employeeCode == "" ? "(trống)" : _employeeCode)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi tải preview");
            Close();
        }
    }

    private void FormatGrid()
    {
        if (dgvPreview.Columns["ProductCode"] != null)
            dgvPreview.Columns["ProductCode"].HeaderText = "Mã hàng";

        if (dgvPreview.Columns["ProductName"] != null)
            dgvPreview.Columns["ProductName"].HeaderText = "Tên hàng";

        if (dgvPreview.Columns["ProductNameWithAttr"] != null)
            dgvPreview.Columns["ProductNameWithAttr"].HeaderText = "Tên hàng (thuộc tính)";

        if (dgvPreview.Columns["ParsedBarcodeCode"] != null)
            dgvPreview.Columns["ParsedBarcodeCode"].HeaderText = "Mã parser";

        if (dgvPreview.Columns["FinalBarcodeCode"] != null)
            dgvPreview.Columns["FinalBarcodeCode"].HeaderText = "Mã in cuối";

        if (dgvPreview.Columns["Quantity"] != null)
            dgvPreview.Columns["Quantity"].HeaderText = "Số lượng";

        if (dgvPreview.Columns["Price"] != null)
            dgvPreview.Columns["Price"].HeaderText = "Giá";

        if (dgvPreview.Columns["IsFullLabel"] != null)
            dgvPreview.Columns["IsFullLabel"].Visible = false;

        if (dgvPreview.Columns["IsBarcodeLabel"] != null)
            dgvPreview.Columns["IsBarcodeLabel"].Visible = false;

        // Width tương đối cho dễ nhìn
        if (dgvPreview.Columns["ProductCode"] != null)
            dgvPreview.Columns["ProductCode"].FillWeight = 90;

        if (dgvPreview.Columns["ProductName"] != null)
            dgvPreview.Columns["ProductName"].FillWeight = 150;

        if (dgvPreview.Columns["ProductNameWithAttr"] != null)
            dgvPreview.Columns["ProductNameWithAttr"].FillWeight = 220;

        if (dgvPreview.Columns["ParsedBarcodeCode"] != null)
            dgvPreview.Columns["ParsedBarcodeCode"].FillWeight = 110;

        if (dgvPreview.Columns["FinalBarcodeCode"] != null)
            dgvPreview.Columns["FinalBarcodeCode"].FillWeight = 140;

        if (dgvPreview.Columns["Quantity"] != null)
            dgvPreview.Columns["Quantity"].FillWeight = 70;

        if (dgvPreview.Columns["Price"] != null)
            dgvPreview.Columns["Price"].FillWeight = 90;

        dgvPreview.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        dgvPreview.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        try
        {
            int total = _labelService.Print(
                _sourceExcelFile,
                _labelCode,
                _employeeCode);

            MessageBox.Show($"Đã xử lý {total} sản phẩm.", "Thành công");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi in tem");
        }
    }
}