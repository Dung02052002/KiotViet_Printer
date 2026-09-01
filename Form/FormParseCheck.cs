using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormParseCheck : Form
{
    private readonly SmoothDataGridView dgv = new();
    private readonly Label lblSummary = new();
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
        Height = 700;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        LoadData();
    }

    private void BuildUi()
    {
        lblSummary.Left = 20;
        lblSummary.Top = 18;
        lblSummary.Width = 900;
        lblSummary.Height = 26;
        lblSummary.Font = AppTheme.Fonts.BodyBold;
        lblSummary.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblSummary);

        chkOnlyFlagged.Text = "Chỉ hiện dòng nghi ngờ";
        chkOnlyFlagged.Left = 950;
        chkOnlyFlagged.Top = 20;
        chkOnlyFlagged.Width = 220;
        chkOnlyFlagged.Font = AppTheme.Fonts.Body;
        chkOnlyFlagged.ForeColor = AppTheme.Colors.TextPrimary;
        chkOnlyFlagged.ContainerColor = AppTheme.Colors.Background;
        chkOnlyFlagged.CheckedChanged += (_, _) => RenderGrid();
        Controls.Add(chkOnlyFlagged);

        dgv.Left = 20;
        dgv.Top = 54;
        dgv.Width = 1190;
        dgv.Height = 540;
        dgv.ReadOnly = true;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.MultiSelect = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        AppTheme.StyleGrid(dgv);
        dgv.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
        Controls.Add(dgv);

        btnClose.Text = "Đóng";
        btnClose.Left = 1090;
        btnClose.Top = 610;
        btnClose.Width = 120;
        btnClose.Height = 42;
        btnClose.Variant = ButtonVariant.Secondary;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }

    private void LoadData()
    {
        try
        {
            List<PreviewRow> previewRows = _labelService.BuildPreview(
                _sourceExcelFile,
                _labelCode,
                _employeeCode);

            _allRows = ParseCheckService.Build(previewRows);

            RenderGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Lỗi kiểm tra mã");
            Close();
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
