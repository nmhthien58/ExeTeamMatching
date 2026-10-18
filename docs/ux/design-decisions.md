# Design Decisions – EXE Team Matching System

> Artifact cho **Slide 07 – Design Decisions** và **Slide 10 – Accessibility / Responsive** (PRM323 Lab 2).
> Tham chiếu: [persona.md](persona.md), [user-flow.md](user-flow.md), [DESIGN.md](DESIGN.md), `PROJECT_CONTEXT.md` mục 3 (quy tắc nghiệp vụ).
> Cập nhật: 19/10/2026 (chốt B10; mã màn hình theo `EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx`).

**Công cụ AI.** Nhóm dùng **Claude** thay cho Google Stitch (đã được giảng viên đồng ý). Claude đọc `DESIGN.md`, `persona.md` và `user-flow.md` rồi dựng màn hình trong Figma (vòng 0 trong [ai-design-log.md](ai-design-log.md)); sau đó Claude Code được yêu cầu phản biện lại chính giao diện đó qua ba vòng.

**Cách đọc.** Mỗi quyết định ghi: phương án được đề xuất → quyết định (**Accept** nhận nguyên, **Modify** nhận có sửa, **Reject** bác) → lý do gắn với persona, nguyên tắc Nielsen hoặc ràng buộc → frame Figma để đối chiếu.

- **Phần A** là các quyết định **đã áp dụng** và kiểm tra được trong file Figma.
- **Phần B** là các quyết định rút ra từ lượt AI critique ngày 12/10/2026. Cột "Tình trạng" ghi rõ cái nào **chưa sửa trong Figma**.

---

## A. Quyết định đã áp dụng

### A1. Viền ô nhập – Reject

| | |
|---|---|
| **Phương án ban đầu** | Viền ô nhập, dropdown, chip lọc dùng màu Border `#D9E2EC` (DESIGN.md bản 1). |
| **Quyết định** | **Reject.** Thêm token Border Strong `#728AA3` cho viền của mọi điều khiển bấm được; Border chỉ còn dùng cho viền trang trí. |
| **Lý do** | Ràng buộc accessibility: Border trên nền trắng chỉ đạt **1,31:1**, dưới ngưỡng 3:1 cho điều khiển. Border Strong đạt **3,57:1**. |
| **Đối chiếu** | Figma `04 Design System` (token `color/border-strong`); `A05 – Hoàn thiện hồ sơ lần đầu / Bước 2`; DESIGN.md Phụ lục B dòng 1. |

### A2. Thành viên nhóm EXE201 kế thừa – Reject

| | |
|---|---|
| **Phương án ban đầu** | Khi sinh viên chấp nhận lời mời, danh sách thành viên đổi ngay (giống nhóm EXE101). |
| **Quyết định** | **Reject.** Tách hai vùng: "Thành viên hiện tại" và "Thay đổi đang chờ duyệt". Thay đổi chỉ áp dụng khi Bộ môn duyệt. |
| **Lý do** | Ràng buộc nghiệp vụ: quy tắc 7 trong `PROJECT_CONTEXT.md`. Persona Minh (P1) lo yêu cầu "mất hút", nên trạng thái chờ phải luôn nhìn thấy (Nielsen 1 – hiển thị trạng thái hệ thống). |
| **Đối chiếu** | `S15 – Nhóm của tôi – Tổng quan / Có thay đổi chờ duyệt`; `S27 – Tạo yêu cầu đổi nhóm – chọn thay đổi / Có thay đổi`. |

### A3. Trạng thái chỉ phân biệt bằng màu – Modify

| | |
|---|---|
| **Phương án ban đầu** | Chip trạng thái, chip lọc đang chọn và banner hạn chót phân biệt bằng màu nền (DESIGN.md bản 1). |
| **Quyết định** | **Modify.** Giữ nhóm màu, thêm icon cố định cho từng nhóm trạng thái, dấu tích cho chip lọc đang chọn, icon và dòng thời gian còn lại cho banner hạn chót. |
| **Lý do** | Ràng buộc accessibility: không truyền đạt thông tin chỉ bằng màu. Persona Vy (P2) cần biết còn bao lâu, không chỉ thấy banner đổi màu. |
| **Đối chiếu** | `S01 – Trang chủ / Banner hạn chót 4 mức`; `S06 – Bộ lọc và sắp xếp / Đã chọn`; DESIGN.md mục 7. |

