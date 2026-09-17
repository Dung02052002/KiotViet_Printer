using System.Globalization;
using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

// Modal "Sửa giá một vài sản phẩm" - mở từ FormMain khi chọn chế độ giá bán
// tương ứng ở màn hình Tem đầy đủ. Chỉ chỉnh giá trong bộ nhớ, không đụng tới
// file Excel nguồn (xem PriceOverride, ExcelService.CopyToBarTenderData).
public class FormEditPrices : Form
{
    private static readonly CultureInfo VnCulture = CultureInfo.GetCultureInfo("vi-VN");

    private readonly Label lblTitle = new();
    private readonly Label lblSummary = new();
    private readonly RoundedTextBox txtSearch = new();
    private readonly RoundedPanel pnlGridCard = new();
    private readonly SmoothDataGridView dgv = new();
    private readonly Label lblFooterHint = new();
    private readonly RoundedButton btnApply = new();
    private readonly RoundedButton btnCancel = new();

    private readonly List<PriceEditRow> _allRows;

    public Dictionary<string, double> ResultOverrides { get; } = new(StringComparer.OrdinalIgnoreCase);

    public FormEditPrices(List<ProductRow> products, IReadOnlyDictionary<string, double> existingOverrides)
    {
        Text = "Sửa giá một vài sản phẩm";
        Width = 980;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(760, 480);
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        _allRows = products.Select(p =>
        {
            string newPriceText = !string.IsNullOrWhiteSpace(p.ProductCode) &&
                existingOverrides.TryGetValue(p.ProductCode, out double overridden)
                    ? ((long)overridden).ToString("N0", VnCulture)
                    : "";

            return new PriceEditRow
            {
                ProductCode = p.ProductCode,
                ProductName = string.IsNullOrWhiteSpace(p.ProductNameWithAttr) ? p.ProductName : p.ProductNameWithAttr,
                CurrentPrice = p.Price,
                CurrentPriceDisplay = p.Price.ToString("N0", VnCulture),
                NewPriceText = newPriceText
            };
        }).ToList();

        BuildUi();
        ApplyFilter("");
    }

    private void BuildUi()
    {
        lblTitle.Text = "Sửa giá một vài sản phẩm";
        lblTitle.SetBounds(24, 16, 440, 32);
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblTitle);

        lblSummary.Text = $"{_allRows.Count:N0} sản phẩm - để trống \"Giá mới\" nếu muốn giữ nguyên giá cũ.";
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
        // Cho phép kéo chuột chọn nhiều ô (Ctrl+C có sẵn của DataGridView) rồi
        // Ctrl+V để dán nhanh - xem Dgv_KeyDown/PasteFromClipboard.
        dgv.MultiSelect = true;
        dgv.ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
        dgv.AutoGenerateColumns = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        AppTheme.StyleGrid(dgv);
        BuildColumns();
        dgv.CellEndEdit += Dgv_CellEndEdit;
        dgv.KeyDown += Dgv_KeyDown;
        pnlGridCard.Controls.Add(dgv);

