using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;
public class FormPreview : Form
{
    private readonly SmoothDataGridView dgvPreview = new();
    private readonly Label lblSummary = new();
    private readonly RoundedButton btnPrint = new();
    private readonly RoundedButton btnClose = new();

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
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        LoadPreview();
    }

    private void BuildUi()
    {
        lblSummary.Left = 20;
        lblSummary.Top = 18;
        lblSummary.Width = 1190;
        lblSummary.Height = 26;
        lblSummary.Font = AppTheme.Fonts.BodyBold;
        lblSummary.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblSummary);

        dgvPreview.Left = 20;
        dgvPreview.Top = 54;
        dgvPreview.Width = 1190;
        dgvPreview.Height = 540;
        dgvPreview.ReadOnly = true;
        dgvPreview.AllowUserToAddRows = false;
        dgvPreview.AllowUserToDeleteRows = false;
        dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvPreview.MultiSelect = false;
        dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AppTheme.StyleGrid(dgvPreview);
        Controls.Add(dgvPreview);

        btnPrint.Text = "🖨 IN TEM";
        btnPrint.Left = 860;
        btnPrint.Top = 610;
        btnPrint.Width = 160;
        btnPrint.Height = 42;
        btnPrint.Variant = ButtonVariant.Primary;
        btnPrint.Font = new Font(AppTheme.Fonts.Button.FontFamily, 10.5f, FontStyle.Bold);
        btnPrint.Click += BtnPrint_Click;
        Controls.Add(btnPrint);

        btnClose.Text = "Đóng";
        btnClose.Left = 1040;
        btnClose.Top = 610;
        btnClose.Width = 120;
        btnClose.Height = 42;
        btnClose.Variant = ButtonVariant.Secondary;
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
        var cProductCode = dgvPreview.Columns["ProductCode"];
        if (cProductCode != null)
            cProductCode.HeaderText = "Mã hàng";

        var cProductName = dgvPreview.Columns["ProductName"];
        if (cProductName != null)
            cProductName.HeaderText = "Tên hàng";

        var cProductNameAttr = dgvPreview.Columns["ProductNameWithAttr"];
        if (cProductNameAttr != null)
            cProductNameAttr.HeaderText = "Tên hàng (thuộc tính)";

        var cParsedBarcode = dgvPreview.Columns["ParsedBarcodeCode"];
        if (cParsedBarcode != null)
            cParsedBarcode.HeaderText = "Mã parser";

        var cFinalBarcode = dgvPreview.Columns["FinalBarcodeCode"];
        if (cFinalBarcode != null)
            cFinalBarcode.HeaderText = "Mã in cuối";

        var cQuantity = dgvPreview.Columns["Quantity"];
        if (cQuantity != null)
            cQuantity.HeaderText = "Số lượng";

        var cPrice = dgvPreview.Columns["Price"];
        if (cPrice != null)
            cPrice.HeaderText = "Giá";

        var cIsFull = dgvPreview.Columns["IsFullLabel"];
        if (cIsFull != null)
            cIsFull.Visible = false;

        var cIsBarcode = dgvPreview.Columns["IsBarcodeLabel"];
        if (cIsBarcode != null)
            cIsBarcode.Visible = false;

        // Width tương đối cho dễ nhìn
        if (cProductCode != null)
            cProductCode.FillWeight = 90;

        if (cProductName != null)
            cProductName.FillWeight = 150;

        if (cProductNameAttr != null)
            cProductNameAttr.FillWeight = 220;

        if (cParsedBarcode != null)
            cParsedBarcode.FillWeight = 110;

        if (cFinalBarcode != null)
            cFinalBarcode.FillWeight = 140;

        if (cQuantity != null)
            cQuantity.FillWeight = 70;

        if (cPrice != null)
            cPrice.FillWeight = 90;

        dgvPreview.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        dgvPreview.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
    }

    private async void BtnPrint_Click(object? sender, EventArgs e)
    {
        // In số lượng lớn có thể mất nhiều phút (phải chờ máy in xử lý
        // xong từng mã trước khi in mã kế tiếp — xem BarTenderService).
        // Chạy trên UI thread sẽ làm app "Not Responding", khiến người
        // dùng tưởng treo rồi tắt app giữa chừng → mất tem đã in dở.
        btnPrint.Enabled = false;
        btnClose.Enabled = false;
        string originalText = btnPrint.Text;
        btnPrint.Text = "Đang in...";
        Cursor = Cursors.WaitCursor;

        try
        {
            int total = await Task.Run(() => _labelService.Print(
                _sourceExcelFile,
                _labelCode,
                _employeeCode));

            ToastForm.ShowSuccess($"Đã xử lý {total} sản phẩm.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi in tem");
        }
        finally
        {
            Cursor = Cursors.Default;
            btnPrint.Text = originalText;
            btnPrint.Enabled = true;
            btnClose.Enabled = true;
        }
    }
}
