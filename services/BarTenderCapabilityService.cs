using System.Diagnostics;
using Microsoft.Win32;
using KiotVietLabelPrinter.Models;

namespace KiotVietLabelPrinter.Services;

/// <summary>
/// Theo dõi máy hiện tại có dùng được /XMLScript= (Enterprise Automation)
/// hay không. Không tự spawn tiến trình BarTender để dò — trạng thái chỉ
/// được xác nhận dựa trên kết quả của một lần in thật (xem
/// BarTenderService.Print), để không tạo thêm rủi ro/popup ngoài luồng in
/// bình thường. Mặc định (null) = chưa biết, luồng gọi vẫn dùng XMLScript
/// y như trước — không đổi hành vi trên máy đang chạy tốt.
/// </summary>
public class BarTenderCapabilityService
{
    private static BarTenderCapabilityService? _instance;
    public static BarTenderCapabilityService Instance => _instance ??= new BarTenderCapabilityService();

    private BarTenderCapabilityService()
    {
        LoadFromConfig();
    }

    public bool? XmlScriptSupported { get; private set; }
    public string? Detail { get; private set; }

    /// <summary>Edition đọc được từ registry lần dò gần nhất (nếu có).</summary>
    public string? DetectedEdition { get; private set; }

    private bool _probedThisSession;

    /// <summary>
    /// Dò edition BarTender qua registry (1 lần / phiên chạy) để biết TRƯỚC
    /// có dùng được /XMLScript= hay không — không phải chờ popup #3112 rồi
    /// timeout. Chỉ ĐẶT trạng thái khi registry cho câu trả lời chắc chắn;
    /// đọc không được thì để nguyên (null = chưa biết, luồng gọi tự xử lý).
    /// </summary>
    public void EnsureProbed(string? bartenderExe)
    {
        if (_probedThisSession)
            return;

        _probedThisSession = true;

        string? edition = TryReadEditionFromRegistry();
        DetectedEdition = edition;

        if (string.IsNullOrWhiteSpace(edition))
            return; // không đọc được → giữ trạng thái hiện tại

        bool supportsXmlScript = EditionSupportsXmlScript(edition);

        PrintDiagnosticsLog.Write(
            $"CAPABILITY probe edition=\"{edition}\" supportsXmlScript={supportsXmlScript}");

        if (supportsXmlScript)
        {
            // Chỉ nâng lên "supported" khi trước đó chưa bị xác nhận là false
            // bởi một lần in thật (kết quả in thật đáng tin hơn suy đoán registry).
            if (XmlScriptSupported != false)
                MarkSupported();
        }
        else
        {
            MarkUnsupported($"Registry BarTender Edition = \"{edition}\" — không phải Enterprise Automation.");
        }
    }

    /// <summary>
    /// XMLScript qua command line yêu cầu "Enterprise Automation edition or
    /// better" (đúng theo thông báo lỗi #3112). Các tên edition có "Enterprise"
    /// đều thoả; "Automation" (không Enterprise), "Professional", "Starter",
    /// "Basic", "Trial (Professional)"… đều không.
    /// </summary>
    private static bool EditionSupportsXmlScript(string edition)
    {
        string lower = edition.ToLowerInvariant();
        return lower.Contains("enterprise");
    }

