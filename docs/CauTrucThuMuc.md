# Cấu trúc thư mục

Chia theo **tầng** trước, rồi theo **tính năng** trong mỗi tầng. Namespace luôn
trùng đường dẫn thư mục (`KiotVietLabelPrinter.<Tầng>.<Tính năng>`) — test
`ArchitectureTests.NamespaceMatchesFolder` kiểm tra điều này.

| Tầng | Vai trò | Được phụ thuộc vào |
|---|---|---|
| `Forms/` | Cửa sổ (Form) | Views, UI, Services, Infrastructure, Models |
| `Views/` | Màn hình công cụ nhúng trong FormMain | UI, Services, Infrastructure, Models |
| `UI/` | Theme và control tự vẽ dùng chung | — |
| `Services/` | Nghiệp vụ (parser, quy tắc, điều phối) — không hiện UI | Infrastructure, Models |
| `Infrastructure/` | Đọc/ghi file, Excel, BarTender, máy in, log | Services, Models |
| `Models/` | Dữ liệu thuần, không phụ thuộc tầng nào | — |

## Tính năng

| Tính năng | Forms | Models | Services | Infrastructure |
|---|---|---|---|---|
| In tem (đầy đủ / mã vạch / thường) | `Forms/Labels` | `Models/Labels` | `Services/Labels` | `Excel/ExcelService`, `BarTender/` |
| Tem kính | `Forms/Glasses` (Parser Lab) | `Models/Glasses` | `Services/Glasses` | `Excel/GlassesExcelService`, `BarTender/GlassesBarTenderService` |
| Tạo mã vạch | — (`Views/BarcodeGeneratorView`) | `Models/Barcodes` | `Services/Barcodes` | `Printing/BarcodePrintService` |
| Phiếu xuất kho | `Forms/Warehouse` | `Models/Warehouse` | `Services/Warehouse` | `Excel/Warehouse*`, `Printing/` |
| Nén ảnh | — (`Views/ImageCompressorView`) | `Models/ImageTools` | `Services/ImageTools` | — |
| Xóa nền | — (`Views/BackgroundRemoverView`) | `Models/BackgroundRemoval` | `Services/BackgroundRemoval` | `Diagnostics/BackgroundRemovalDiagnosticsLog` |

Dùng chung toàn app (để ở gốc tầng): `Forms/FormMain`, `Forms/FormConfig`,
`Forms/ToastForm`, `Models/AppConfig`, `Models/ToolDefinition`.

`UI/Theme` = màu, font, icon, hiệu ứng; `UI/Controls` = control tự vẽ.

`Services/Glasses/Samples` là bộ mẫu tên hàng → mã mong đợi, dùng cho Parser
Lab trong app và cho test hồi quy (`tests/KiotVietLabelPrinter.Tests`).
