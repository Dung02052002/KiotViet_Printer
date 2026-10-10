namespace KiotVietLabelPrinter.Services.Warehouse;

/// <summary>Đơn vị tính đề xuất cho một tên hàng; <see cref="IsAmbiguous"/> = người dùng nên xem lại.</summary>
public sealed record UnitDetection(string Unit, bool IsAmbiguous, string? Reason);

/// <summary>
/// Xác định đơn vị tính theo tên hàng: giày dép (đi theo đôi) tính "Đôi",
/// còn lại (túi, ví, kính, thắt lưng, phụ kiện...) tính "Cái" — đúng như các
/// phiếu xuất kho thực tế.
///
/// Cách nhận diện:
/// - Khớp TỪ KHÓA THEO TỪ (không khớp chuỗi con) trên tên CÒN DẤU. Không bỏ
///   dấu khi tên có dấu, vì bỏ dấu sẽ nhầm "giấy" (Túi giấy) thành "giày",
///   "đẹp" thành "dép".
/// - Chỉ xét phần LOẠI HÀNG ở đầu tên (trước dấu phẩy đầu tiên): tên thực tế
///   có dạng "Giày nữ, mũ giày bằng da...", "Túi xách tay nữ, kt(...)...".
/// - Tên không dấu ("GIAY NU 3233") mới so khớp kiểu không dấu, và những từ
///   có thể hiểu hai nghĩa (giay = giày/giấy, dep = dép/đẹp...) bị đánh dấu
///   mơ hồ để người dùng kiểm tra lại.
/// Danh sách từ khóa để public/static để dễ bổ sung.
/// </summary>
public class WarehouseUnitService
{
    public const string PairUnit = "Đôi";
    public const string PieceUnit = "Cái";

    /// <summary>Từ khóa (chữ thường, CÓ dấu) của hàng tính theo đôi. Cụm nhiều từ viết cách nhau một dấu cách.</summary>
    public static readonly IReadOnlyList<string> PairKeywords =
    [
        "giày", "giầy", "dép", "guốc", "sandal", "sandals", "xăng đan", "xăng-đan",
        "boot", "boots", "bốt", "ủng"
    ];

    /// <summary>
    /// Cụm từ thông dụng bắt đầu bằng một từ khóa nhưng KHÔNG phải giày dép
    /// ("ủng hộ" = hỗ trợ) — gặp cụm này thì bỏ qua từ khóa đó.
    /// </summary>
    public static readonly IReadOnlyList<string> PairKeywordExceptions =
    [
        "ủng hộ"
    ];

    /// <summary>Từ khóa không dấu chắc chắn là giày dép (không trùng nghĩa với từ thông dụng khác).</summary>
    public static readonly IReadOnlyList<string> UnaccentedPairKeywords =
    [
        "sandal", "sandals", "xang dan", "boot", "boots", "guoc"
    ];

    /// <summary>Từ khóa không dấu có thể là giày dép nhưng cũng có thể là từ khác.</summary>
    public static readonly IReadOnlyDictionary<string, string> AmbiguousUnaccentedKeywords = new Dictionary<string, string>
    {
        ["giay"] = "giày hoặc giấy",
        ["giays"] = "giày",
        ["dep"] = "dép hoặc đẹp",
        ["bot"] = "bốt hoặc bột",
        ["ung"] = "ủng hoặc từ khác"
    };

    /// <summary>
    /// Từ đứng đầu loại hàng cho biết đây là món đồ đi riêng lẻ dù có nhắc
    /// tới giày ("Túi đựng giày", "Dây giày", "Lót giày"...). So sánh không dấu.
    /// </summary>
    public static readonly IReadOnlySet<string> SingleItemLeadWords = new HashSet<string>
    {
        "tui", "vi", "kinh", "that", "day", "hop", "lot", "xi", "ban", "khan", "mu", "non"
    };

    public UnitDetection Detect(string? productName)
    {
        string name = VietnameseText.CollapseSpaces(productName);
        if (name.Length == 0)
            return new(PieceUnit, true, "Chưa có tên hàng để xác định đơn vị.");

        string head = ProductTypeSegment(name);
        string tail = name.Length > head.Length ? name[head.Length..] : "";

        return VietnameseText.HasDiacritics(name)
            ? DetectAccented(head, tail)
            : DetectUnaccented(head, tail);
    }

    private static UnitDetection DetectAccented(string head, string tail)
    {
        List<string> headWords = VietnameseText.Words(VietnameseText.Lower(head));
        string? keyword = FindKeyword(headWords, PairKeywords);

        if (keyword != null)
        {
            string lead = VietnameseText.Fold(headWords[0]);
            if (SingleItemLeadWords.Contains(lead) && !IsKeywordAt(headWords, 0, keyword))
                return new(PieceUnit, true, $"Tên bắt đầu bằng \"{headWords[0]}\" nhưng có nhắc \"{keyword}\" — kiểm tra lại đơn vị.");

            return new(PairUnit, false, null);
        }

        string? tailKeyword = FindKeyword(VietnameseText.Words(VietnameseText.Lower(tail)), PairKeywords);
        if (tailKeyword != null)
            return new(PieceUnit, true, $"Phần mô tả có nhắc \"{tailKeyword}\" nhưng loại hàng không phải giày dép — kiểm tra lại đơn vị.");

        return new(PieceUnit, false, null);
    }

    private static UnitDetection DetectUnaccented(string head, string tail)
    {
        List<string> headWords = VietnameseText.Words(VietnameseText.Fold(head));
        if (FindKeyword(headWords, UnaccentedPairKeywords) != null)
            return new(PairUnit, false, null);

        foreach ((string word, string meaning) in AmbiguousUnaccentedKeywords)
        {
            int index = headWords.IndexOf(word);
            if (index < 0)
                continue;

            // "GIAY NU 3233" (từ đầu tên) gần như chắc là giày; "TUI GIAY" là túi giấy.
            string unit = index == 0 ? PairUnit : PieceUnit;
            return new(unit, true, $"Tên không dấu: \"{word}\" có thể là {meaning} — kiểm tra lại đơn vị.");
        }

        string? tailKeyword = FindKeyword(VietnameseText.Words(VietnameseText.Fold(tail)), UnaccentedPairKeywords);
        if (tailKeyword != null)
            return new(PieceUnit, true, $"Phần mô tả có nhắc \"{tailKeyword}\" — kiểm tra lại đơn vị.");

        return new(PieceUnit, false, null);
    }

    /// <summary>Phần loại hàng ở đầu tên: trước dấu phẩy đầu tiên, hoặc cả tên nếu không có.</summary>
    private static string ProductTypeSegment(string name)
    {
        int comma = name.IndexOf(',');
        return comma > 0 ? name[..comma] : name;
    }

    private static string? FindKeyword(List<string> words, IEnumerable<string> keywords)
    {
        foreach (string keyword in keywords)
        {
            for (int i = 0; i < words.Count; i++)
            {
                if (IsKeywordAt(words, i, keyword) && !PairKeywordExceptions.Any(e => IsKeywordAt(words, i, e)))
                    return keyword;
            }
        }

        return null;
    }

    // Từ khóa nhiều từ ("xăng đan") phải khớp các từ liên tiếp.
    private static bool IsKeywordAt(List<string> words, int index, string keyword)
    {
        string[] parts = keyword.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (index + parts.Length > words.Count)
            return false;

        for (int k = 0; k < parts.Length; k++)
        {
            if (!string.Equals(words[index + k], parts[k], StringComparison.Ordinal))
                return false;
        }

        return true;
    }
}
