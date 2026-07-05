using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormMain : Form
{
    private readonly TextBox txtExcelFile = new();
    private readonly TextBox txtEmployeeCode = new();

    private readonly CheckBox chkFullLabel = new();
    private readonly CheckBox chkBarcodeLabel = new();

    private readonly Button btnChooseExcel = new();
    private readonly Button btnConfig = new();
    private readonly Button btnPrint = new();

    private readonly LabelService _labelService = new();

    public FormMain()
    {
        Text = "KiotViet Label Printer";
        Width = 700;
        Height = 300;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        CheckConfigOnStart();
    }

    private void BuildUi()
    {
        Label lblExcel = new()
        {
            Text = "File Excel KiotViet",
            Left = 20,
            Top = 25,
            Width = 120
        };
        Controls.Add(lblExcel);

        txtExcelFile.SetBounds(150, 20, 380, 28);
        txtExcelFile.ReadOnly = true;
        Controls.Add(txtExcelFile);

        btnChooseExcel.Text = "Chọn file";
        btnChooseExcel.SetBounds(540, 20, 100, 30);
        btnChooseExcel.Click += BtnChooseExcel_Click;
        Controls.Add(btnChooseExcel);

        chkFullLabel.Text = "Tem đầy đủ";
        chkFullLabel.SetBounds(150, 70, 200, 30);
        Controls.Add(chkFullLabel);

        chkBarcodeLabel.Text = "Tem mã vạch";
        chkBarcodeLabel.SetBounds(350, 70, 200, 30);
        Controls.Add(chkBarcodeLabel);

        Label lblEmployee = new()
        {
            Text = "Mã nhân viên",
            Left = 20,
            Top = 120,
            Width = 120
        };
        Controls.Add(lblEmployee);

        txtEmployeeCode.SetBounds(150, 115, 380, 28);
        Controls.Add(txtEmployeeCode);

        btnConfig.Text = "Cấu hình";
        btnConfig.SetBounds(150, 180, 120, 40);
        btnConfig.Click += BtnConfig_Click;
        Controls.Add(btnConfig);

        btnPrint.Text = "IN TEM";
        btnPrint.SetBounds(300, 180, 180, 40);
        btnPrint.Click += BtnPrint_Click;
        Controls.Add(btnPrint);
    }

    private void CheckConfigOnStart()
    {
        if (!ConfigService.Instance.IsConfigured())
        {
            MessageBox.Show("Phần mềm chưa được cấu hình. Vui lòng chọn đường dẫn trước khi sử dụng.");
            using FormConfig formConfig = new();
            formConfig.ShowDialog();
        }
    }

    private void BtnChooseExcel_Click(object? sender, EventArgs e)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "Excel Files|*.xls;*.xlsx"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            txtExcelFile.Text = dialog.FileName;
        }
    }

    private void BtnConfig_Click(object? sender, EventArgs e)
    {
        using FormConfig formConfig = new();
        formConfig.ShowDialog();
    }

    private void BtnPrint_Click(object? sender, EventArgs e)
{
    try
    {
        if (!chkFullLabel.Checked && !chkBarcodeLabel.Checked)
        {
            MessageBox.Show("Vui lòng chọn ít nhất một loại tem.");
            return;
        }

        if (!ConfigService.Instance.IsConfigured())
        {
            MessageBox.Show("Cấu hình chưa đầy đủ.");
            return;
        }

        int total = _labelService.Print(
            txtExcelFile.Text.Trim(),
            chkFullLabel.Checked,
            chkBarcodeLabel.Checked,
            txtEmployeeCode.Text.Trim());

        MessageBox.Show($"Đã xử lý {total} sản phẩm.");
    }
    catch (Exception ex)
    {
        MessageBox.Show(ex.Message, "Lỗi");
    }
}
}