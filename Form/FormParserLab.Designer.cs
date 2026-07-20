using System.Windows.Forms;

namespace KiotVietLabelPrinter.Forms;

partial class FormParserLab
{
    private TextBox txtInput = null!;

    private Button btnParse = null!;

    private Button btnRunAll = null!;

    private Button btnClear = null!;

    private Label lblBaseCode = null!;

    private Label lblRule = null!;

    private Label lblTime = null!;

    private DataGridView dgvToken = null!;

    private DataGridView dgvTest = null!;

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

        //------------------------------------------
        // Input
        //------------------------------------------

        txtInput.Multiline = true;

        txtInput.Left = 10;

        txtInput.Top = 10;

        txtInput.Width = 1060;

        txtInput.Height = 70;

        //------------------------------------------
        // Parse
        //------------------------------------------

        btnParse.Text = "Parse";

        btnParse.Left = 10;

        btnParse.Top = 90;

        btnParse.Width = 120;

        //------------------------------------------
        // Run All
        //------------------------------------------

        btnRunAll.Text = "Run All";

        btnRunAll.Left = 140;

        btnRunAll.Top = 90;

        btnRunAll.Width = 120;

        //------------------------------------------
        // Clear
        //------------------------------------------

        btnClear.Text = "Clear";

        btnClear.Left = 270;

        btnClear.Top = 90;

        btnClear.Width = 120;

        //------------------------------------------
        // Result
        //------------------------------------------

        lblBaseCode.Left = 10;

        lblBaseCode.Top = 130;

        lblBaseCode.Width = 600;

        lblBaseCode.Text = "BaseCode :";

        lblRule.Left = 10;

        lblRule.Top = 155;

        lblRule.Width = 600;

        lblRule.Text = "Rule :";

        lblTime.Left = 10;

        lblTime.Top = 180;

        lblTime.Width = 600;

        lblTime.Text = "Time :";

        //------------------------------------------
        // Grid
        //------------------------------------------

        dgvToken.Left = 10;

        dgvToken.Top = 220;

        dgvToken.Width = 700;

        dgvToken.Height = 470;

        dgvToken.ReadOnly = true;

        dgvToken.AllowUserToAddRows = false;

        dgvToken.RowHeadersVisible = false;

        dgvToken.AutoSizeColumnsMode =
            DataGridViewAutoSizeColumnsMode.Fill;

        //------------------------------------------
        // Log
        //------------------------------------------

        dgvTest.Left = 720;

dgvTest.Top = 220;

dgvTest.Width = 350;

dgvTest.Height = 470;

dgvTest.ReadOnly = true;

dgvTest.AllowUserToAddRows = false;

dgvTest.RowHeadersVisible = false;

dgvTest.SelectionMode =
    DataGridViewSelectionMode.FullRowSelect;

dgvTest.AutoSizeColumnsMode =
    DataGridViewAutoSizeColumnsMode.Fill;

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