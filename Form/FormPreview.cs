using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;
public class FormPreview : Form
{
    private readonly SmoothDataGridView dgvPreview = new();
    private readonly RoundedPanel pnlGridCard = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSummary = new();
    private readonly Label lblFooterHint = new();
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
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 560);
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        Shown += async (_, _) => await LoadPreviewAsync();
    }

    private void BuildUi()
    {
        lblTitle.Text = "Xem trước dữ liệu";
        lblTitle.SetBounds(24, 16, 520, 32);
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblTitle);

        lblSummary.Text = "Đang chuẩn bị dữ liệu xem trước...";
        lblSummary.SetBounds(26, 52, ClientSize.Width - 52, 22);
        lblSummary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblSummary.Font = AppTheme.Fonts.Subtitle;
        lblSummary.ForeColor = AppTheme.Colors.TextSecondary;
        Controls.Add(lblSummary);

        pnlGridCard.SetBounds(20, 90, ClientSize.Width - 40, ClientSize.Height - 166);
        pnlGridCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlGridCard.CornerRadius = 16;
        pnlGridCard.FillColor = AppTheme.Colors.SurfaceElevated;
        pnlGridCard.BorderColor = AppTheme.Colors.Border;
        pnlGridCard.BorderThickness = 1;
        pnlGridCard.ContainerColor = AppTheme.Colors.Background;
        Controls.Add(pnlGridCard);

        dgvPreview.SetBounds(1, 1, pnlGridCard.Width - 2, pnlGridCard.Height - 2);
        dgvPreview.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        dgvPreview.ReadOnly = true;
        dgvPreview.AllowUserToAddRows = false;
        dgvPreview.AllowUserToDeleteRows = false;
        dgvPreview.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvPreview.MultiSelect = false;
        dgvPreview.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AppTheme.StyleGrid(dgvPreview);
        pnlGridCard.Controls.Add(dgvPreview);

        lblFooterHint.Text = "Kiểm tra tên hàng, mã và số lượng trước khi gửi lệnh in.";
        lblFooterHint.SetBounds(24, ClientSize.Height - 54, 620, 22);
        lblFooterHint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblFooterHint.Font = AppTheme.Fonts.Hint;
        lblFooterHint.ForeColor = AppTheme.Colors.TextMuted;
        Controls.Add(lblFooterHint);

        btnPrint.Text = "🖨 IN TEM";
        btnPrint.SetBounds(ClientSize.Width - 316, ClientSize.Height - 62, 164, 44);
        btnPrint.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnPrint.Variant = ButtonVariant.Primary;
        btnPrint.ContainerColor = AppTheme.Colors.Background;
        btnPrint.Font = new Font(AppTheme.Fonts.Button.FontFamily, 10.5f, FontStyle.Bold);
        btnPrint.Enabled = false;
        btnPrint.Click += BtnPrint_Click;
        Controls.Add(btnPrint);

        btnClose.Text = "Đóng";
        btnClose.SetBounds(ClientSize.Width - 140, ClientSize.Height - 62, 120, 44);
        btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnClose.Variant = ButtonVariant.Secondary;
        btnClose.ContainerColor = AppTheme.Colors.Background;
        btnClose.DialogResult = DialogResult.Cancel;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        CancelButton = btnClose;
    }

    private async Task LoadPreviewAsync()
    {
        Cursor = Cursors.WaitCursor;
        btnPrint.Enabled = false;

        try
        {
            List<PreviewRow> rows = await Task.Run(() => _labelService.BuildPreview(
                    _sourceExcelFile,
                    _labelCode,
                    _employeeCode));

            if (IsDisposed || Disposing || !Visible)
                return;

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

            btnPrint.Enabled = true;
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing && Visible)
            {
                MessageBox.Show(ex.Message, "Lỗi tải preview");
                Close();
            }
        }
        finally
        {
            if (!IsDisposed && !Disposing)
                Cursor = Cursors.Default;
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
