# Nâng cấp pipeline Background Remover

Ngày: 2026-09-08 · Chạy hoàn toàn local bằng C# + ONNX Runtime (không Python, không API, không cloud).

---

## 1. Model: cũ → mới

| | Model cũ | Model mới |
|---|---|---|
| Segmentation | `isnet-general-use.onnx` — IS-Net / DIS, ~171 MB, đóng gói sẵn qua Git LFS | **`BiRefNet-lite`** (BiRefNet, backbone Swin-V1-Tiny) — `BiRefNet-general-bb_swin_v1_tiny-epoch_232.onnx`, 224 MB |
| Matting (mode ULTRA) | *(không có)* | **`BiRefNet-matting`** (trimap-free, alpha liên tục) — `birefnet-matting.onnx`, 941 MB |
| Dự phòng offline | — | `isnet-general-use.onnx` giữ nguyên trong `assets/Models`, tự dùng nếu chưa tải được BiRefNet |

**Phân phối:** model BiRefNet **tải tự động lần đầu** về `%LOCALAPPDATA%\KiotVietLabelPrinter\Models`,
verify **SHA-256** + kích thước, ghi `.part` rồi `File.Move` atomic. Nguồn chính GitHub (rembg release) /
HuggingFace, có URL dự phòng. Sau khi tải xong, mọi suy luận **offline hoàn toàn** — không tải lại.

- BiRefNet-lite SHA-256 `5600024376f572a557870a5eb0afb1e5961636bef4e1e22132025467d0f03333`
- BiRefNet-matting SHA-256 `f0843e38f6a4e88efc8c5fad4178ad7ed6c818346ce12f82e7b579324fe7e0c5`

Vì sao BiRefNet-lite chứ không phải BiRefNet-general (full Swin-Large, ~1 GB): full nhanh nhất cũng
15–30 s/ảnh **trên GPU**, còn CPU thì hàng phút — không dùng được cho batch. Lite (Swin-Tiny) giữ được
gần hết chất lượng biên (tóc/lông/vải) mà chạy được trên CPU.

---

## 2. Preprocessing

Theo đúng `rembg` `BiRefNetSession` + `BaseSession.normalize` (không đoán tham số):

1. RGB → resize **1024×1024** (Mitchell cubic).
2. Chia pixel cho **255** (BiRefNet — đúng chuẩn `ToTensor` gốc; IS-Net dự phòng vẫn chia cho `max(pixel)` như rembg).
3. Chuẩn hoá ImageNet per-channel: `mean = (0.485, 0.456, 0.406)`, `std = (0.229, 0.224, 0.225)`.
4. Layout CHW → tensor `float32 [1, 3, 1024, 1024]`.
5. Output: lấy `output[0]`, **channel 0**.
6. Hậu xử lý ra soft alpha:
   - **segmentation (lite):** `sigmoid(x)` → min-max normalize toàn cục → `[0..1]`.
   - **matting:** `sigmoid(x)` → **chỉ clamp** `[0..1]` (giữ nguyên độ mềm, không kéo giãn tương phản).
7. **Không hard threshold** — alpha giữ mềm suốt pipeline.
8. Alpha resize chính xác về **resolution ảnh gốc** bằng Mitchell cubic; ảnh output PNG đúng `W×H` gốc.
   `InferenceSession` tạo **một lần** trong `EnsureLoaded`, tái dùng cho cả batch — không new session mỗi ảnh.

---

## 3. Mask Refinement (mode HIGH + ULTRA)

Chạy ở working-resolution 1024, thứ tự đúng yêu cầu (`services/BackgroundRemoval/MaskRefiner.cs`):

1. **clamp `[0..1]`**.
2. **Khử vùng cô lập nhỏ:** gán nhãn thành phần liên thông 8-hướng (flood-fill), xoá blob foreground
   `< 0.03%` diện tích ảnh **và** nằm tách xa chủ thể chính (giữ lại dây treo / khoá móc dù bị cắt rời).
3. **Lấp lỗ nhỏ:** vùng nền kín lọt trong foreground, không chạm biên ảnh, `< 0.4%` diện tích → kéo alpha lên ~1.
4. **Làm mượt biên edge-aware rất nhẹ:** cross-bilateral bán kính 2, guide = ảnh xám 1024, chỉ áp trong dải
   biên (nới ~3 px). Khử răng cưa / bậc thang mà **không** làm nhoè các sợi lông (trọng số theo màu guide
   giữ được chi tiết texture).
5. **Feather ở resolution cuối:** box blur bán kính **1–2 px** (Ultra dùng 1 px), **chỉ** áp ở dải bán trong
   suốt (trọng số smoothstep, cực đại quanh `a≈0.5`, bằng 0 ở `a=0` và `a=1`).

**Không Gaussian blur mạnh toàn mask** ở bất kỳ bước nào.

---

## 4. Foreground Edge Color Decontamination (mode HIGH + ULTRA)

