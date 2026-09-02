using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormParseCheck : Form
{
    private readonly SmoothDataGridView dgv = new();
    private readonly RoundedPanel pnlGridCard = new();
    private readonly Label lblTitle = new();
    private readonly Label lblSummary = new();
    private readonly Label lblFooterHint = new();
    private readonly ToggleSwitch chkOnlyFlagged = new();
    private readonly RoundedButton btnClose = new();

    private readonly LabelService _labelService = new();
    private readonly LabelCatalogService _catalogService = new();

    private readonly string _sourceExcelFile;
    private readonly string _labelCode;
    private readonly string _employeeCode;

    private List<ParseCheckRow> _allRows = [];

    public FormParseCheck(
        string sourceExcelFile,
        string labelCode,
        string employeeCode)
    {
        _sourceExcelFile = sourceExcelFile;
        _labelCode = labelCode;
        _employeeCode = employeeCode;

        Text = "Kiểm tra mã parse";
        Width = 1250;
        Height = 720;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(900, 560);
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        Shown += async (_, _) => await LoadDataAsync();
    }

    private void BuildUi()
    {
        lblTitle.Text = "Kiểm tra mã parse";
        lblTitle.SetBounds(24, 16, 520, 32);
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblTitle);

        lblSummary.Text = "Đang phân tích dữ liệu...";
        lblSummary.SetBounds(26, 52, ClientSize.Width - 340, 22);
        lblSummary.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        lblSummary.Font = AppTheme.Fonts.Subtitle;
        lblSummary.ForeColor = AppTheme.Colors.TextSecondary;
        Controls.Add(lblSummary);

        chkOnlyFlagged.Text = "Chỉ hiện dòng nghi ngờ";
        chkOnlyFlagged.SetBounds(ClientSize.Width - 280, 24, 256, 28);
        chkOnlyFlagged.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        chkOnlyFlagged.Font = AppTheme.Fonts.Body;
        chkOnlyFlagged.ForeColor = AppTheme.Colors.TextPrimary;
        chkOnlyFlagged.ContainerColor = AppTheme.Colors.Background;
        chkOnlyFlagged.Enabled = false;
        chkOnlyFlagged.CheckedChanged += (_, _) => RenderGrid();
        Controls.Add(chkOnlyFlagged);

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
        dgv.ReadOnly = true;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.MultiSelect = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        AppTheme.StyleGrid(dgv);
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        pnlGridCard.Controls.Add(dgv);

        lblFooterHint.Text = "Các dòng nền hồng cần được kiểm tra trước khi in.";
        lblFooterHint.SetBounds(24, ClientSize.Height - 54, 620, 22);
        lblFooterHint.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        lblFooterHint.Font = AppTheme.Fonts.Hint;
        lblFooterHint.ForeColor = AppTheme.Colors.TextMuted;
        Controls.Add(lblFooterHint);

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

    private async Task LoadDataAsync()
    {
        Cursor = Cursors.WaitCursor;

        try
        {
            _allRows = await Task.Run(() =>
            {
                List<PreviewRow> previewRows = _labelService.BuildPreview(
                    _sourceExcelFile,
                    _labelCode,
                    _employeeCode);

                return ParseCheckService.Build(previewRows);
            });

            if (IsDisposed || Disposing || !Visible)
                return;

            chkOnlyFlagged.Enabled = true;
            RenderGrid();
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing && Visible)
            {
                MessageBox.Show(ex.Message, "Lỗi kiểm tra mã");
                Close();
            }
        }
        finally
        {
            if (!IsDisposed && !Disposing)
                Cursor = Cursors.Default;
        }
    }

    private void RenderGrid()
    {
        List<ParseCheckRow> shown = chkOnlyFlagged.Checked
            ? _allRows.Where(x => x.HasFlag).ToList()
            : _allRows;

        dgv.DataSource = null;

        dgv.DataSource = shown
            .Select(x => new
            {
                x.ProductName,
                x.ProductNameWithAttr,
                x.ProductCode,
                x.ParsedCode,
                x.FinalCode,
                x.Flags
            })
            .ToList();

        FormatGrid();

        for (int i = 0; i < shown.Count; i++)
        {
            if (shown[i].HasFlag)
            {
                dgv.Rows[i].DefaultCellStyle.BackColor = AppTheme.Colors.DangerLight;
            }
        }

        int total = _allRows.Count;
        int flagged = _allRows.Count(x => x.HasFlag);

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
            $"Tổng: {total} dòng   |   " +
            $"Nghi ngờ: {flagged} dòng";
    }

    private void FormatGrid()
    {
        var cName = dgv.Columns["ProductName"];
        if (cName != null) cName.HeaderText = "Tên hàng";

        var cNameAttr = dgv.Columns["ProductNameWithAttr"];
        if (cNameAttr != null) cNameAttr.HeaderText = "Tên hàng (thuộc tính)";

        var cCode = dgv.Columns["ProductCode"];
        if (cCode != null) cCode.HeaderText = "Mã hàng gốc";

        var cParsed = dgv.Columns["ParsedCode"];
        if (cParsed != null) cParsed.HeaderText = "Mã parser";

        var cFinal = dgv.Columns["FinalCode"];
        if (cFinal != null) cFinal.HeaderText = "Mã in cuối";

        var cFlags = dgv.Columns["Flags"];
        if (cFlags != null) cFlags.HeaderText = "Cờ nghi ngờ";

        if (cName != null) cName.FillWeight = 160;
        if (cNameAttr != null) cNameAttr.FillWeight = 220;
        if (cCode != null) cCode.FillWeight = 90;
        if (cParsed != null) cParsed.FillWeight = 100;
        if (cFinal != null) cFinal.FillWeight = 110;
        if (cFlags != null) cFlags.FillWeight = 220;
    }
}
