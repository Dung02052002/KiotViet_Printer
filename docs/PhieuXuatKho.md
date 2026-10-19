# Phiếu xuất kho — hướng dẫn sử dụng

Chức năng tạo phiếu xuất kho bằng cách điền dữ liệu vào **chính file phiếu mẫu**
(không dựng lại workbook): chỉ đổi số phiếu, tên hàng, đơn vị tính, số lượng yêu
cầu / thực xuất (và font, thiết lập in nếu người dùng chọn đổi). Mọi thứ khác
của mẫu giữ nguyên.

## Mở chức năng

Màn hình chính → mục **CÔNG CỤ** → card **Phiếu xuất kho**.

## Các bước

1. **Mã phiếu**: nhập `23609` → mã hoàn chỉnh `XK23609`. Nhập sẵn `XK23609`
   (hoặc `xk23609`) cũng được, không bị ghép tiền tố hai lần. Chỉ dùng chữ không
   dấu, số, `-`, `_`, `.` (vd `23609.01` → `XK23609.01`). Tiền tố đổi được ở ô *Tiền tố*.
   Nhiều phiếu chuyển gộp một phiếu xuất: ngăn bằng dấu phẩy, `23609, 23610` →
   `XK23609,XK23610` (mỗi mã đều có tiền tố; file kết quả `XK23609,XK23610.xlsx`).
   Không được để dấu phẩy thừa ở đầu/cuối, hai dấu phẩy liền nhau hay mã lặp.
2. **CHỌN FILE PHIẾU MẪU**: file phiếu xuất kho mẫu (.xlsx / .xls), ví dụ
   `Desktop/Phiếu xuât Sài Gòn/<cửa hàng>/Phiếu Xuất Kho.xlsx`. Phần mềm tự tìm
   ô `Số : ...` và bảng *Tên hàng hóa | Đơn vị tính | Số lượng (Yêu cầu / Thực xuất)*
   theo nội dung, không theo địa chỉ ô cố định. File mẫu chỉ được đọc.
3. **CHỌN FILE HÀNG HÓA**: file *Chi tiết chuyển hàng* xuất từ KiotViet — cần các
   cột `Mã hàng`, `Tên hàng`, `Số lượng chuyển` (hoặc `Số lượng`). Cột
   `Số lượng nhận` không bao giờ được dùng.
4. **CHỌN THƯ MỤC OUTPUT**: file kết quả tên `<mã phiếu>.xlsx` (mẫu .xls thì
   kết quả .xls). Thư mục chưa có sẽ được tạo.
5. Xem bảng **XEM TRƯỚC DỮ LIỆU**: dòng đỏ = lỗi (phải sửa), dòng vàng = cảnh báo
   (nên xem lại). Có thể sửa trực tiếp *Tên hàng mới*, *Đơn vị*, *SL yêu cầu*,
   *SL thực xuất*.
6. (Tùy chọn) **KHỔ GIẤY VÀ THIẾT LẬP IN**: mặc định *Giữ nguyên thiết lập in
   của file mẫu*. Chọn *Tùy chỉnh* để đổi khổ giấy, hướng, lề (cm), tỷ lệ in
   (% / vừa chiều rộng trang / vừa một trang) — chỉ những mục KHÁC mẫu mới được
   ghi vào file kết quả. **LẤY THIẾT LẬP TỪ FILE MẪU** đọc lại thiết lập in từ
   file mẫu; **KHÔI PHỤC THIẾT LẬP MẪU** bỏ mọi tùy chỉnh. Phần mềm không bao giờ
   tự đổi khổ giấy / lề / tỷ lệ để "ép" phiếu vừa trang.
7. **KIỂM TRA DỮ LIỆU** → **XUẤT PHIẾU EXCEL**. File đã tồn tại thì luôn hỏi
   trước khi ghi đè. Sau khi ghi, file được mở lại và đối chiếu với mẫu; thông
   báo liệt kê những gì đã kiểm tra.
8. *(Tùy chọn — có thể bỏ qua, in thẳng ở bước 9)* **XEM TRƯỚC BẢN IN** (cần Microsoft Excel): xem từng trang đúng như sẽ in —
   TRANG ĐẦU / TRANG TRƯỚC / TRANG SAU / TRANG CUỐI, "Trang X / Y", phóng to /
   thu nhỏ (Ctrl + lăn chuột, nút − / +, VỪA TRANG, 100% = đúng cỡ tờ giấy), xem
   MỘT TRANG hoặc NHIỀU TRANG. Bên phải: khổ giấy (trong file và thực tế), hướng,
   tổng số trang, tỷ lệ, vùng in, lề, font, các dòng trên từng trang và danh sách
   cảnh báo bố cục (bấm một cảnh báo để mở trang liên quan). Có thể bấm **IN**
   ngay trong cửa sổ này.