`services/BackgroundRemoval/EdgeDecontaminator.cs`. Pixel biên: `C = a·F + (1−a)·B` (B = màu nền cũ).

1. Chỉ xử lý dải **`0.05 < a < 0.95`** — foreground chắc chắn (`a ≥ 0.95`) **không đụng màu**.
2. Lan truyền màu bằng **BFS đa nguồn** (theo khoảng cách):
   - `F_local` = màu foreground thật gần nhất (seed: `a ≥ 0.95`).
   - `B_local` = màu nền cũ gần nhất (seed: `a ≤ 0.05`).
3. Unmix rồi ổn định hoá:
   `F_unmix = (C − (1−a)·B_local) / max(a, 0.15)` → `F_est = lerp(F_unmix, F_local, 0.25)`.
4. **Chống halo:** chặn mỗi kênh `|F_est − F_local| ≤ 60` (không dark/white halo); không bao giờ nội suy về
   trắng — chỉ về màu foreground thật lân cận (không gray halo); trọng số smoothstep → 0 khi `a → 0.985` để
   chính bước khử không tạo viền.
5. Composite: `out = F_est·a + 255·(1−a)`.

**Kết quả đo (test nền xanh tổng hợp):** vệt màu nền rò rỉ ở biên giảm rõ rệt — xem `REPORT_decontam_proof.png`
(bản đồ nhiệt "độ xanh rò rỉ": FAST có vòng xanh liền mạch quanh toàn bộ biên → HIGH gần như sạch).

---

## 5. Khác biệt FAST / HIGH / ULTRA

| | FAST | HIGH QUALITY *(mặc định)* | ULTRA |
|---|---|---|---|
| Segmentation | BiRefNet-lite | BiRefNet-lite | BiRefNet-lite |
| Mask refinement | — | ✅ (khử đốm, lấp lỗ, mượt biên, feather) | ✅ |
| Edge decontamination | — | ✅ | ✅ |
| Pass matting thứ 2 | — | — | ✅ BiRefNet-matting |
| Cách ULTRA dùng matting | | | segmentation → **trimap** (`a≥0.92` FG, `a≤0.08` BG, còn lại unknown, nới 8 px) → chạy matting → **chỉ** lấy alpha matting ở vùng **unknown**, FG/BG giữ nguyên |
| Model tải về | lite (214 MB) | lite (214 MB) | lite + matting (**+897 MB**) |
| Dùng khi | xem nhanh / số lượng lớn | **đa số trường hợp** | lông/tóc rất mảnh, biên khó, cần tối đa |

> ULTRA hiện chạy matting trên **toàn ảnh** ở 1024² (một lần suy luận) rồi hợp nhất theo trimap. Cắt sát
> bounding-box chủ thể trước khi matting là tối ưu **chất lượng** khả dĩ về sau (không giảm thời gian vì
> cost cố định theo resolution model).

---

## 6. Thời gian trung bình mỗi ảnh

Đo thực trên **CPU Intel i5-12400** (6 nhân / 12 luồng), ảnh 800×800 – 1280×960, model inference cố định 1024².

| Mode | Preprocess | Inference (lite) | Matting | Refinement | Decontam | Composite + Save | **Tổng / ảnh** |
|---|---|---|---|---|---|---|---|
| FAST | ~35 ms | 7–9 s | — | — | — | ~60 ms | **≈ 7–9 s** |
| HIGH | ~40 ms | 7–9 s | — | ~100–230 ms | ~30–90 ms | ~70 ms | **≈ 7.5–9.5 s** |
| ULTRA | ~70 ms | 7–20 s | **30–50 s** | ~150–350 ms | ~40–100 ms | ~90 ms | **≈ 38–70 s** |

- Thời gian dao động khi chạy batch dài: CPU 65 W throttle nhiệt sau vài chục giây tải nặng (rõ nhất ở ULTRA).
- **GPU / DirectML:** code có sẵn đường DirectML (thử → xác minh bằng ORT profiling trace thật → tự fallback CPU
  nếu không chạy được). Trên máy test (Intel UHD 730 tích hợp) DirectML **báo hết bộ nhớ** với BiRefNet nên
  fallback CPU — **giống hệt** model IS-Net cũ trước đây trên máy này (log `2026-09-07` cũng là `Provider: CPU`).
  Trên GPU rời đủ VRAM, inference thường nhanh hơn CPU **~5–15×** (ULTRA sẽ về mức vài giây/ảnh).
- Model **không nạp lại** giữa các ảnh — xác nhận trong log: mỗi batch chỉ có **một** dòng `ONNX [...] Provider`.
- UI không đứng (toàn bộ chạy `Task.Run`), **DỪNG được ở mọi bước** kể cả lúc đang tải model
  (`CancellationToken` xuyên suốt download → inference → refine → matting → composite → save).

---

