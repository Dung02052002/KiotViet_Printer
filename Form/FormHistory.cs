using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormHistory : Form
{
    private readonly HistoryService _historyService = new();

    private readonly DataGridView dgvHistory = new();
    private readonly Button btnRefresh = new();
    private readonly Button btnClose = new();

    public FormHistory()
    {
        Text = "Lịch sử in tem";
        Width = 980;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;

        BuildUi();
        LoadHistory();
    }

    private void BuildUi()
    {
        Label lblTitle = new()
        {
            Text = "LỊCH SỬ IN TEM",
            Left = 20,
            Top = 15,
            Width = 300,
            Font = new Font("Segoe UI", 14, FontStyle.Bold)
        };
        Controls.Add(lblTitle);

        btnRefresh.Text = "Làm mới";
        btnRefresh.Left = 720;
        btnRefresh.Top = 12;
        btnRefresh.Width = 100;
        btnRefresh.Height = 34;
        btnRefresh.Click += (_, _) => LoadHistory();
        Controls.Add(btnRefresh);

        btnClose.Text = "Đóng";
        btnClose.Left = 835;
        btnClose.Top = 12;
        btnClose.Width = 100;
        btnClose.Height = 34;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        dgvHistory.Left = 20;
        dgvHistory.Top = 60;
        dgvHistory.Width = 915;
        dgvHistory.Height = 390;
        dgvHistory.ReadOnly = true;
        dgvHistory.AllowUserToAddRows = false;
        dgvHistory.AllowUserToDeleteRows = false;
        dgvHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvHistory.MultiSelect = false;
        dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvHistory.RowHeadersVisible = false;
        dgvHistory.BackgroundColor = Color.White;
        dgvHistory.BorderStyle = BorderStyle.FixedSingle;
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