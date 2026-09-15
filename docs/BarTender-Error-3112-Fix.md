# Khắc phục BarTender Error #3112 (`XMLScript=` chỉ có ở Enterprise Automation)

## Nguyên nhân gốc

`BarTenderService.Print()` (bản cũ) **luôn** dựng lệnh:

```
bartend.exe /XMLScript="...print_xxx.xml" /X
```

cho **mọi loại tem**, kể cả "Tem đầy đủ" và "Tem mã vạch" — những loại không hề
cần dữ liệu động. Trên máy BarTender không phải bản **Enterprise Automation**,
`/XMLScript=` bị chặn và BarTender bật popup lỗi **#3112**.

Bản vá "capability" trước đó chỉ **phản ứng sau khi** popup đã hiện (đợi timeout
rồi mới đánh dấu không hỗ trợ), nên popup vẫn xuất hiện ở lần in đầu.

## Cách sửa

### 1. Tách 2 backend in — `IBarTenderPrintBackend`

| Backend | Lệnh | Dùng khi | Edition |
|---|---|---|---|
| `StandardCommandLinePrintBackend` | `/F=... /PRN=... /P /X` | Lệnh in **không** có Named Sub-String | **Mọi** edition |
| `XmlScriptPrintBackend` | `/XMLScript="..." /X` | Lệnh in **có** Named Sub-String | Chỉ Enterprise Automation |

`BarTenderService` chọn backend theo **nội dung request**, không theo cờ đoán:

- Không có Named Sub-String → **luôn** `StandardCommandLine`. Không bao giờ chạm
  `/XMLScript` ⇒ không bao giờ có #3112.
  - "Tem đầy đủ" (`FullLabelHandler`), "Tem mã vạch" (`BarcodeLabelHandler`),
    "Tem thường" (`GenericLabelHandler`) — dữ liệu đã được `ExcelService` ghi vào
    file data (`.xls`) mà template `.btw` liên kết sẵn.
- Có Named Sub-String → cần `/XMLScript=`.
  - Chỉ "Tem kính" (`GlassesBarTenderService`) — tiêu đề / block thông tin format
    động, không nằm trong file data.

### 2. Dò edition TRƯỚC khi in (không chờ popup)

`BarTenderCapabilityService.EnsureProbed()` đọc registry:

```
HKLM\SOFTWARE\[WOW6432Node\]Seagull Scientific\BarTender\Licensing\<ver>\Edition
```

- `Edition` chứa "Enterprise" → cho phép `/XMLScript=`.
- Ngược lại → đánh dấu không hỗ trợ ⇒ "Tem kính" báo thông báo rõ ràng
  (`BuildEnterpriseRequiredException`), **không gọi `bartend.exe`, không popup**.
- Đọc registry thất bại → giữ "chưa biết": "Tem kính" thử `/XMLScript=` **một
  lần** với timeout ngắn (12s), nếu treo (nghi popup #3112) thì kill + đánh dấu
  không hỗ trợ. "Tem đầy đủ" / "Tem mã vạch" vẫn không bị ảnh hưởng.

Cache capability được đóng dấu theo **tên máy + đường dẫn BarTender.exe**, nên
config vô tình copy sang máy khác / installer ghi đè sẽ **không** mang theo trạng
thái sai — máy mới tự dò lại.

### 3. Ghi log lệnh thật

Trước **mỗi** `Process.Start`, ghi vào `logs/bartender-command.log`:

```
---- PRINT via StandardCommandLine ----
[...] BARTENDER EXECUTABLE: C:\...\bartend.exe
[...] BARTENDER ARGUMENTS : /F=... /PRN=... /P /X
[...] LABEL TEMPLATE      : ...
[...] NAMED SUBSTRINGS    : (không có)
```

### 4. Phiên bản + chẩn đoán khởi động

- `AppInfo` — version (`<Version>` trong `.csproj`), build timestamp (nhúng lúc
  compile), `Environment.ProcessPath`, `AppContext.BaseDirectory`.
- Hiện trên header màn hình chính và tiêu đề form Cấu hình.
- Ghi `logs/app-startup.log` mỗi lần mở app.

### 5. Selftest (không cần máy in)

```
"KiotViet Label Printer Pro V2.exe" --bartender-selftest
```

In ra backend + lệnh chính xác cho từng loại tem, ghi `logs/bartender-selftest.log`.

## Kiểm tra khi phát hành bản mới

1. Tăng `<Version>` trong `.csproj` **và** `AppVersion` trong `installer.iss`.
2. Chạy `build-installer.bat` (đã tự clean bin/obj/publish + strip artifact runtime).
3. Trên máy đích: mở app → header phải hiện đúng version mới.
4. Chạy `--bartender-selftest` trên máy đích → "Tem đầy đủ" phải là
   `StandardCommandLine` / `Dùng XMLScript? KHÔNG`.
5. Nhấn IN "Tem đầy đủ" → không còn popup #3112.
