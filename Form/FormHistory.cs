using KiotVietLabelPrinter.Models;
using KiotVietLabelPrinter.Services;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

public class FormHistory : Form
{
    private readonly HistoryService _historyService = new();

    private readonly Label lblTitle = new();
    private readonly Label lblSummary = new();
    private readonly RoundedPanel pnlGridCard = new();
    private readonly SmoothDataGridView dgvHistory = new();
    private readonly RoundedButton btnRefresh = new();
    private readonly RoundedButton btnClose = new();

    public FormHistory()
    {
        Text = "Lịch sử in tem";
        Width = 1040;
        Height = 620;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimumSize = new Size(820, 480);
        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        BuildUi();
        LoadHistory();
    }

    private void BuildUi()
    {
        lblTitle.Text = "Lịch sử in tem";
        lblTitle.SetBounds(24, 18, 440, 32);
        lblTitle.Font = AppTheme.Fonts.Title;
        lblTitle.ForeColor = AppTheme.Colors.TextPrimary;
        Controls.Add(lblTitle);

        lblSummary.Text = "Đang tải lịch sử...";
        lblSummary.SetBounds(26, 52, 600, 22);
        lblSummary.Font = AppTheme.Fonts.Subtitle;
        lblSummary.ForeColor = AppTheme.Colors.TextSecondary;
        Controls.Add(lblSummary);

        btnRefresh.Text = "Làm mới";
        btnRefresh.Icon = IconGlyphs.Kind.Refresh;
        btnRefresh.SetBounds(ClientSize.Width - 244, 24, 104, 38);
        btnRefresh.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnRefresh.Variant = ButtonVariant.Outline;
        btnRefresh.ContainerColor = AppTheme.Colors.Background;
        btnRefresh.Click += (_, _) => LoadHistory();
        Controls.Add(btnRefresh);

        btnClose.Text = "Đóng";
        btnClose.SetBounds(ClientSize.Width - 128, 24, 104, 38);
        btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnClose.Variant = ButtonVariant.Secondary;
        btnClose.ContainerColor = AppTheme.Colors.Background;
        btnClose.DialogResult = DialogResult.Cancel;
        btnClose.Click += (_, _) => Close();
        Controls.Add(btnClose);

        CancelButton = btnClose;

        pnlGridCard.SetBounds(20, 90, ClientSize.Width - 40, ClientSize.Height - 110);
        pnlGridCard.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        pnlGridCard.CornerRadius = 16;
        pnlGridCard.FillColor = AppTheme.Colors.SurfaceElevated;
        pnlGridCard.BorderColor = AppTheme.Colors.Border;
        pnlGridCard.BorderThickness = 1;
        pnlGridCard.ContainerColor = AppTheme.Colors.Background;
        Controls.Add(pnlGridCard);

        dgvHistory.SetBounds(1, 1, pnlGridCard.Width - 2, pnlGridCard.Height - 2);
        dgvHistory.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        dgvHistory.ReadOnly = true;
        dgvHistory.AllowUserToAddRows = false;
        dgvHistory.AllowUserToDeleteRows = false;
        dgvHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvHistory.MultiSelect = false;
        dgvHistory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        AppTheme.StyleGrid(dgvHistory);
        pnlGridCard.Controls.Add(dgvHistory);
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
            lblSummary.Text = $"{rows.Count:N0} lượt in đã được lưu · mới nhất hiển thị trước";

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

            SetColumnLayout(cThoiGian, 125);
            SetColumnLayout(cLoaiTem, 100);
            SetColumnLayout(cMaTem, 78);
            SetColumnLayout(cMaNhanVien, 115);
            SetColumnLayout(cSoSanPham, 105);
            SetColumnLayout(cTongSoTem, 100);
            SetColumnLayout(cFileExcel, 160);
            SetColumnLayout(cMayTinh, 90);
            SetColumnLayout(cNguoiDung, 90);
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

    private static void SetColumnLayout(DataGridViewColumn? column, int width)
    {
        if (column == null)
            return;

        column.MinimumWidth = width;
        column.FillWeight = width;
    }
}
