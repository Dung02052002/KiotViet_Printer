using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormHistory : Form
{
    private readonly HistoryService _historyService = new();
    private readonly DataGridView dgvHistory = new();

    public FormHistory()
    {
        Text = "Lịch sử in tem";
        Width = 1100;
        Height = 600;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadHistory();
    }

    private void BuildUi()
    {
        dgvHistory.Left = 20;
        dgvHistory.Top = 20;
        dgvHistory.Width = 1040;
        dgvHistory.Height = 500;
        dgvHistory.ReadOnly = true;
        dgvHistory.AllowUserToAddRows = false;
        dgvHistory.AllowUserToDeleteRows = false;
        dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(dgvHistory);
    }

    private void LoadHistory()
    {
        List<PrintHistory> histories = _historyService.Load();

        dgvHistory.DataSource = null;
        dgvHistory.DataSource = histories;

        if (dgvHistory.Columns["PrintTime"] != null)
            dgvHistory.Columns["PrintTime"].HeaderText = "Thời gian in";

        if (dgvHistory.Columns["SourceExcelFile"] != null)
        {
            dgvHistory.Columns["SourceExcelFile"].HeaderText = "File Excel";
            dgvHistory.Columns["SourceExcelFile"].Width = 260;
        }

        if (dgvHistory.Columns["LabelCode"] != null)
            dgvHistory.Columns["LabelCode"].HeaderText = "Mã tem";

        if (dgvHistory.Columns["LabelName"] != null)
            dgvHistory.Columns["LabelName"].HeaderText = "Loại tem";

        if (dgvHistory.Columns["EmployeeCode"] != null)
            dgvHistory.Columns["EmployeeCode"].HeaderText = "Mã nhân viên";

        if (dgvHistory.Columns["ProductCount"] != null)
            dgvHistory.Columns["ProductCount"].HeaderText = "Số SP";

        if (dgvHistory.Columns["TotalLabels"] != null)
            dgvHistory.Columns["TotalLabels"].HeaderText = "Tổng tem";

        if (dgvHistory.Columns["MachineName"] != null)
            dgvHistory.Columns["MachineName"].HeaderText = "Máy";

        if (dgvHistory.Columns["UserName"] != null)
            dgvHistory.Columns["UserName"].HeaderText = "User";
    }
}