9. Chọn **MÁY IN PHIẾU** rồi bấm **IN PHIẾU** (hoặc IN trong cửa sổ xem trước).
   Luôn hỏi xác nhận trước khi in. Không bắt buộc xem trước: chưa xem thì hộp
   xác nhận chỉ nhắc, vẫn in được.

Dữ liệu, font hoặc thiết lập in đổi sau khi xuất → bản đã xuất bị coi là **cũ**:
XEM TRƯỚC BẢN IN và IN PHIẾU bị khóa tới khi xuất lại. File kết quả bị sửa / ghi
đè bên ngoài (vd mở bằng Excel rồi lưu) cũng phải xuất lại mới in được.

## Nhiều phiếu trong một file

File hàng hóa là **Danh sách chi tiết chuyển hàng** xuất từ KiotViet cho nhiều
phiếu chuyển cùng lúc (có cột `Mã chuyển hàng`, từ 2 mã trở lên) → màn hình tự
chuyển sang chế độ nhiều phiếu:

- Mỗi mã chuyển hàng là **một phiếu, xuất ra một file riêng** (không gộp). Mã
  phiếu = mã chuyển, đổi tiền tố `TRF` thành tiền tố ở ô *Tiền tố*, **giữ nguyên
  số 0 ở đầu**: `TRF023522` → `XK023522` → file `XK023522.xlsx`. Ô *Mã phiếu* bị
  khóa.
- Số lượng lấy từ cột `Số lượng chuyển` của từng dòng (không dùng *Tổng SL chuyển*
  hay *Số lượng nhận*). Mỗi phiếu được đối chiếu riêng: một mã hàng có ở hai phiếu
  khác nhau không bị coi là trùng.
- Ngày lập phiếu: dùng ô *Ngày lập phiếu* trên màn hình (mặc định hôm nay) cho
  mọi phiếu — **không** lấy cột *Ngày chuyển* trong file.
- Ô **Phiếu đang xem** (trên bảng xem trước) chọn phiếu để xem / sửa; mỗi dòng ghi
  ghi chú chuyển, số dòng, tổng SL và tình trạng (lỗi / chưa xuất / đã xuất /
  đã xem trước).
- **XUẤT PHIẾU EXCEL** kiểm tra **tất cả** phiếu trước — còn một phiếu lỗi thì
  không xuất phiếu nào; file đã có thì hỏi ghi đè một lần cho cả lô.
- **XEM TRƯỚC BẢN IN** mở phiếu đang chọn; trong cửa sổ xem trước có
  **PHIẾU TRƯỚC / PHIẾU SAU** (góc trên bên phải, hoặc Ctrl + PageUp / PageDown)
  để xem lần lượt mọi phiếu mà không phải đóng cửa sổ. Phiếu nào đã hiện trong
  cửa sổ thì được tính là đã xem trước.
- Nút in của cửa sổ xem trước là **IN TẤT CẢ**, bật ngay khi máy in hợp lệ —
  **không** cần xem đủ mọi phiếu. **IN PHIẾU** ở màn hình chính cũng in tất cả
  phiếu, chỉ cần **mọi phiếu là bản xuất hiện hành**; phiếu nào chưa xem trước
  được liệt kê trong hộp xác nhận để nhắc. Sửa dữ liệu một phiếu chỉ bắt xuất lại phiếu đó;
  đổi ngày, font, thiết lập in, tiền tố, thư mục thì phải xuất lại tất cả.
- Dòng thiếu mã chuyển hàng được gom thành một phiếu lỗi riêng ở cuối danh sách
  và chặn xuất.

File chỉ có **một** mã chuyển hàng: vẫn là chế độ một phiếu, ô *Mã phiếu* được
điền sẵn từ mã chuyển (nếu đang trống).

## Quy tắc dữ liệu

| Nội dung | Quy tắc |
|---|---|
| Tên hàng | Lấy đúng cột *Tên hàng* của file hàng hóa. Dòng thiếu tên chỉ được lấy tên từ dòng khác **cùng mã hàng**; không có thì báo lỗi. |
| Mã trùng | Cùng mã, khác tên → **lỗi**. Cùng mã, cùng tên → cảnh báo, mỗi dòng giữ số lượng riêng (không cộng gộp). |
| Đơn vị | Giày, giầy, dép, guốc, sandal, xăng đan, boot, bốt, ủng → **Đôi**; còn lại → **Cái**. Khớp theo từ (có dấu), nên "Túi giấy" vẫn là Cái. Tên không dấu / mơ hồ được đánh dấu cảnh báo để kiểm tra. Danh sách từ khóa ở `Services/Warehouse/WarehouseUnitService.cs`. |
| Số lượng | Yêu cầu = Thực xuất = số lượng trong file. Trống, không phải số, ≤ 0 → lỗi. Số lẻ → cảnh báo. Không nhân quy cách, không đổi đơn vị. |

## Số dòng khác file mẫu

