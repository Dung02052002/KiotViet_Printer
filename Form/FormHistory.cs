using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormHistory : Form
{
    private readonly DataGridView dgvHistory = new();
    private readonly Button btnRefresh = new();
    private readonly Button btnClose = new();

    private readonly HistoryService _historyService = new();

    public FormHistory()
    {
        Text = "Lịch sử in tem";
        Width = 1200;
        Height = 650;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadHistory();
    }

    private void BuildUi()
    {
        dgvHistory.Left = 20;
        dgvHistory.Top = 20;
        dgvHistory.Width = 1140;
        dgvHistory.Height = 520;
        dgvHistory.ReadOnly = true;
        dgvHistory.AllowUserToAddRows = false;
        dgvHistory.AllowUserToDeleteRows = false;
        dgvHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Controls.Add(dgvHistory);

        btnRefresh.Text = "Tải lại";
        btnRefresh.Left = 780;
        btnRefresh.Top = 560;
        btnRefresh.Width = 140;
        btnRefresh.Height = 40;
        btnRefresh.Click += (_, _) => LoadHistory();
        Controls.Add(btnRefresh);

        btnClose.Text = "Đóng";
        btnClose.Left = 940;
        btnClose.Top = 560;
        btnClose.Width = 140;
        btnClose.Height = 40;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);
    }

    private void LoadHistory()
    {
        List<PrintHistory> histories = _historyService.Load();

        dgvHistory.DataSource = null;
        dgvHistory.DataSource = histories;

        if (dgvHistory.Columns["SourceExcelFile"] != null)
            dgvHistory.Columns["SourceExcelFile"].Width = 260;
    }
}