    /// <summary>
    /// Đọc HKLM\SOFTWARE\[WOW6432Node\]Seagull Scientific\BarTender\Licensing\
    /// &lt;version&gt;\Edition. Quét cả 2 view registry (32/64-bit) và mọi subkey
    /// version, lấy giá trị Edition cuối cùng tìm thấy.
    /// </summary>
    private static string? TryReadEditionFromRegistry()
    {
        string? found = null;

        foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using RegistryKey? licensing = baseKey.OpenSubKey(
                    @"SOFTWARE\Seagull Scientific\BarTender\Licensing");

                if (licensing == null)
                    continue;

                foreach (string versionSub in licensing.GetSubKeyNames())
                {
                    using RegistryKey? versionKey = licensing.OpenSubKey(versionSub);
                    if (versionKey?.GetValue("Edition") is string ed && !string.IsNullOrWhiteSpace(ed))
                        found = ed.Trim();
                }
            }
            catch
            {
                // Không đọc được view này — thử view kế tiếp.
            }
        }

        return found;
    }

    public void MarkSupported()
    {
        if (XmlScriptSupported == true)
            return;

        XmlScriptSupported = true;
        Detail = null;
        SaveToConfig();
    }

    public void MarkUnsupported(string reason)
    {
        if (XmlScriptSupported == false)
            return;

        XmlScriptSupported = false;
        Detail = reason;
        SaveToConfig();

        PrintDiagnosticsLog.Write(
            $"CAPABILITY XMLScript=false reason={reason}");
    }

    /// <summary>
    /// Xoá trạng thái đã ghi nhớ — dùng khi người dùng vừa nâng cấp/cài lại
    /// BarTender và muốn app tự dò lại ở lần in kế tiếp.
    /// </summary>
    public void Reset()
    {
        XmlScriptSupported = null;
        Detail = null;
        DetectedEdition = null;
        _probedThisSession = false;

        AppConfig config = ConfigService.Instance.Config;
        config.BarTenderCapabilityExePath = null;
        config.BarTenderCapabilityMachine = null;
        config.BarTenderXmlScriptSupported = null;
        config.BarTenderXmlScriptDetail = null;
        ConfigService.Instance.Save();
    }

    /// <summary>
    /// Cụm dấu hiệu lỗi giới hạn edition của BarTender (lỗi #3112 và các
    /// biến thể thông báo liên quan) — chỉ đọc metadata/text, không suy
    /// đoán dựa trên timeout một mình.
    /// </summary>
    public static bool LooksLikeEditionGateError(string? stdErr, string? stdOut)
    {
        string combined = $"{stdErr} {stdOut}";

        if (string.IsNullOrWhiteSpace(combined))
            return false;

        string lower = combined.ToLowerInvariant();

        return lower.Contains("3112") ||
               lower.Contains("enterprise automation") ||
               (lower.Contains("xmlscript") && lower.Contains("only available")) ||
               (lower.Contains("automation option") && lower.Contains("edition"));
    }

    /// <summary>
    /// Thông báo thân thiện khi loại tem cần dữ liệu động (NamedSubString)
    /// nhưng máy không hỗ trợ /XMLScript= — không lộ chi tiết kỹ thuật,
    /// không phải popup gốc của BarTender.
    /// </summary>
    public static Exception BuildEnterpriseRequiredException()
    {
        return new Exception(
            "Không thể in loại tem này: bản BarTender trên máy không hỗ trợ " +
            "tính năng Automation nâng cao (Enterprise Automation) mà loại tem " +
            "này cần để tự động điền nội dung động (ví dụ: tiêu đề / thông tin " +
            "tem kính).\n\n" +
            "Cách khắc phục:\n" +
            "1) Nâng cấp BarTender lên bản Enterprise Automation, hoặc\n" +
            "2) Liên hệ bộ phận kỹ thuật để cấu hình lại template dùng nguồn " +
            "dữ liệu từ file thay vì nhập trực tiếp.\n\n" +
            "Các loại tem khác (Tem đầy đủ, Tem mã vạch, Tem thường) vẫn in " +
            "bình thường trên máy này.");
    }

    /// <summary>
    /// Đọc version file BarTender.exe — chỉ đọc metadata, không chạy tiến
    /// trình, an toàn tuyệt đối để gọi bất cứ lúc nào (kể cả lúc mở form
    /// cấu hình).
    /// </summary>
    public static string? TryGetVersion(string bartenderExe)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(bartenderExe) || !File.Exists(bartenderExe))
                return null;

            FileVersionInfo info = FileVersionInfo.GetVersionInfo(bartenderExe);

            return info.ProductVersion?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private void LoadFromConfig()
    {
        AppConfig config = ConfigService.Instance.Config;

        string? cachedExe = config.BarTenderCapabilityExePath;
        string currentExe = config.BarTenderExe ?? string.Empty;
        string? cachedMachine = config.BarTenderCapabilityMachine;

        // Cache chỉ đáng tin khi:
        //   - đường dẫn BarTender.exe chưa đổi (không cài lại/nâng cấp), VÀ
        //   - đúng máy đã ghi cache (config copy sang máy khác / installer ghi
        //     đè sẽ có tên máy khác → bỏ qua, dò lại từ đầu).
        if (string.IsNullOrWhiteSpace(cachedExe) ||
            !string.Equals(cachedExe, currentExe, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(cachedMachine, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        XmlScriptSupported = config.BarTenderXmlScriptSupported;
        Detail = config.BarTenderXmlScriptDetail;
    }

    private void SaveToConfig()
    {
        AppConfig config = ConfigService.Instance.Config;

        config.BarTenderCapabilityExePath = config.BarTenderExe;
        config.BarTenderCapabilityMachine = Environment.MachineName;
        config.BarTenderXmlScriptSupported = XmlScriptSupported;
        config.BarTenderXmlScriptDetail = Detail;

        ConfigService.Instance.Save();
    }
}