        lblFooterHint.Text = "Sản phẩm không nhập \"Giá mới\" sẽ giữ nguyên giá hiện tại trong file Excel. " +
            "Kéo chuột chọn nhiều ô rồi Ctrl+C/Ctrl+V để copy giá nhanh.";
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
            HeaderText = "Tên sản phẩm",
            DataPropertyName = "ProductName",
            ReadOnly = true,
            FillWeight = 260
        });

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "CurrentPriceDisplay",
            HeaderText = "Giá hiện tại",
            DataPropertyName = "CurrentPriceDisplay",
            ReadOnly = true,
            FillWeight = 110,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight }
        });

        DataGridViewTextBoxColumn newPriceColumn = new()
        {
            Name = "NewPriceText",
            HeaderText = "Giá mới",
            DataPropertyName = "NewPriceText",
            ReadOnly = false,
            FillWeight = 110,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                BackColor = AppTheme.Colors.PrimaryLight
            }
        };
        dgv.Columns.Add(newPriceColumn);
    }

    // Chỉ cho nhập số, tự định dạng dấu phân cách nghìn giống ô "Nhập giá bán"
    // ở màn hình chính (xem FormMain.TxtUniformPrice_TextChanged).
    private void Dgv_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (dgv.Columns[e.ColumnIndex].Name != "NewPriceText")
            return;

        DataGridViewCell cell = dgv.Rows[e.RowIndex].Cells[e.ColumnIndex];
        cell.Value = FormatPriceText(cell.Value?.ToString() ?? "");
    }

    private static string FormatPriceText(string raw)
    {
        string digits = new(raw.Where(char.IsDigit).ToArray());

        if (digits.Length > 15)
            digits = digits[..15];

        return digits.Length == 0 || !long.TryParse(digits, out long parsed)
            ? ""
            : parsed.ToString("N0", VnCulture);
    }

    // Cho phép kéo chuột chọn nhiều ô rồi Ctrl+V để dán nhanh, kể cả dán 1 giá
    // trị (copy từ 1 ô) vào cả vùng đã chọn - giống Excel. Ctrl+C dùng hành vi
    // có sẵn của DataGridView (ClipboardCopyMode ở trên), không cần code thêm.
    private void Dgv_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.V)
        {
            PasteFromClipboard();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void PasteFromClipboard()
    {
        if (!Clipboard.ContainsText())
            return;

        string text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text))
            return;

        string[] lines = text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        string[][] grid = lines.Select(l => l.Split('\t')).ToArray();

        dgv.EndEdit();

        List<DataGridViewCell> selected = dgv.SelectedCells.Cast<DataGridViewCell>().ToList();

        // Dán 1 giá trị (copy từ đúng 1 ô) vào toàn bộ vùng đang bôi đen -
        // giống Excel, đúng nhu cầu sửa nhanh giá của nhiều sản phẩm cùng lúc.
        if (grid.Length == 1 && grid[0].Length == 1 && selected.Count > 1)
        {
            string value = FormatPriceText(grid[0][0]);
            foreach (DataGridViewCell cell in selected)
            {
                if (!cell.ReadOnly)
                    cell.Value = value;
            }
            return;
        }

        int startRow = selected.Count > 0 ? selected.Min(c => c.RowIndex) : dgv.CurrentCell?.RowIndex ?? -1;
        int startCol = selected.Count > 0 ? selected.Min(c => c.ColumnIndex) : dgv.CurrentCell?.ColumnIndex ?? -1;

        if (startRow < 0 || startCol < 0)
            return;

        for (int r = 0; r < grid.Length; r++)
        {
            int rowIndex = startRow + r;
            if (rowIndex >= dgv.Rows.Count)
                break;

            for (int c = 0; c < grid[r].Length; c++)
            {
                int colIndex = startCol + c;
                if (colIndex >= dgv.Columns.Count)
                    break;

                DataGridViewCell target = dgv.Rows[rowIndex].Cells[colIndex];
                if (!target.ReadOnly)
                    target.Value = FormatPriceText(grid[r][c]);
            }
        }
    }

    private void ApplyFilter(string query)
    {
        query = query.Trim();

        List<PriceEditRow> filtered = string.IsNullOrEmpty(query)
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

        foreach (PriceEditRow row in _allRows)
        {
            string digits = new((row.NewPriceText ?? "").Where(char.IsDigit).ToArray());

            if (digits.Length == 0 || string.IsNullOrWhiteSpace(row.ProductCode))
                continue;

            if (long.TryParse(digits, out long price) && price >= 0)
                ResultOverrides[row.ProductCode] = price;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private class PriceEditRow
    {
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public double CurrentPrice { get; set; }
        public string CurrentPriceDisplay { get; set; } = "";
        public string NewPriceText { get; set; } = "";
    }
}
