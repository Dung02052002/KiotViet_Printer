using System.Linq;
using KiotVietLabelPrinter.Services.Glasses.Tests;

namespace KiotVietLabelPrinter.Forms;

public partial class FormParserLab
{
    //---------------------------------------------------------
    // Wire Events
    //---------------------------------------------------------

    private void WireEvents()
    {
        btnParse.Click += BtnParse_Click;
        btnRunAll.Click += BtnRunAll_Click;
        btnClear.Click += BtnClear_Click;
    }

    //---------------------------------------------------------
    // Parse
    //---------------------------------------------------------

    private void BtnParse_Click(
        object? sender,
        EventArgs e)
    {
        ParseCurrent();
    }

    //---------------------------------------------------------
    // Clear
    //---------------------------------------------------------

    private void BtnClear_Click(
        object? sender,
        EventArgs e)
    {
        txtInput.Clear();

        dgvToken.DataSource = null;
        dgvTest.DataSource = null;

        lblBaseCode.Text = "BaseCode :";
        lblRule.Text = "Rule :";
        lblTime.Text = "Time :";

        txtInput.Focus();
    }

    //---------------------------------------------------------
    // Run All Test
    //---------------------------------------------------------

    private void BtnRunAll_Click(
        object? sender,
        EventArgs e)
    {
        List<ParserTestResult> results =
            ParserTestRunner.RunAll();

        dgvTest.DataSource =
            results
            .Select(x => new
            {
                x.TestCase.Name,
                x.TestCase.Input,
                x.TestCase.Expected,
                Actual = x.Actual,
                Result = x.Success ? "PASS" : "FAIL"
            })
            .ToList();

        int pass = results.Count(x => x.Success);
        int fail = results.Count - pass;

        lblRule.Text = $"PASS : {pass}     FAIL : {fail}";
    }
}