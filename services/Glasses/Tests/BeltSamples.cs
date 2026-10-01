namespace KiotVietLabelPrinter.Services.Glasses.Tests;

// Test case tem thắt lưng (xem BeltCodeExtractor). Dùng cho cả GlassesParser
// (Run All ở Parser Lab) và BarcodeParser — hai parser phải ra cùng một mã.
public static class BeltSamples
{
    public static List<ParserTestCase> Get()
    {
        return
        [
            new() { Name="BELT 1. TL-MAU dính số", Input="THẮT LƯNG NAM TL10 Mẫu01 - Da bò", Expected="TL10 Mẫu01" },
            new() { Name="BELT 1. TL-MAU không NAM", Input="THẮT LƯNG TL25 Mẫu03", Expected="TL25 Mẫu03" },
            new() { Name="BELT 1. TL-MAU cách số", Input="THẮT LƯNG NAM TL14 MẪU 1 - VUÔNG (TLC)", Expected="TL14 MẪU 1" },
            new() { Name="BELT 1. TL-MAU 2 chữ số", Input="THẮT LƯNG NAM TL14 MẪU 47 - VUÔNG (TLC)", Expected="TL14 MẪU 47" },
            new() { Name="BELT 1. TL-MAU tiền tố PCN-", Input="THẮT LƯNG NAM PCN-TL15 MẪU 9 - VUÔNG (TLC)", Expected="TL15 MẪU 9" },
            new() { Name="BELT 1. TL-MAU thắt lưng nữ", Input="THẮT LƯNG NỮ TL16 MẪU 1 - VUÔNG - Da A (TLC)", Expected="TL16 MẪU 1" },
            new() { Name="BELT 1. TL-MAU có TLC phía sau", Input="THẮT LƯNG NAM TL14 MẪU 35 - TLC (Chiếc)", Expected="TL14 MẪU 35" },
            new() { Name="BELT 1. TL-MAU chữ thường", Input="thắt lưng nam tl10 mẫu01 - da bò", Expected="tl10 mẫu01" },
            new() { Name="BELT 1. TL-MAU không dấu", Input="THAT LUNG NAM TL10 MAU 01", Expected="TL10 MAU 01" },
            new() { Name="BELT 1. TL-MAU khoảng trắng đầu/cuối", Input="   THẮT LƯNG TL25 Mẫu03   ", Expected="TL25 Mẫu03" },
            new() { Name="BELT 1. TL-MAU thắng PUTL", Input="THẮT LƯNG PUTL01 TL10 Mẫu02", Expected="TL10 Mẫu02" },
            new() { Name="BELT 1. TL-MAU có model/mã khác", Input="THẮT LƯNG NAM model 6282 TL10 Mẫu05", Expected="TL10 Mẫu05" },
            new() { Name="BELT 2. PUTL có mô tả", Input="THẮT LƯNG PUTL01 - Da A", Expected="PUTL01" },
            new() { Name="BELT 2. PUTL thắt lưng nam", Input="THẮT LƯNG NAM PUTL25", Expected="PUTL25" },
            new() { Name="BELT 2. PUTL thắng TLxx", Input="THẮT LƯNG NAM PUTL05 TL10", Expected="PUTL05" },
            new() { Name="BELT 2. PUTL không bị hiểu là TL Mẫu", Input="THẮT LƯNG PUTL01 Mẫu02", Expected="PUTL01" },
            new() { Name="BELT 3. TL có XIÊN", Input="THẮT LƯNG NAM TL10 - Da A - XIÊN", Expected="TL10" },
            new() { Name="BELT 3. TL có Da B", Input="THẮT LƯNG NAM TL25 - Da B", Expected="TL25" },
            new() { Name="BELT 3. TL tiền tố PCN-", Input="THẮT LƯNG NAM PCN-TL15 - VUÔNG", Expected="TL15" },
            new() { Name="BELT 3. TL có TLC phía sau", Input="THẮT LƯNG NAM TL10 - Da A (TLC)", Expected="TL10" },
            new() { Name="BELT 4. Không mã có XIÊN", Input="THẮT LƯNG NAM - Da A - XIÊN", Expected="Thắt lưng" },
            new() { Name="BELT 4. Không mã chỉ có TLC", Input="THẮT LƯNG DA BÒ (TLC)", Expected="Thắt lưng" },
            new() { Name="BELT 5. Không phải thắt lưng, dây đồng hồ TLxx", Input="DÂY ĐỒNG HỒ TL20 - BLACK", Expected="TL20" },
            new() { Name="BELT 5. Không phải thắt lưng, kính model", Input="MODEL 6282", Expected="6282" }
        ];
    }
}