### A4. Cỡ chữ của chip và dòng lỗi – Modify

| | |
|---|---|
| **Phương án ban đầu** | Chữ trong chip, dòng lỗi dưới ô nhập và dòng phụ của thẻ sinh viên dùng cỡ 12 (DESIGN.md bản 1). |
| **Quyết định** | **Modify.** Nâng lên 14 (Label và Body). Cỡ 12 chỉ còn cho thời gian, bộ đếm ký tự và nhãn tab bar. |
| **Lý do** | Ràng buộc accessibility: chữ nội dung tối thiểu 14 sp. Dòng lỗi là thứ người dùng buộc phải đọc để sửa (Nielsen 9 – giúp nhận biết và phục hồi lỗi). |
| **Đối chiếu** | `A05 – Hoàn thiện hồ sơ lần đầu / Lỗi nhập SĐT`; DESIGN.md Phụ lục B dòng 6–7. Phần còn sót: xem B12. |

### A5. Thông báo lỗi bằng snackbar – Modify

| | |
|---|---|
| **Phương án ban đầu** | Mọi kết quả thao tác, cả thành công lẫn lỗi, hiện bằng snackbar. |
| **Quyết định** | **Modify.** Snackbar chỉ dùng cho kết quả thành công. Lỗi dùng dòng lỗi tại ô nhập hoặc banner lỗi có nút "Thử lại", và giữ nguyên nội dung đã nhập. |
| **Lý do** | Snackbar tự ẩn sau 4 giây nên người dùng có thể bỏ lỡ lỗi. Persona Minh dùng 4G giữa giờ nghỉ, dễ gặp lỗi mạng khi gửi yêu cầu đổi nhóm (Nielsen 9; flow F2 nhánh E2). |
| **Đối chiếu** | `S28 – Tạo yêu cầu đổi nhóm – lý do và xác nhận / Lỗi mạng`; `S09 – Gửi yêu cầu tham gia / Lỗi gửi`. |

---

## B. Quyết định từ lượt AI critique ngày 12/10/2026

Claude Code xem 45 màn hình mobile (trạng thái mặc định) và 17 trang web chính, kèm một lượt đo tự động. Web được xem ở mức thu nhỏ nên các mục W kém chắc hơn mobile.

