using System.Linq;
using System.Windows.Forms;
using KiotVietLabelPrinter.Models.Glasses;
using KiotVietLabelPrinter.Services.Glasses;
using KiotVietLabelPrinter.Services.Glasses.Lexer;
using KiotVietLabelPrinter.Services.Glasses.Tests;

namespace KiotVietLabelPrinter.Forms;

public partial class FormParserLab : Form
{
    public FormParserLab()
    {
        InitializeComponent();

        WireEvents();

        dgvTest.CellDoubleClick += DgvTest_CellDoubleClick;
    }

    //---------------------------------------------------------
    // Parse
    //---------------------------------------------------------

    private void ParseCurrent()
    {
        dgvToken.DataSource = null;

    string text = txtInput.Text.Trim();

    if (string.IsNullOrWhiteSpace(text))
        return;

    //-------------------------------------------------
    // Parser
    //-------------------------------------------------

    GlassesParser parser = new();

        GlassesParserResult result = parser.Parse(text);

        //-----------------------------------------------------
        // Result
        //-----------------------------------------------------

        lblBaseCode.Text = $"BaseCode : {result.BaseCode}";
        lblRule.Text = $"Rule : {result.RuleName}";
        lblTime.Text = $"Time : {result.Elapsed.TotalMilliseconds:0.###} ms";

        //-----------------------------------------------------
        // Token
        //-----------------------------------------------------

        List<GlassesToken> tokens = GlassesLexer.Scan(text);

        dgvToken.DataSource = tokens
            .Select(x => new
            {
                x.Index,
                Type = x.Type.ToString(),
                x.Text,
                x.Start,
                x.End
            })
            .ToList();

        //-----------------------------------------------------
        // Log
        //-----------------------------------------------------

        dgvTest.DataSource =
    result.Logs
        .Select(x => new
        {
            Log = x
        })
        .ToList();
    }

    //---------------------------------------------------------
    // Double Click Test
    //---------------------------------------------------------

    private void DgvTest_CellDoubleClick(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        List<ParserTestCase> tests =
            ParserSamples.Get();

        if (e.RowIndex >= tests.Count)
            return;

        ParserTestCase test = tests[e.RowIndex];

        txtInput.Text = test.Input;

        ParseCurrent();
    }
}