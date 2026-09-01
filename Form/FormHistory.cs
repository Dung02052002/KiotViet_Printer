using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormHistory : Form
{
    private readonly HistoryService _historyService = new();

    private readonly SmoothDataGridView dgvHistory = new();
    private readonly RoundedButton btnRefresh = new();
    private readonly RoundedButton btnClose = new();

    public FormHistory()
    {
        Text = "Lịch sử in tem";
        Width = 980;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        LoadHistory();
    }

    private void BuildUi()
    {
        Label lblTitle = new()
        {
            Text = "Lịch sử in tem",
            Left = 20,
            Top = 18,
            Width = 400,
            Font = AppTheme.Fonts.Title,
            ForeColor = AppTheme.Colors.TextPrimary
        };
        Controls.Add(lblTitle);

        btnRefresh.Text = "↻ Làm mới";
        btnRefresh.Left = 720;
        btnRefresh.Top = 20;
        btnRefresh.Width = 100;
        btnRefresh.Height = 36;
        btnRefresh.Variant = ButtonVariant.Outline;
        btnRefresh.Click += (_, _) => LoadHistory();
        Controls.Add(btnRefresh);

        btnClose.Text = "Đóng";
        btnClose.Left = 835;
        btnClose.Top = 20;
        btnClose.Width = 100;
        btnClose.Height = 36;
        btnClose.Variant = ButtonVariant.Secondary;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        dgvHistory.Left = 20;
        dgvHistory.Top = 68;
        dgvHistory.Width = 915;
        dgvHistory.Height = 400;
        dgvHistory.ReadOnly = true;
        dgvHistory.AllowUserToAddRows = false;
        dgvHistory.AllowUserToDeleteRows = false;
        dgvHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvHistory.MultiSelect = false;
        dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AppTheme.StyleGrid(dgvHistory);
        Controls.Add(dgvHistory);
    }

    private void LoadHistory()
    {
        try
        {
            List<PrintHistory> histories = _historyService.GetAll() ?? new List<PrintHistory>();

            var rows = histories
                .OrderByDescending(x => x.PrintTime)
                .Select(x => new
                {
                    ThoiGian = x.PrintTime.ToString("dd/MM/yyyy HH:mm:ss"),
                    LoaiTem = x.LabelName ?? "",
                    MaTem = x.LabelCode ?? "",
                    MaNhanVien = x.EmployeeCode ?? "",
                    SoSanPham = x.ProductCount,
                    TongSoTem = x.TotalLabels,
                    FileExcel = x.SourceExcelFile ?? "",
                    MayTinh = x.MachineName ?? "",
                    NguoiDung = x.UserName ?? ""
                })
                .ToList();

            dgvHistory.DataSource = null;
            dgvHistory.DataSource = rows;

            var cThoiGian = dgvHistory.Columns["ThoiGian"];
            if (cThoiGian != null)
                cThoiGian.HeaderText = "Thời gian";

            var cLoaiTem = dgvHistory.Columns["LoaiTem"];
            if (cLoaiTem != null)
                cLoaiTem.HeaderText = "Loại tem";

            var cMaTem = dgvHistory.Columns["MaTem"];
            if (cMaTem != null)
                cMaTem.HeaderText = "Mã tem";

            var cMaNhanVien = dgvHistory.Columns["MaNhanVien"];
            if (cMaNhanVien != null)
                cMaNhanVien.HeaderText = "Mã nhân viên";

            var cSoSanPham = dgvHistory.Columns["SoSanPham"];
            if (cSoSanPham != null)
                cSoSanPham.HeaderText = "Số sản phẩm";

            var cTongSoTem = dgvHistory.Columns["TongSoTem"];
            if (cTongSoTem != null)
                cTongSoTem.HeaderText = "Tổng số tem";

            var cFileExcel = dgvHistory.Columns["FileExcel"];
            if (cFileExcel != null)
                cFileExcel.HeaderText = "File Excel";

            var cMayTinh = dgvHistory.Columns["MayTinh"];
            if (cMayTinh != null)
                cMayTinh.HeaderText = "Máy tính";

            var cNguoiDung = dgvHistory.Columns["NguoiDung"];
            if (cNguoiDung != null)
                cNguoiDung.HeaderText = "Người dùng";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Không tải được lịch sử in tem.\n\n{ex.Message}",
                "Lỗi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
