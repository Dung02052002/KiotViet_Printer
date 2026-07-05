using KiotVietLabelPrinter.Services;

namespace KiotVietLabelPrinter.Forms;

public class FormConfig : Form
{
    private readonly TextBox txtBarTender = new();
    private readonly TextBox txtFullTemplate = new();
    private readonly TextBox txtFullData = new();
    private readonly TextBox txtBarcodeTemplate = new();
    private readonly TextBox txtBarcodeData = new();

    private readonly Button btnBrowseBarTender = new();
    private readonly Button btnBrowseFullTemplate = new();
    private readonly Button btnBrowseFullData = new();
    private readonly Button btnBrowseBarcodeTemplate = new();
    private readonly Button btnBrowseBarcodeData = new();
    private readonly Button btnSave = new();

    public FormConfig()
    {
        Text = "Cấu hình phần mềm";
        Width = 850;
        Height = 380;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();
        LoadConfig();
    }

    private void BuildUi()
    {
        int leftLabel = 20;
        int leftText = 170;
        int top = 20;
        int rowHeight = 45;

        Controls.Add(CreateLabel("BarTender.exe", leftLabel, top));
        txtBarTender.SetBounds(leftText, top, 540, 28);
        btnBrowseBarTender.Text = "...";
        btnBrowseBarTender.SetBounds(720, top, 50, 28);
        btnBrowseBarTender.Click += (_, _) => BrowseFile(txtBarTender, "Executable|*.exe");
        Controls.Add(txtBarTender);
        Controls.Add(btnBrowseBarTender);

        top += rowHeight;
        Controls.Add(CreateLabel("Template 3 tem", leftLabel, top));
        txtFullTemplate.SetBounds(leftText, top, 540, 28);
        btnBrowseFullTemplate.Text = "...";
        btnBrowseFullTemplate.SetBounds(720, top, 50, 28);
        btnBrowseFullTemplate.Click += (_, _) => BrowseFile(txtFullTemplate, "BarTender Template|*.btw");
        Controls.Add(txtFullTemplate);
        Controls.Add(btnBrowseFullTemplate);

        top += rowHeight;
        Controls.Add(CreateLabel("Data 3 tem", leftLabel, top));
        txtFullData.SetBounds(leftText, top, 540, 28);
        btnBrowseFullData.Text = "...";
        btnBrowseFullData.SetBounds(720, top, 50, 28);
        btnBrowseFullData.Click += (_, _) => BrowseFile(txtFullData, "Excel Files|*.xls;*.xlsx");
        Controls.Add(txtFullData);
        Controls.Add(btnBrowseFullData);

        top += rowHeight;
        Controls.Add(CreateLabel("Template mã vạch", leftLabel, top));
        txtBarcodeTemplate.SetBounds(leftText, top, 540, 28);
        btnBrowseBarcodeTemplate.Text = "...";
        btnBrowseBarcodeTemplate.SetBounds(720, top, 50, 28);
        btnBrowseBarcodeTemplate.Click += (_, _) => BrowseFile(txtBarcodeTemplate, "BarTender Template|*.btw");
        Controls.Add(txtBarcodeTemplate);
        Controls.Add(btnBrowseBarcodeTemplate);

        top += rowHeight;
        Controls.Add(CreateLabel("Data mã vạch", leftLabel, top));
        txtBarcodeData.SetBounds(leftText, top, 540, 28);
        btnBrowseBarcodeData.Text = "...";
        btnBrowseBarcodeData.SetBounds(720, top, 50, 28);
        btnBrowseBarcodeData.Click += (_, _) => BrowseFile(txtBarcodeData, "Excel Files|*.xls;*.xlsx");
        Controls.Add(txtBarcodeData);
        Controls.Add(btnBrowseBarcodeData);

        top += 60;
        btnSave.Text = "Lưu cấu hình";
        btnSave.SetBounds(320, top, 160, 40);
        btnSave.Click += BtnSave_Click;
        Controls.Add(btnSave);
    }

    private Label CreateLabel(string text, int left, int top)
    {
        return new Label
        {
            Text = text,
            Left = left,
            Top = top + 5,
            Width = 140
        };
    }

    private void BrowseFile(TextBox target, string filter)
    {
        using OpenFileDialog dialog = new()
        {
            Filter = filter
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            target.Text = dialog.FileName;
        }
    }

    private void LoadConfig()
    {
        var config = ConfigService.Instance.Config;

        txtBarTender.Text = config.BarTenderExe;
        txtFullTemplate.Text = config.FullLabel.Template;
        txtFullData.Text = config.FullLabel.Data;
        txtBarcodeTemplate.Text = config.BarcodeLabel.Template;
        txtBarcodeData.Text = config.BarcodeLabel.Data;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        var config = ConfigService.Instance.Config;

        config.BarTenderExe = txtBarTender.Text.Trim();
        config.FullLabel.Template = txtFullTemplate.Text.Trim();
        config.FullLabel.Data = txtFullData.Text.Trim();
        config.BarcodeLabel.Template = txtBarcodeTemplate.Text.Trim();
        config.BarcodeLabel.Data = txtBarcodeData.Text.Trim();

        ConfigService.Instance.Save();

        MessageBox.Show("Đã lưu cấu hình.");
        DialogResult = DialogResult.OK;
        Close();
    }
}