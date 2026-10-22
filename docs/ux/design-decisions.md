# Design Decisions – EXE Team Matching System

> Artifact cho **Slide 07 – Design Decisions** và **Slide 10 – Accessibility / Responsive** (PRM323 Lab 2).
> Tham chiếu: [persona.md](persona.md), [user-flow.md](user-flow.md), [DESIGN.md](DESIGN.md), `PROJECT_CONTEXT.md` mục 3 (quy tắc nghiệp vụ).
> Cập nhật: 19/10/2026 (chốt B10; mã màn hình theo `EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx`). Lần 2 cùng ngày: thêm Phần E – quyết định theo góp ý của giảng viên.

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

---

## E. Quyết định theo góp ý của giảng viên ngày 19/10/2026

Góp ý gốc và nơi xử lý: `PROJECT_CONTEXT.md` mục 13. Phần này ghi **phương án đã cân nhắc** và lý do chọn, để trả lời câu hỏi "vì sao làm như vậy". Figma chưa cập nhật; tài liệu và code theo các quyết định dưới đây.

| # | Góp ý | Phương án đã cân nhắc | Quyết định | Lý do |
|---|---|---|---|---|
| E1 | Web cho sinh viên, giảng viên | (a) Web riêng cho từng vai trò; (b) một web, bốn khung vai trò, **dùng chung mã màn hình với mobile** | **(b)** | Một spec cho hai nền tảng, không nhân đôi tài liệu; giảng viên (P3) và mentor (P5) làm việc nhiều trên laptop; bố cục danh sách + chi tiết tận dụng màn hình rộng (`DESIGN.md` 8.2) |
| E2 | Giao tiếp giảng viên – sinh viên | (a) Chat tự do một kênh; (b) **hai kênh Nhóm / Cố vấn + đánh dấu câu hỏi**; (c) chỉ bình luận trên việc | **(b)** | Nỗi đau thật không phải "thiếu chỗ nhắn" (đã có Zalo) mà là **câu hỏi cho cố vấn bị trôi** (P1, P5). Câu hỏi có trạng thái Chờ / Đã trả lời và gom vào "Cần trả lời" ở L01, M01; kênh Nhóm riêng để cố vấn không bị ồn. Tin thường không gửi thông báo để tránh quá tải |
| E3 | Quản lý file, task của nhóm và giảng viên | (a) Liên kết Google Drive; (b) **lưu file trong hệ thống, có phiên bản; task + bài nộp** | **(b)** | Link Drive hay mất quyền, không biết bản nào mới (P5). Phiên bản tự động khi tải trùng tên giải quyết "bản mới nhất"; **bài nộp** do giảng viên tạo thay cho nhận báo cáo qua email, và cho P3 thấy nhóm nào trễ hạn ngay ở trang chủ |
| E4 | Tông màu sáng hơn, FPT hơn, hiện đại, ít chữ | (a) Cam `#F37021` làm nút chính; (b) **cam đậm `#C2410C` cho hành động, xanh `#0E5BA8` cho điều hướng, cam thương hiệu chỉ trang trí** | **(b)** | (a) chỉ đạt 2,94:1 với chữ trắng – trượt chuẩn 4,5:1 mà Lab 2 đã chứng minh đạt. Tách vai trò hai màu (cam = làm, xanh = ở đâu) giữ được nguyên tắc "một nút Primary". Ít chữ: quy tắc ở `DESIGN.md` 9.1 |
| E4a | Sửa lại E4 (22/10/2026): màu chưa đúng FPT, nút cam đỏ còn tab, chip lọc, nút phụ, icon màn trống màu xanh – nhìn lệch | (a) Giữ E4; (b) dùng đúng `#F37021` cho nút, chữ tối; (c) **mã màu theo sổ tay thương hiệu FPT: cam FPT đậm `#C6510B` (cùng sắc độ `#F37021`) cho mọi thứ bấm được và đang chọn, FPT Blue `#0066B3` chỉ cho thông tin, FPT Green cho thành công** | **(c)** | `#C2410C` và `#0E5BA8` không phải mã FPT; hai màu nhấn cho hai loại điều khiển làm lệch tông (góp ý của trưởng nhóm 22/10). (b) giữ đúng mã nhưng nút cam chữ đen khác mọi ứng dụng FPT và khó đọc khi nhấn. (c) đạt 4,58:1 với chữ trắng, một màu nhấn duy nhất, ba màu logo vẫn hiện diện ở đúng vai trò. Chi tiết `DESIGN.md` bản 5, Phụ lục E |
| E5 | Ý tưởng khi xin vào nhóm | (a) Một ô mô tả tự do; (b) **1–3 thẻ ý tưởng có cấu trúc hoặc tích "Chưa có ý tưởng"**, bắt buộc chọn một trong hai | **(b)** | Trưởng nhóm (P1) cần so sánh nhanh nhiều yêu cầu; thẻ ý tưởng có tiêu đề đọc lướt được. Ô "Chưa có ý tưởng" kèm câu "nhiều nhóm cần người thực hiện" để Vy (P2) không thấy bị đánh giá thấp |
| E6 | Không hiện Khám phá khi học kỳ đã bắt đầu | (a) Giữ tab, hiện popup khi bấm; (b) **đổi tab Khám phá thành Dự án**, popup chỉ cho đường dẫn cũ | **(b)** | (a) để lại một tab chết ở vị trí dễ bấm nhất suốt cả kỳ. Giai đoạn đổi thì nhu cầu đổi: trước là *tìm nhóm*, sau là *làm việc với nhóm* – tab bar đi theo nhu cầu. Popup vẫn cần cho link trong thông báo, email |
| E7 | Gợi ý đề tài cho giảng viên; 2 giảng viên cùng thích 1 đề tài | (a) Ai bấm trước giữ đề tài; (b) giảng viên mời, nhóm nhận lời đầu tiên; (c) **"Quan tâm" không giữ chỗ, giảng viên thấy số người cùng quan tâm, nhóm thấy tên và tự chọn, các quan tâm khác tự đóng** | **(c)** | (a) tạo cuộc đua và trái quy tắc 9 (nhóm chọn nguyện vọng, Bộ môn quyết). (b) gây áp lực cho nhóm phải trả lời nhanh. (c) giữ nguyên luồng F3 đã kiểm thử, chuyển xung đột thành **lựa chọn của nhóm**, minh bạch với giảng viên (biết có cạnh tranh), không lộ danh tính đồng nghiệp; giới hạn "sức chứa còn lại + 2" chặn giữ đề tài. **Sửa 20/10/2026 bởi E12:** nhóm không gửi yêu cầu mà xếp nguyện vọng, hệ thống ghép – F3 chỉ còn trong Figma Lab 2 |
| E8 | 5 kỹ năng, thanh kéo 1–5 | (a) Chip kỹ năng tự do như cũ; (b) **5 thanh kéo, mặc định "Chưa đánh giá"**; (c) biểu đồ radar | **(b)** cho nhập, **thanh ngang 5 ô** cho xem | Mặc định 3/5 sẽ làm mọi hồ sơ giống nhau; bắt buộc chạm từng thanh. Radar khó đọc ở 360 px và với trình đọc màn hình. Ở S20, kỹ năng nhóm đang thiếu được đánh dấu để trưởng nhóm so khớp nhanh; Matching dùng thang này để gợi ý bù chỗ yếu |
| E9 | Hồ sơ chỉ kỹ năng + mô tả, bỏ liên lạc | – | **Theo góp ý**; liên lạc lấy từ file import, chỉ hiện cho người trong nhóm | Rút onboarding từ 2 bước còn 1 màn (mục tiêu ≤ 1 phút); đồng thời xử lý luôn nỗi đau lộ SĐT (E01). Liên lạc hằng ngày chuyển sang tin nhắn trong Dự án (E2) |
| E10 | Mentor: người ngoài hoặc giảng viên, màn hình như giảng viên | (a) Nhóm tự mời mentor; (b) **chỉ Bộ môn tạo và gán**; (c) nhóm mời, Bộ môn duyệt | **(b)** (nhóm chọn 19/10/2026); từ 20/10 việc "gán" do hệ thống ghép theo quan tâm của mentor ở tuần 6, Bộ môn soát và sửa (E13) | Người ngoài trường cần được Bộ môn kiểm soát. **Một mentor mỗi nhóm**; Bộ môn đặt số nhóm tối đa riêng cho từng mentor, mentor tự chọn số mong muốn (cảnh báo khi vượt mong muốn, chặn khi vượt tối đa). Mentor vào từ tuần 6 và họp 2–3 buổi, nên điều quan trọng nhất là **vào cuộc nhanh**: C01 có thẻ tóm tắt nhóm và "Buổi mentor n/3". Giảng viên làm mentor: một tài khoản hai vai trò, nhóm mentor hiện trong khung giảng viên với nhãn – không có nút chuyển vai trò (bớt một bước, bớt nhầm lẫn) |
| E11 | Quản lý đề tài và nhóm từ EXE101 lên EXE201 | (a) Theo **nhóm** (mã nhóm, tên nhóm); (b) coi nhóm EXE201 là **cùng một bản ghi** với nhóm EXE101; (c) **đề tài là trục xuyên kỳ, nhóm là đơn vị theo từng môn** | **(c)** | (a) đứt mạch khi đổi thành viên hoặc đổi tên nhóm – kỳ FA26 có 84 yêu cầu đổi nhóm. (b) trái quy tắc 4 (mỗi môn một nhóm) và làm rối yêu cầu đổi nhóm. (c) giữ được file, lịch sử, giảng viên kỳ trước theo đề tài, trong khi nhóm vẫn kiểm tra 4–6 người, 2 chuyên ngành theo môn. Đổi đề tài ở EXE201 đi cùng yêu cầu đổi nhóm để Bộ môn duyệt một lần (quy tắc 26) |
| E12 | Ghép giảng viên hướng dẫn (làm rõ 20/10/2026) | (a) Giữ luồng Lab 2: nhóm gửi yêu cầu → giảng viên chấp nhận/từ chối → Bộ môn phân công; (b) Bộ môn tự gán dựa vào "Quan tâm"; (c) **nhóm xếp nguyện vọng 1–3, giảng viên bấm Quan tâm, hệ thống tự ghép lúc đóng đợt lập nhóm, Bộ môn "Ghép sớm" và sửa tay** | **(c)** | Đúng thực tế: Bộ môn vốn chốt giảng viên vào hạn lập nhóm, trước đây chỉ dựa nguyện vọng rồi ghép tay giảng viên còn trống. (a) thêm một vòng chờ cho cả hai bên, và với ~40 nhóm mỗi giảng viên thì duyệt từng yêu cầu là gánh nặng. (b) bỏ mất tiếng nói của nhóm. (c) dùng tín hiệu của cả hai phía; "hai bên cùng chọn" được ưu tiên nên xung đột 2 giảng viên cùng thích một đề tài tự được giải bằng nguyện vọng của nhóm; bước ghép tay cũ thành bước tự động cuối (cùng chuyên môn, ít nhóm nhất). Mỗi kết quả có **nhãn lý do** để không ai bất ngờ (nỗi đau 55 nhóm bị điều tiết mà không ai báo, E01) |
| E13 | Số nhóm tối đa của giảng viên | (a) Giảng viên tự khai số slot như mentor; (b) không giới hạn, Bộ môn ghép bao nhiêu quản bấy nhiêu; (c) **Bộ môn đặt tối đa theo môn, được để trống** | **(c)** | Ngoài thực tế số nhóm do Bộ môn phân công, không do giảng viên chọn – khác mentor (5–10 nhóm, tình nguyện). Có tối đa thì thuật toán không dồn quá nhiều nhóm cho người được nhiều nhóm chọn; để trống thì vẫn chia đều theo "ít nhóm nhất". Mentor giữ tối đa (Bộ môn) + mong muốn (mentor) như E10, nhưng **sinh viên không chọn mentor** – ghép theo quan tâm của mentor ở tuần 6 |
| E14 | Lịch họp EXE201 | (a) Mỗi tuần đề xuất 1–3 khung giờ như họp với mentor; (b) giảng viên tự xếp nhóm vào giờ; (c) **giảng viên đăng khung giờ, hệ thống cắt slot 15 phút, nhóm tự chọn một slot cố định, hệ thống sinh họp hằng tuần** | **(c)** | Thực tế EXE201 họp online 15 phút mỗi tuần và chốt lịch trong 2 tuần đầu. (a) với ~40 nhóm là 40 cuộc bỏ phiếu mỗi tuần. (b) giảng viên phải biết lịch của từng nhóm. (c) đẩy việc chọn giờ cho nhóm (họ biết lịch học của mình), ai chọn trước được trước, giảng viên chỉ đổi khi cần; nhóm chưa chọn tới hạn được xếp tự động nên không ai bị bỏ sót. Cách bỏ phiếu (a) vẫn dùng cho buổi mentor và họp phát sinh |
| E15 | Chấm Checkpoint EXE101 | (a) Lưu điểm từng checkpoint; (b) đặt lịch trình bày; (c) **chỉ đánh dấu "đã trình bày" + ngày + nhận xét, không điểm, không đặt lịch** | **(c)** | Điểm đã nằm trên FAP – nhập lại là trùng việc và dễ lệch. Sinh viên trình bày lúc nào cũng được trong giờ dạy nên đặt lịch là thừa. Giá trị hệ thống mang lại là **biết còn thiếu gì**: giảng viên lọc "Chưa trình bày CP2" ngay trên lớp, nhóm thấy "Checkpoint 2/4" và nhận xét. Đánh dấu nhầm thì Hoàn tác thay vì hộp xác nhận (DESIGN.md 6.4) |

**Kiến trúc đi kèm (chốt 19/10/2026):** thêm **Collaboration Service** riêng với `collab_db`, SignalR cho tin nhắn, MinIO cho file (`PROJECT_CONTEXT.md` §5.8). DB script, OpenAPI và sự kiện Kafka đã làm ở BE-60, BE-61 (theo luồng giảng viên ngày 19/10); phần thay đổi của mục E12–E15 dưới đây chưa vào DB script và contract.

**Đã chốt với nhóm (19/10/2026):** "học kỳ đã bắt đầu" = đợt lập nhóm đã đóng; một mentor mỗi nhóm, số nhóm do Bộ môn đặt riêng cho từng mentor; mentor từ tuần 6, 2–3 buổi; giảng viên lớp trên FAP không hiển thị.

**Còn mở:** giới hạn 25 MB và 3 ý tưởng; đổi đề tài ở EXE201 có cần Bộ môn duyệt không (đang chọn: có, đi cùng yêu cầu đổi nhóm).