| # | Màn hình | Phát hiện | Căn cứ | Quyết định | Tình trạng |
|---|---|---|---|---|---|
| B1 | S01, L01 | Khối lời chào nền Sky bão hòa là thứ nổi nhất màn hình, lấn át hạn chót và nút chính; lặp với tiêu đề "Trang chủ". | Nielsen 8; DESIGN.md: Sky chỉ để trang trí | **Modify** – giữ lời chào, đổi sang nền nhạt (Primary Container) hoặc bỏ khối nền | Đã sửa 12/10/2026 (vòng 1): bỏ khối nền Sky |
| B2 | S10 → S04 | Thẻ sinh viên tìm nhóm không có nút mời; prototype dẫn sang `S04 / Người khác xem`, cũng không có nút. | Nielsen 4, 7; persona Minh | **Accept** – thêm biến thể S10 cho trưởng nhóm, có nút "Gửi lời mời" và dẫn sang `S04 / Trưởng nhóm xem` | Đã sửa 12/10/2026 (vòng 2): frame mới `S10 / Trưởng nhóm xem` |
| B3 | S06 | Bottom sheet bị cắt ở đáy frame, không thấy nút áp dụng và nút xóa bộ lọc. | Nielsen 3 | **Accept** – đưa hai nút vào thanh cố định cuối sheet | Đã sửa 12/10/2026 (vòng 2) |
| B4 | S01 | Banner "Bạn có 1 lời mời… Chạm để xem" trông giống banner hạn chót tĩnh, không có dấu hiệu bấm được. | Nielsen 4, 6 | **Accept** – đổi thành thẻ có mũi tên, giống các dòng điều hướng khác | Đã sửa 12/10/2026 (vòng 1) |
| B5 | S01, S05, S18 | "Rất phù hợp · 92%" là con số chính xác giả; S01 không giải thích 92% tính từ đâu. | Nielsen 2; persona cần phân biệt "tham khảo" với "chính thức" | **Modify** – giữ nhãn "Rất phù hợp", bỏ số %, luôn kèm dòng lý do | Chưa sửa |
| B6 | S05 | Nhóm đã 6/6 vẫn nằm trong "Nhóm đang tuyển"; chip "Hoạt động" / "Chưa đủ điều kiện" là trạng thái nội bộ. | Persona Vy; Nielsen 2 | **Accept** – ẩn nhóm đã đủ; với người ngoài nhóm chỉ hiện "Còn n chỗ" | Đã sửa 12/10/2026 (vòng 2): thay chip bằng dòng "n/6 thành viên · còn k chỗ" |
| B7 | S08, S15, L01–L05 | Chip trạng thái nằm chung hàng với chip domain, cùng hình dạng. | Nielsen 4 | **Accept** – đưa chip trạng thái lên cạnh tên nhóm | Đã sửa 12/10/2026 (vòng 1) ở S08, S15, L03, L05; thẻ danh sách L01, L02, L04 vốn đã đúng |
| B8 | S17, S19 | Chuyên ngành bị cắt ("Digital M…"), trong khi đây là thông tin quyết định khi mời người. | Nielsen 6; quy tắc ≥ 2 chuyên ngành | **Accept** – cho dòng chuyên ngành xuống tối đa 2 dòng | Đã sửa 12/10/2026 (vòng 3), cả S27–S29 |
| B9 | L02 | Thẻ "Đã thu hồi" nằm trong tab "Chờ xác nhận (3)". L01 lặp lại gần nguyên danh sách của L02. | Nielsen 4, 8; persona thầy Khoa | **Accept** – bỏ thẻ đã thu hồi khỏi tab; L01 chỉ giữ số liệu và 1–2 yêu cầu mới nhất | Đã sửa 12/10/2026 (vòng 2): tab thứ ba đổi thành "Đã đóng" |
| B10 | Tab bar giảng viên | Tab thứ tư là Thông báo; tài liệu context ghi là Hồ sơ, thông báo mở từ chuông. AI đề xuất đổi tab thành Hồ sơ cho khớp tài liệu. | Nhất quán với tài liệu | **Reject** – giữ Thông báo ở tab vì persona thầy Khoa vào ứng dụng chủ yếu từ thông báo (flow F3); hồ sơ L06 mở từ icon tài khoản. Cập nhật tài liệu thay vì sửa giao diện | Đã chốt 19/10/2026; tài liệu đã cập nhật ([user-flow.md](user-flow.md) mục 7). Dựa trên giả định A4 trong persona.md, chưa kiểm chứng |
| B11 | A07 | "Đăng xuất" là nút đỏ tô đặc rộng hết màn hình. | Nielsen 5; DESIGN.md: hành động nguy hiểm đi qua dialog | **Modify** – đổi thành nút viền; màu Error chỉ dùng trong dialog xác nhận | Đã sửa 12/10/2026 (vòng 1) |
| B12 | S27–S29, L01 | 26 nhãn số liệu ("Thành viên thay đổi", "Tỷ lệ thay đổi", "Yêu cầu đang chờ", "Nhóm đang hướng dẫn") dùng cỡ 12. | Accessibility: chữ nội dung ≥ 14 sp | **Accept** – nâng lên Body 14 | Đã sửa 12/10/2026 (vòng 3) |
| B13 | S23 | Thẻ giảng viên đã đủ nhóm bị làm mờ toàn bộ. | Accessibility: Text Disabled chỉ 2,15:1 | **Modify** – giữ chữ bình thường, chỉ khóa hành động và thêm chip "Đã đủ" | Đã sửa 12/10/2026 (vòng 3): chip "Đã đủ" |
| B14 | S14 | Tên domain không khớp danh mục D01–D18 trong tài liệu context. | Nhất quán dữ liệu | **Accept** – dùng đúng 18 tên trong `EXE_Team_Matching_Context.md` | Chưa sửa |
| W1 | W19 | Cột Trạng thái bị cắt ở mép phải tại 1440 px. | Responsive; DESIGN.md mục 1.3 | **Accept** – cho bảng cuộn ngang trong khung hoặc rút gọn cột | Đã sửa 12/10/2026 (vòng 3): nới cột Trạng thái lên 192 px |
| W2 | W19 | Hai nút "Gán theo nguyện vọng" và "Gán giảng viên đã nhận" khó phân biệt; thanh tải 0/6 vẫn hiện gần đầy. | Nielsen 2, 5 | **Accept** – đặt lại tên nút, vẽ thanh tải đúng tỷ lệ | Chưa sửa |
| W3 | W18 | Nút Duyệt / Từ chối ở header, còn lý do của trưởng nhóm nằm dưới mép 900 px. | Nielsen 5; persona cô Hạnh | **Accept** – đưa lý do lên trên bảng so sánh | Đã sửa 12/10/2026 (vòng 2) |

