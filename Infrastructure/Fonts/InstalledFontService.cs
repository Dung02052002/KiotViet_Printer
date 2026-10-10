using System.Drawing.Text;
using SkiaSharp;

namespace KiotVietLabelPrinter.Infrastructure.Fonts;

/// <summary>Kết quả kiểm tra một font trước khi áp dụng lên phiếu.</summary>
public sealed record FontCheck(bool IsInstalled, bool SupportsVietnamese);

/// <summary>
/// Danh sách font đã cài trên Windows + kiểm tra font có đủ chữ tiếng Việt
/// (tránh in ra ô vuông thay cho "ữ", "ặ"...). Chỉ đọc — không cài font.
/// </summary>
public class InstalledFontService
{
    // Đủ các nguyên âm có dấu + đ/ơ/ư — font thiếu bất kỳ ký tự nào thì cảnh báo.
    public const string VietnameseSample =
        "ĂÂĐÊÔƠƯăâđêôơư" +
        "ÁÀẢÃẠẮẰẲẴẶẤẦẨẪẬÉÈẺẼẸẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÝỲỶỸỴ" +
        "áàảãạắằẳẵặấầẩẫậéèẻẽẹếềểễệíìỉĩịóòỏõọốồổỗộớờởỡợúùủũụứừửữựýỳỷỹỵ";

    public List<string> GetInstalledFamilies()
    {
        using InstalledFontCollection installed = new();
        return installed.Families
            .Select(f => f.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public FontCheck Check(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
            return new FontCheck(false, false);

        bool installed = GetInstalledFamilies().Contains(family, StringComparer.OrdinalIgnoreCase);
        if (!installed)
            return new FontCheck(false, false);

        // FromFamilyName trả font thay thế khi không tìm thấy — chỉ tin khi tên khớp.
        using SKTypeface? typeface = SKTypeface.FromFamilyName(family);
        bool vietnamese = typeface != null &&
                          string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase) &&
                          typeface.ContainsGlyphs(VietnameseSample);
        return new FontCheck(true, vietnamese);
    }
}