## 7. Preview — kiểm tra chất lượng mask

Nền trắng trên nền trắng thì **không đánh giá được gì**. Preview có 4 chế độ, đổi qua lại
giữ nguyên zoom/pan để so cùng một vùng biên:

| Chế độ | Nguồn dữ liệu |
|---|---|
| **Ảnh gốc** | file đầu vào |
| **Mask AI** | `BgRemovalInspection.FinalMask` — alpha **cuối cùng** (sau refine + feather), gray8: trắng = FG, đen = BG, xám = biên mềm |
| **Nền caro** | `BgRemovalInspection.Cutout` (foreground đã khử nhiễm + alpha cuối) vẽ trên caro trắng/xám kiểu Photoshop — **chỉ để soi biên, không phải file xuất** |
| **Kết quả** | file PNG xuất thật, nền `RGB(255,255,255)` |

Mask hiển thị là **đúng alpha truyền cho `CompositeOnWhite`**, không phải bản dựng lại.

Zoom **Fit / 100% / 200% / 400%**, con lăn chuột zoom tự do quanh con trỏ, kéo chuột trái để pan,
nháy đúp đổi nhanh Fit ↔ 100% (`UI/ZoomPanImageView.cs`). Ô caro vẽ ở tọa độ **màn hình** nên
không phóng to theo ảnh — vùng bán trong suốt lộ rõ ở mọi mức zoom.

Sau mỗi ảnh, mask + cutout ghi ra `%TEMP%\KiotVietLabelPrinter\bg-preview\<session>\<item-id>_{mask,cutout}.png`
(không giữ trong RAM để batch lớn không phình bộ nhớ); dọn khi bấm XÓA DANH SÁCH hoặc đóng màn hình.

---

## 7b. Debug Mode

Bật toggle "Lưu mask debug" (hoặc CLI `--debug`) → lưu vào `<thư mục output>\debug\`:

- `<tên>_<mode>_raw_mask.png` — alpha thô từ model (resize về full-res)
- `<tên>_<mode>_refined_mask.png` — alpha sau refinement, **trước** feather
- `<tên>_<mode>_final_mask.png` — alpha **cuối cùng** dùng để composite (sau feather)
- `<tên>_<mode>_checker.png` — foreground trên nền caro (giống chế độ "Nền caro" của Preview)
- `<tên>_<mode>_final.png` — ảnh kết quả
- ULTRA thêm: `<tên>_Ultra_trimap.png` (FG trắng / unknown xám / BG đen), `<tên>_Ultra_matte.png`

> Tên file có kèm `<mode>` để chạy cả 3 chế độ trên cùng một ảnh không đè lên nhau.

---

## 8. CLI test / benchmark

```
"KiotViet Label Printer Pro V2.exe" --bg-test <ảnh|thư mục> <thư mục lưu> \
     [--mode fast|high|ultra|all] [--device cpu|gpu|auto] [--debug]
```

In thời gian từng giai đoạn cho mỗi ảnh + trung bình mỗi mode. Không mở UI, không đụng single-instance.

---

## 9. File thay đổi

- **Thêm** `services/BackgroundRemoval/`: `QualityMode`, `BgModelCatalog`, `BgModelStore`, `OnnxModelSession`,
  `AlphaMap`, `MaskMath`, `MaskRefiner`, `TrimapBuilder`, `EdgeDecontaminator`, `BgRemovalPipeline`,
  `BgRemovalTimings`, `BgRemovalCli`.
- **Sửa** `services/BackgroundRemovalService.cs` (façade: cache session theo model + device, tải model,
  `ProcessAsync(mode, device, debug)`), `Views/BackgroundRemoverView.cs` (combo Chất lượng, toggle Debug,
  luồng tải model có progress + hủy, dòng benchmark), `Program.cs` (hook `--bg-test`), `.gitignore`.
- **Không đổi** `BackgroundRemovalDiagnosticsLog.cs`, `Form/FormMain.cs`, `.gitattributes`, `.csproj`
  (không thêm package — dùng `HttpClient`, `SHA256`, `SkiaSharp`, ONNX Runtime DirectML sẵn có).

---

## 10. Giới hạn còn lại

- **Sợi rất mảnh, độ tương phản thấp** (ria mèo đơn lẻ, sợi tóc rời trên nền trắng): model chưa bắt được.
- **Màu bão hoà chạm trực tiếp vật thể** (dây/tag đỏ dính vào lông trắng): model xếp phần lông ám đỏ vào
  foreground chắc chắn nên decontam (theo đúng yêu cầu) không đụng vào → vẫn còn ám màu.
- **DirectML trên GPU tích hợp yếu**: hết VRAM → fallback CPU (an toàn, có log). Nếu cần ULTRA nhanh nên
  chạy trên GPU rời; hoặc bổ sung model BiRefNet **fp16** cho đường GPU (giảm nửa bộ nhớ) — là bước mở rộng.