**Chọn cho Slide 07** (5 ví dụ, đủ ba loại): viền ô nhập – AI phát hiện ở vòng 0, cùng nội dung A1 (Accept, có số đo), B10 (Reject), B13 (Modify), B11 (Modify), B2 (Accept).

---

## C. Minh chứng Accessibility / Responsive

| Tiêu chí | Kết quả | Minh chứng |
|---|---|---|
| Tương phản chữ ≥ 4,5:1 | Đạt với 23 cặp chữ / nền được phép dùng | DESIGN.md Phụ lục A.1 |
| Tương phản điều khiển và icon ≥ 3:1 | Đạt với 6 cặp nét / nền | DESIGN.md Phụ lục A.2 |
| Cặp màu bị cấm | Trắng / Sky 2,30:1; Primary / Primary Container 4,27:1 (cho chữ); Text Disabled / Surface 2,15:1 (chỉ cho thành phần vô hiệu) | DESIGN.md Phụ lục A.3 |
| Vùng chạm ≥ 48 × 48 dp | Không có nút, tab hay icon button nào dưới 48 trên 171 frame mobile. Chip cao 32 được bọc vùng chạm 48 khi bấm được | Đo tự động ngày 12/10/2026, nhận diện theo tên layer |
| Chữ nội dung ≥ 14 sp | 4.452 đoạn chữ, không có đoạn nào dưới 12. Nhãn số liệu ở S27–S29 và L01 đã nâng lên Body 14 ở vòng 3 (mục B12); chữ cỡ 12 còn lại ở các màn này đều là mốc thời gian | Đo tự động ngày 12/10/2026 |
| Kiểm tra 360 và 412 dp | 26 frame `@412` cho các màn hình trong 3 flow | Figma `03 Final UI` → section `Kiểm tra 412` |
| Không chỉ dùng màu | Chip trạng thái có icon và chữ; chip lọc đang chọn có dấu tích; banner hạn chót có icon và dòng thời gian; ô nhập lỗi có icon và dòng lỗi | Quyết định A3; DESIGN.md mục 7 |
| Text style | Toàn bộ 4.452 đoạn chữ gắn text style của thư viện | Đo tự động ngày 12/10/2026 |

---

## D. Tồn tại

- 13 trên 17 mục ở Phần B đã sửa trong Figma qua ba vòng ngày 12/10/2026; ảnh trước / sau và chi tiết ở [ai-design-log.md](ai-design-log.md). Còn lại: B5, B14, W2 chưa sửa. B10 đã chốt Reject ngày 19/10/2026 (không cần sửa Figma).
- B5 chưa sửa nên ảnh S01, S05, S07 trong slide vẫn còn "Rất phù hợp · 92%".
- Web mới được phản biện ở trạng thái mặc định của 17 trên 26 trang; các trạng thái trống, lỗi và form chưa được xem.
- Tương phản được tính từ mã màu của token, chưa đo trên từng frame.
