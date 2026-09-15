using System.Text;

namespace KiotVietLabelPrinter.Services.BarTenderBackends;

/// <summary>
/// Dựng file BTXML (Print XML Script) cho <see cref="XmlScriptPrintBackend"/>.
///
/// XML này làm ĐÚNG các việc sau (đã rà lại toàn bộ):
///   - &lt;Format&gt;            → mở file .btw template
///   - &lt;PrintSetup&gt;&lt;Printer&gt; → chọn máy in
///   - &lt;EnablePrompting&gt;false  → KHÔNG hiện preview / hộp thoại nhập tay
///   - &lt;NamedSubString&gt;        → bơm dữ liệu động vào object trên template
///
/// Những việc XML này KHÔNG làm (nên command line chuẩn thay thế được khi
/// không có Named Sub-String): số lượng bản in (lấy theo cột số lượng trong
/// file data), preview, export ảnh, kết nối database (database connection đã
/// lưu sẵn trong .btw), chọn record range.
/// </summary>
public static class PrintXmlBuilder
{
    /// <summary>Dựng nội dung XML (không ghi file).</summary>
    public static string BuildXml(
        string btwFile,
        IReadOnlyDictionary<string, string>? namedSubStrings,
        string printerName)
    {
        StringBuilder sb = new();

        sb.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
        sb.AppendLine("""<XMLScript Version="2.0">""");
        sb.AppendLine("""  <Command>""");
        sb.AppendLine("""    <Print>""");
        sb.AppendLine($"      <Format>{EscapeXml(btwFile)}</Format>");
        sb.AppendLine("""      <PrintSetup>""");
        sb.AppendLine($"        <Printer>{EscapeXml(printerName)}</Printer>");
        sb.AppendLine("""        <EnablePrompting>false</EnablePrompting>""");
        sb.AppendLine("""      </PrintSetup>""");

        if (namedSubStrings != null && namedSubStrings.Count > 0)
        {
            foreach (KeyValuePair<string, string> kv in namedSubStrings)
            {
                if (string.IsNullOrWhiteSpace(kv.Key))
                    continue;

                string value = kv.Value ?? string.Empty;

                sb.AppendLine($"      <NamedSubString Name=\"{EscapeXml(kv.Key)}\">");
                sb.AppendLine($"        <Value>{EscapeXml(value)}</Value>");
                sb.AppendLine("      </NamedSubString>");
            }
        }

        sb.AppendLine("""    </Print>""");
        sb.AppendLine("""  </Command>""");
        sb.AppendLine("""</XMLScript>""");

        return sb.ToString();
    }

    /// <summary>
    /// Dựng XML rồi ghi ra &lt;app&gt;\debug_xml\print_&lt;timestamp&gt;.xml, trả về đường
    /// dẫn file. Cũng ghi đè debug_xml\last_print_debug.xml để tiện xem nhanh.
    /// </summary>
    public static string WriteXmlNearApp(
        string btwFile,
        IReadOnlyDictionary<string, string>? namedSubStrings,
        string printerName)
    {
        string appFolder = AppContext.BaseDirectory;
        string debugFolder = Path.Combine(appFolder, "debug_xml");
        Directory.CreateDirectory(debugFolder);

        string xmlPath = Path.Combine(
            debugFolder,
            $"print_{DateTime.Now:yyyyMMdd_HHmmss_fff}.xml");

        string xml = BuildXml(btwFile, namedSubStrings, printerName);

        using (FileStream fs = new(xmlPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (StreamWriter writer = new(fs, new UTF8Encoding(false)))
        {
            writer.Write(xml);
            writer.Flush();
            fs.Flush(true);
        }

        File.WriteAllText(
            Path.Combine(debugFolder, "last_print_debug.xml"),
            xml,
            new UTF8Encoding(false));

        return xmlPath;
    }

    public static string SafeReadAllText(string path)
    {
        try
        {
            if (!File.Exists(path))
                return "(Không tìm thấy file XML)";

            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return $"(Không đọc được file XML: {ex.Message})";
        }
    }

    private static string EscapeXml(string value)
    {
        return System.Security.SecurityElement.Escape(value) ?? value;
    }
}