- Nhiều hơn mẫu: chèn thêm dòng, chép định dạng dòng hàng cuối (viền, căn lề,
  xuống dòng, chế độ chiều cao); phần chữ ký, ô gộp, vùng in, ngắt trang bên
  dưới được dời xuống theo.
- Ít hơn mẫu: xóa các dòng hàng thừa ở cuối bảng, kéo phần bên dưới lên.
- Dòng hàng trong mẫu thực tế không đặt chiều cao cố định, nên Excel tự giãn
  dòng theo tên hàng dài khi mở/in.
- Chưa hỗ trợ (sẽ báo lỗi rõ ràng, không ghi): mẫu có ô gộp hoặc công thức
  trong chính các dòng hàng.

## Font chữ

Mặc định **Giữ nguyên định dạng của file mẫu** — không ô nào bị đổi font.
Các phạm vi khác chỉ đổi tên font + cỡ chữ (6–36 pt) trong phạm vi chọn; đậm,
nghiêng, viền, căn lề, nền giữ nguyên. Font chưa cài → lỗi; font thiếu chữ
tiếng Việt → cảnh báo.

## Xem trước bản in — cách hoạt động

- Excel (phiên ẩn, riêng của phần mềm) mở file phiếu **chỉ đọc**, chọn máy in
  đang chọn ở màn hình phiếu, rồi xuất **đúng sheet phiếu** ra PDF. Excel phân
  trang theo máy in đó, nên số trang, chỗ ngắt trang, khổ giấy, font, cỡ chữ,
  độ rộng cột, chiều cao dòng (kể cả tự giãn theo tên hàng dài), viền, ô gộp
  trên bản xem trước là của chính Excel. File PDF tạm nằm trong
  `%TEMP%\KiotVietLabelPrinter\warehouse-preview` và bị xóa khi đóng cửa sổ.
- Tổng số trang là số trang Excel tính (`PageSetup.Pages`), so khớp với số trang
  PDF — không tự suy ra từ số dòng.
- Cảnh báo bố cục (đo bằng Excel, không đoán theo độ dài chữ):
  - Tên hàng có thể bị cắt: ô xuống dòng mà dòng đặt chiều cao cố định thấp hơn
    chiều cao cần; ô không xuống dòng mà chữ rộng hơn ô.
  - Cột số lượng quá hẹp (Excel in ra `###`).
  - Phiếu rộng hơn trang giấy (cột bị in sang trang riêng).
  - Tiêu đề bảng bị cắt đôi / nằm riêng cuối trang; trang cuối trống hoặc chỉ có
    phần ký tên; số trang tăng bất thường so với file mẫu.
  - Khổ giấy thực tế khác khổ đặt trong file (máy in không có khổ đó).
  - Font trên phiếu chưa cài trên máy; tỷ lệ in làm chữ in ra nhỏ hơn 7 pt;
    ô có dữ liệu nằm ngoài vùng in.
- Lưu ý đo trên máy thực tế: các mẫu ở `Phiếu xuât Sài Gòn` đặt khổ **Letter**.
  Với máy in "HP Laser 103 107 108" Excel in trên khổ **A4**; với "Microsoft
  Print to PDF" và "HP LaserJet MFP M236sdw" là Letter. Bản xem trước luôn theo
  máy in đang chọn và báo cảnh báo khi khác khổ trong file.

## Máy in

- Máy in, font, thư mục output, file mẫu được lưu riêng ở
  `Config/warehouse-export.json` — không đụng tới `Config/config.json` (máy in
  tem, BarTender). Thiết lập in tùy chỉnh không được lưu: mỗi lần mở / đổi file
  mẫu đều về "giữ nguyên mẫu".
- In bằng Microsoft Excel (phiên Excel ẩn, riêng) với máy in được truyền trực
  tiếp, **chỉ in sheet phiếu**: máy in mặc định của Windows không bị đổi (đã
  kiểm tra). Máy không cài Excel thì chỉ xuất file được, không xem trước bản in
  và không in trực tiếp.
- Mọi thao tác Excel có giới hạn thời gian (xem trước 2 phút, in 3 phút). Excel
  treo thì đúng tiến trình Excel của phần mềm bị dừng; tiến trình Excel của
  phiên luôn được đóng hẳn sau mỗi lần xem trước / in.

## Nhật ký

`logs/warehouse_yyyyMMdd.log`: mỗi lần xuất / xem trước / in và chi tiết kỹ
thuật khi lỗi.

## Kiểm thử

`dotnet test tests/KiotVietLabelPrinter.Tests` — các test `Warehouse*`,
`PdfPageRenderer*`. Các test dùng Microsoft Excel thật (xem trước, phân trang,
in ra "Microsoft Print to PDF", tiến trình Excel không tồn đọng) chỉ chạy khi
đặt `KV_WAREHOUSE_PRINT_TEST=1`.
