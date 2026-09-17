using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

// Modal "Sửa tên hàng" - mở từ FormMain khi bấm nút "Sửa tên hàng" ở màn hình
// Tem đầy đủ. Chỉ chỉnh tên trong bộ nhớ, không đụng tới file Excel nguồn (xem
// ExcelService.CopyToBarTenderData). Độc lập hoàn toàn với FormEditPrices /
// _productPriceOverrides - không đọc, không ghi state của chức năng sửa giá.
public class FormEditNames : Form
{
    private readonly Label lblTitle = new();
    private readonly Label lblSummary = new();
    private readonly RoundedTextBox txtSearch = new();
    private readonly RoundedPanel pnlGridCard = new();
    private readonly SmoothDataGridView dgv = new();
    private readonly Label lblFooterHint = new();
    private readonly RoundedButton btnApply = new();
    private readonly RoundedButton btnCancel = new();

    private readonly List<NameEditRow> _allRows;

    public Dictionary<string, string> ResultOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FormEditNames(List<ProductRow> products, IReadOnlyDictionary<string, string> existingOverrides)
    {
        Text = "Sửa tên hàng";
        Width = 980;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 480);
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        _allRows = products.Select(p =>
        {
            string originalName = string.IsNullOrWhiteSpace(p.ProductNameWithAttr) ? p.ProductName : p.ProductNameWithAttr;

            string newNameText = !string.IsNullOrWhiteSpace(p.ProductCode) &&
                existingOverrides.TryGetValue(p.ProductCode, out string? overridden)
                    ? overridden
                    : "";

            return new NameEditRow
            {
                ProductCode = p.ProductCode,
                ProductName = originalName,
                NewNameText = newNameText
            };
        }).ToList();

        BuildUi();
        ApplyFilter("");
    }

    private void BuildUi()
    {
        lblTitle.Text = "Sửa tên hàng";
        lblTitle.SetBounds(24, 16, 440, 32);
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblTitle);

        lblSummary.Text = $"{_allRows.Count:N0} sản phẩm - để trống \"Tên hàng mới\" nếu muốn giữ nguyên tên gốc.";
        lblSummary.SetBounds(26, 52, 560, 22);
        lblSummary.Font = AppTheme.Fonts.Subtitle;
        lblSummary.ForeColor = AppTheme.Colors.TextSecondary;
        Controls.Add(lblSummary);

        txtSearch.PlaceholderText = "Tìm theo mã hoặc tên sản phẩm...";
        txtSearch.SetBounds(ClientSize.Width - 320, 20, 296, 38);
        txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        txtSearch.ContainerColor = AppTheme.Colors.Background;
        txtSearch.TextChanged += (_, _) => ApplyFilter(txtSearch.Text);
        Controls.Add(txtSearch);

        pnlGridCard.SetBounds(20, 90, ClientSize.Width - 40, ClientSize.Height - 166);
        pnlGridCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlGridCard.CornerRadius = 16;
        pnlGridCard.FillColor = AppTheme.Colors.SurfaceElevated;
        pnlGridCard.BorderColor = AppTheme.Colors.Border;
        pnlGridCard.BorderThickness = 1;
        pnlGridCard.ContainerColor = AppTheme.Colors.Background;
        Controls.Add(pnlGridCard);

        dgv.SetBounds(1, 1, pnlGridCard.Width - 2, pnlGridCard.Height - 2);
        dgv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        dgv.ReadOnly = false;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;
        dgv.MultiSelect = false;
        dgv.AutoGenerateColumns = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        AppTheme.StyleGrid(dgv);
        BuildColumns();
        pnlGridCard.Controls.Add(dgv);

        lblFooterHint.Text = "Sản phẩm không nhập \"Tên hàng mới\" sẽ giữ nguyên tên hiện tại trong file Excel.";
        lblFooterHint.SetBounds(24, ClientSize.Height - 54, 620, 22);
        lblFooterHint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblFooterHint.Font = AppTheme.Fonts.Hint;
        lblFooterHint.ForeColor = AppTheme.Colors.TextMuted;
        Controls.Add(lblFooterHint);

        btnApply.Text = "Áp dụng";
        btnApply.Icon = IconGlyphs.Kind.Check;
        btnApply.SetBounds(ClientSize.Width - 244, ClientSize.Height - 62, 140, 44);
        btnApply.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnApply.Variant = ButtonVariant.Primary;
        btnApply.ContainerColor = AppTheme.Colors.Background;
        btnApply.Font = new Font(AppTheme.Fonts.Button.FontFamily, 10.5f, FontStyle.Bold);
        btnApply.Click += BtnApply_Click;
        Controls.Add(btnApply);

        btnCancel.Text = "Hủy";
        btnCancel.SetBounds(ClientSize.Width - 100, ClientSize.Height - 62, 76, 44);
        btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnCancel.Variant = ButtonVariant.Secondary;
        btnCancel.ContainerColor = AppTheme.Colors.Background;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.Click += (_, _) => Close();
        Controls.Add(btnCancel);

        CancelButton = btnCancel;
        AcceptButton = null;
    }

    private void BuildColumns()
    {
        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ProductCode",
            HeaderText = "Mã sản phẩm",
            DataPropertyName = "ProductCode",
            ReadOnly = true,
            FillWeight = 90
        });

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ProductName",
            HeaderText = "Tên hàng hiện tại",
            DataPropertyName = "ProductName",
            ReadOnly = true,
            FillWeight = 220
        });

        DataGridViewTextBoxColumn newNameColumn = new()
        {
            Name = "NewNameText",
            HeaderText = "Tên hàng mới",
            DataPropertyName = "NewNameText",
            ReadOnly = false,
            FillWeight = 220,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = AppTheme.Colors.PrimaryLight
            }
        };
        dgv.Columns.Add(newNameColumn);
    }

    private void ApplyFilter(string query)
    {
        query = query.Trim();

        List<NameEditRow> filtered = string.IsNullOrEmpty(query)
            ? _allRows
            : _allRows.Where(r =>
                r.ProductCode.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.ProductName.Contains(query, StringComparison.OrdinalIgnoreCase))
              .ToList();

        dgv.DataSource = null;
        dgv.DataSource = filtered;
    }

    private void BtnApply_Click(object? sender, EventArgs e)
    {
        dgv.EndEdit();

        ResultOverrides.Clear();

        foreach (NameEditRow row in _allRows)
        {
            string newName = (row.NewNameText ?? "").Trim();

            if (newName.Length == 0 || string.IsNullOrWhiteSpace(row.ProductCode))
                continue;

            ResultOverrides[row.ProductCode] = newName;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private class NameEditRow
    {
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string NewNameText { get; set; } = "";
    }
}
