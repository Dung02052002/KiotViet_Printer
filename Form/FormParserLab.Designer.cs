using System.Windows.Forms;
using KiotVietLabelPrinter.UI;

namespace KiotVietLabelPrinter.Forms;

partial class FormParserLab
{
    private RoundedTextBox txtInput = null!;

    private RoundedButton btnParse = null!;

    private RoundedButton btnRunAll = null!;

    private RoundedButton btnClear = null!;

    private Label lblBaseCode = null!;

    private Label lblRule = null!;

    private Label lblTime = null!;

    private SmoothDataGridView dgvToken = null!;

    private SmoothDataGridView dgvTest = null!;

    private void InitializeComponent()
    {
        txtInput = new();
        btnParse = new();
        btnRunAll = new();
        btnClear = new();

        lblBaseCode = new();
        lblRule = new();
        lblTime = new();

        dgvToken = new();
        dgvTest = new();

        SuspendLayout();

        //------------------------------------------
        // Form
        //------------------------------------------

        Text = "Parser Lab";

        Width = 1100;

        Height = 750;

        StartPosition =
            FormStartPosition.CenterParent;

        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        //------------------------------------------
        // Input
        //------------------------------------------

        txtInput.Multiline = true;

        txtInput.Left = 20;

        txtInput.Top = 20;

        txtInput.Width = 1040;

        txtInput.Height = 70;

        txtInput.Font = AppTheme.Fonts.Body;

        txtInput.ContainerColor = AppTheme.Colors.Background;

        txtInput.PlaceholderText = "Nhập tên sản phẩm hoặc chuỗi cần phân tích...";

        txtInput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        //------------------------------------------
        // Parse
        //------------------------------------------

        btnParse.Text = "Parse";

        btnParse.Left = 20;

        btnParse.Top = 100;

        btnParse.Width = 120;

        btnParse.Height = 34;

        btnParse.Variant = ButtonVariant.Primary;

        //------------------------------------------
        // Run All
        //------------------------------------------

        btnRunAll.Text = "Run All";

        btnRunAll.Left = 150;

        btnRunAll.Top = 100;

        btnRunAll.Width = 120;

        btnRunAll.Height = 34;

        btnRunAll.Variant = ButtonVariant.Outline;

        //------------------------------------------
        // Clear
        //------------------------------------------

        btnClear.Text = "Clear";

        btnClear.Left = 280;

        btnClear.Top = 100;

        btnClear.Width = 120;

        btnClear.Height = 34;

        btnClear.Variant = ButtonVariant.Ghost;

        //------------------------------------------
        // Result
        //------------------------------------------

        lblBaseCode.Left = 20;

        lblBaseCode.Top = 148;

        lblBaseCode.Width = 600;

        lblBaseCode.Text = "BaseCode :";

        lblBaseCode.Font = AppTheme.Fonts.BodyBold;

        lblBaseCode.ForeColor = AppTheme.Colors.TextPrimary;

        lblRule.Left = 20;

        lblRule.Top = 172;

        lblRule.Width = 600;

        lblRule.Text = "Rule :";

        lblRule.Font = AppTheme.Fonts.Body;

        lblRule.ForeColor = AppTheme.Colors.TextSecondary;

        lblTime.Left = 20;

        lblTime.Top = 196;

        lblTime.Width = 600;

        lblTime.Text = "Time :";

        lblTime.Font = AppTheme.Fonts.Body;

        lblTime.ForeColor = AppTheme.Colors.TextSecondary;

        //------------------------------------------
        // Grid
        //------------------------------------------

        dgvToken.Left = 20;

        dgvToken.Top = 230;

        dgvToken.Width = 690;

        dgvToken.Height = 470;

        dgvToken.ReadOnly = true;

        dgvToken.AllowUserToAddRows = false;

        dgvToken.AutoSizeColumnsMode =
            DataGridViewAutoSizeColumnsMode.Fill;

        AppTheme.StyleGrid(dgvToken);

        //------------------------------------------
        // Log
        //------------------------------------------

        dgvTest.Left = 720;

        dgvTest.Top = 230;

        dgvTest.Width = 340;

        dgvTest.Height = 470;

        dgvTest.ReadOnly = true;

        dgvTest.AllowUserToAddRows = false;

        dgvTest.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;

        dgvTest.AutoSizeColumnsMode =
            DataGridViewAutoSizeColumnsMode.Fill;

        AppTheme.StyleGrid(dgvTest);

        //------------------------------------------
        // Add
        //------------------------------------------

        Controls.Add(txtInput);

        Controls.Add(btnParse);

        Controls.Add(btnRunAll);

        Controls.Add(btnClear);

        Controls.Add(lblBaseCode);

        Controls.Add(lblRule);

        Controls.Add(lblTime);

        Controls.Add(dgvToken);

        Controls.Add(dgvTest);

        ResumeLayout(false);
    }
}
