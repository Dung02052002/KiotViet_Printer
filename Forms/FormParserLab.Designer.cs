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

    private Label lblInputTitle = null!;

    private Label lblInputHint = null!;

    private Label lblTokenTitle = null!;

    private Label lblLogTitle = null!;

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

        lblInputTitle = new();
        lblInputHint = new();

        lblTokenTitle = new();
        lblLogTitle = new();

        dgvToken = new();
        dgvTest = new();

        SuspendLayout();

        //------------------------------------------
        // Form
        //------------------------------------------

        Text = "Parser Lab";

        Width = 1100;

        Height = 750;

        MinimumSize = new Size(900, 600);

        FormBorderStyle = FormBorderStyle.Sizable;

        StartPosition =
            FormStartPosition.CenterParent;

        DoubleBuffered = true;

        AppTheme.StyleForm(this);

        //------------------------------------------
        // Input
        //------------------------------------------

        lblInputTitle.Text = "Chuỗi đầu vào";

        lblInputTitle.SetBounds(20, 18, 130, 24);

        lblInputTitle.Font = AppTheme.Fonts.BodyBold;

        lblInputTitle.ForeColor = AppTheme.Colors.TextPrimary;

        lblInputHint.Text = "Nhập tên sản phẩm hoặc chuỗi cần phân tích";

        lblInputHint.SetBounds(150, 19, 520, 24);

        lblInputHint.Font = AppTheme.Fonts.Hint;

        lblInputHint.ForeColor = AppTheme.Colors.TextMuted;

        txtInput.Multiline = true;

        txtInput.Left = 20;

        txtInput.Top = 46;

        txtInput.Width = 1040;

        txtInput.Height = 70;

        txtInput.Font = AppTheme.Fonts.Body;

        txtInput.ContainerColor = AppTheme.Colors.Background;

        txtInput.PlaceholderText = "Nhập tên sản phẩm hoặc chuỗi cần phân tích...";

        //------------------------------------------
        // Parse
        //------------------------------------------

        btnParse.Text = "Parse";

        btnParse.Left = 20;

        btnParse.Top = 130;

        btnParse.Width = 120;

        btnParse.Height = 38;

        btnParse.Variant = ButtonVariant.Primary;

        //------------------------------------------
        // Run All
        //------------------------------------------

        btnRunAll.Text = "Run All";

        btnRunAll.Left = 150;

        btnRunAll.Top = 130;

        btnRunAll.Width = 120;

        btnRunAll.Height = 38;

        btnRunAll.Variant = ButtonVariant.Outline;

        //------------------------------------------
        // Clear
        //------------------------------------------

        btnClear.Text = "Clear";

        btnClear.Left = 280;

        btnClear.Top = 130;

        btnClear.Width = 120;

        btnClear.Height = 38;

        btnClear.Variant = ButtonVariant.Ghost;

        //------------------------------------------
        // Result
        //------------------------------------------

        lblBaseCode.Left = 20;

        lblBaseCode.Top = 184;

        lblBaseCode.Width = 360;

        lblBaseCode.Text = "BaseCode :";

        lblBaseCode.Font = AppTheme.Fonts.BodyBold;

        lblBaseCode.AutoEllipsis = true;

        lblBaseCode.ForeColor = AppTheme.Colors.TextPrimary;

        lblRule.Left = 390;

        lblRule.Top = 184;

        lblRule.Width = 330;

        lblRule.Text = "Rule :";

        lblRule.Font = AppTheme.Fonts.Body;

        lblRule.AutoEllipsis = true;

        lblRule.ForeColor = AppTheme.Colors.TextSecondary;

        lblTime.Left = 740;

        lblTime.Top = 184;

        lblTime.Width = 320;

        lblTime.Text = "Time :";

        lblTime.Font = AppTheme.Fonts.Body;

        lblTime.AutoEllipsis = true;

        lblTime.ForeColor = AppTheme.Colors.TextSecondary;

        lblTokenTitle.Text = "Tokens";

        lblTokenTitle.SetBounds(20, 228, 400, 24);

        lblTokenTitle.Font = AppTheme.Fonts.BodyBold;

        lblTokenTitle.ForeColor = AppTheme.Colors.TextPrimary;

        lblLogTitle.Text = "Nhật ký rule";

        lblLogTitle.SetBounds(720, 228, 340, 24);

        lblLogTitle.Font = AppTheme.Fonts.BodyBold;

        lblLogTitle.ForeColor = AppTheme.Colors.TextPrimary;

        //------------------------------------------
        // Grid
        //------------------------------------------

        dgvToken.Left = 20;

        dgvToken.Top = 256;

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

        dgvTest.Top = 256;

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

        Controls.Add(lblInputTitle);

        Controls.Add(lblInputHint);

        Controls.Add(btnParse);

        Controls.Add(btnRunAll);

        Controls.Add(btnClear);

        Controls.Add(lblBaseCode);

        Controls.Add(lblRule);

        Controls.Add(lblTime);

        Controls.Add(lblTokenTitle);

        Controls.Add(lblLogTitle);

        Controls.Add(dgvToken);

        Controls.Add(dgvTest);

        ApplyResponsiveLayout();

        ClientSizeChanged += (_, _) => ApplyResponsiveLayout();

        ResumeLayout(false);
    }

    private void ApplyResponsiveLayout()
    {
        if (txtInput == null || dgvToken == null || dgvTest == null)
            return;

        const int margin = 20;
        const int gap = 10;
        int contentWidth = Math.Max(600, ClientSize.Width - (margin * 2));
        int tokenWidth = (int)Math.Round((contentWidth - gap) * 0.66);
        int logWidth = contentWidth - tokenWidth - gap;
        int gridHeight = Math.Max(230, ClientSize.Height - 276);

        txtInput.Width = contentWidth;

        int resultWidth = Math.Max(180, (contentWidth - (gap * 2)) / 3);
        lblBaseCode.SetBounds(margin, 184, resultWidth, 24);
        lblRule.SetBounds(margin + resultWidth + gap, 184, resultWidth, 24);
        lblTime.SetBounds(margin + ((resultWidth + gap) * 2), 184, resultWidth, 24);

        lblTokenTitle.SetBounds(margin, 228, tokenWidth, 24);
        lblLogTitle.SetBounds(margin + tokenWidth + gap, 228, logWidth, 24);

        dgvToken.SetBounds(margin, 256, tokenWidth, gridHeight);
        dgvTest.SetBounds(margin + tokenWidth + gap, 256, logWidth, gridHeight);
    }
}
