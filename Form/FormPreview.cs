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

    private readonly string _sourceExcelFile;
    private readonly bool _printFull;
    private readonly bool _printBarcode;
    private readonly string _employeeCode;

    public FormPreview(
        string sourceExcelFile,
        bool printFull,
        bool printBarcode,
        string employeeCode)
    {
        _sourceExcelFile = sourceExcelFile;
        _printFull = printFull;
        _printBarcode = printBarcode;
        _employeeCode = employeeCode;

        Text = "Xem trước dữ liệu in tem";
        Width = 1200;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadPreview();
    }

    private void BuildUi()
    {
        lblSummary.Left = 20;
        lblSummary.Top = 15;
        lblSummary.Width = 1000;
        lblSummary.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        Controls.Add(lblSummary);

        dgvPreview.Left = 20;
        dgvPreview.Top = 45;
        dgvPreview.Width = 1140;
        dgvPreview.Height = 500;
        dgvPreview.ReadOnly = true;
        dgvPreview.AllowUserToAddRows = false;
        dgvPreview.AllowUserToDeleteRows = false;
        dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        Controls.Add(dgvPreview);

        btnPrint.Text = "IN TEM";
        btnPrint.Left = 760;
        btnPrint.Top = 565;
        btnPrint.Width = 180;
        btnPrint.Height = 40;
        btnPrint.Click += BtnPrint_Click;
        Controls.Add(btnPrint);

        btnClose.Text = "Đóng";
        btnClose.Left = 960;
        btnClose.Top = 565;
        btnClose.Width = 120;
        btnClose.Height = 40;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }

    private void LoadPreview()
    {
        List<PreviewRow> rows = _labelService.BuildPreview(
            _sourceExcelFile,
            _printFull,
            _printBarcode,
            _employeeCode);

        dgvPreview.DataSource = rows;

        int totalProducts = rows.Count;
        double totalLabels = rows.Sum(x => x.Quantity);

        lblSummary.Text =
            $"Sản phẩm: {totalProducts} | Tổng tem theo số lượng: {totalLabels} | " +
            $"Tem đầy đủ: {(_printFull ? "Có" : "Không")} | " +
            $"Tem mã vạch: {(_printBarcode ? "Có" : "Không")} | " +
            $"Mã NV: {_employeeCode}";
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
    {
        try
        {
            int total = _labelService.Print(
                _sourceExcelFile,
                _printFull,
                _printBarcode,
                _employeeCode);

            MessageBox.Show($"Đã xử lý {total} sản phẩm.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi");
        }
    }
}