using System.Text;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

/// <summary>
/// Chế độ dòng lệnh: in ra CHÍNH XÁC lệnh BarTender sẽ được gửi cho từng loại
/// tem — KHÔNG thực thi. Mục đích: xác nhận không còn code path nào dựng
/// /XMLScript cho các loại tem không cần (đặc biệt "Tem đầy đủ").
///
///   "KiotViet Label Printer Pro V2.exe" --bartender-selftest
/// </summary>
public static class BarTenderSelfTest
{
    public static bool ShouldRun(string[] args)
        => args.Any(a => string.Equals(a, "--bartender-selftest", StringComparison.OrdinalIgnoreCase));

    public static int Run()
    {
        StringBuilder sb = new();

        sb.AppendLine();
        sb.AppendLine("==================== BARTENDER SELFTEST ====================");
        sb.AppendLine(AppInfo.DiagnosticsBlock());
        sb.AppendLine();

        AppConfig config = ConfigService.Instance.Config;
        sb.AppendLine($"BarTender.exe    : {config.BarTenderExe}");
        sb.AppendLine($"Máy in cấu hình  : {(string.IsNullOrWhiteSpace(config.PrinterName) ? "(mặc định Windows)" : config.PrinterName)}");

        BarTenderCapabilityService cap = BarTenderCapabilityService.Instance;
        cap.EnsureProbed(config.BarTenderExe);

        sb.AppendLine($"Registry Edition : {cap.DetectedEdition ?? "(không đọc được)"}");
        sb.AppendLine($"XMLScript hỗ trợ : {cap.XmlScriptSupported?.ToString() ?? "chưa biết"}");
        if (!string.IsNullOrWhiteSpace(cap.Detail))
            sb.AppendLine($"  chi tiết        : {cap.Detail}");
        sb.AppendLine();

        BarTenderService service = new();

        foreach (LabelDefinition label in config.Labels)
        {
            sb.AppendLine("-----------------------------------------------------------");
            sb.AppendLine($"Loại tem   : {label.Name}  [{label.Code} / handler={label.HandlerType}]");
            sb.AppendLine($"Template   : {label.TemplatePath}");

            // Tem kính là loại DUY NHẤT gửi Named Sub-String. Mô phỏng đúng
            // dạng dữ liệu để selftest chọn backend giống lúc chạy thật.
            Dictionary<string, string>? named =
                string.Equals(label.HandlerType, "GLASSES", StringComparison.OrdinalIgnoreCase)
                    ? new Dictionary<string, string>
                    {
                        ["GLASSES_TITLE"] = "(mẫu)",
                        ["GLASSES_INFO"] = "(mẫu)"
                    }
                    : null;

            bool needsXmlScript = named is { Count: > 0 };

            // Luồng in THẬT: tem cần Named Sub-String + máy không hỗ trợ
            // XMLScript ⇒ báo cần nâng cấp, KHÔNG gọi bartend.exe, KHÔNG popup.
            if (needsXmlScript && cap.XmlScriptSupported == false)
            {
                sb.AppendLine("Backend    : (không in được — thiếu Enterprise Automation)");
                sb.AppendLine("Dùng XMLScript? : KHÔNG (chặn trước khi gọi bartend.exe)");
                sb.AppendLine("→ App sẽ hiện thông báo yêu cầu nâng cấp BarTender, KHÔNG có popup #3112.");
                sb.AppendLine();
                continue;
            }

            try
            {
                string cmd = service.DescribePlannedCommand(
                    label.TemplatePath ?? string.Empty,
                    named,
                    out string backendName);

                bool touchesXmlScript =
                    cmd.Contains("XMLScript", StringComparison.OrdinalIgnoreCase) ||
                    cmd.Contains(".btxml", StringComparison.OrdinalIgnoreCase);

                sb.AppendLine($"Backend    : {backendName}");
                sb.AppendLine($"Dùng XMLScript? : {(touchesXmlScript ? "CÓ" : "KHÔNG")}");
                sb.AppendLine("Lệnh sẽ gửi:");
                sb.AppendLine($"  {cmd.Replace("\n", "\n  ")}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Backend    : (không dựng được lệnh) {ex.Message}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("===========================================================");
        sb.AppendLine("Kỳ vọng: Tem đầy đủ / Tem mã vạch → backend StandardCommandLine, Dùng XMLScript? KHÔNG.");
        sb.AppendLine("         Tem kính → XmlScript (nếu Edition có Enterprise) hoặc báo cần nâng cấp license.");
        sb.AppendLine();

        string output = sb.ToString();
        Console.WriteLine(output);

        try
        {
            string folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);
            File.WriteAllText(
                Path.Combine(folder, "bartender-selftest.log"),
                output);
        }
        catch
        {
            // bỏ qua
        }

        return 0;
    }
}
