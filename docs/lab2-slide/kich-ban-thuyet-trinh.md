# Kịch bản thuyết trình – PRM323 Lab 2

> Dùng với `PRM393 Lab 2 – EXE Team Matching System v2.pptx` (18 slide). Lời nói giống hệt phần speaker notes trong file pptx.
> Thời lượng theo hướng dẫn chấm: **10 phút trình bày · 5 phút demo · 5 phút hỏi đáp**.
> Chữ *nghiêng trong ngoặc* là thao tác, không đọc. Slide có dấu ⏩ có thể nói lướt nếu sắp hết giờ.

---

## Phần 1 – Trình bày (≈ 10 phút)

### Slide 01 · Project Overview (≈ 30 giây)

Em chào thầy. Em xin trình bày **EXE Team Matching System** – ứng dụng mobile ghép nhóm cho hai môn EXE101 và EXE201.

Hiện nay mỗi kỳ có khoảng 1.250 sinh viên ghép nhóm trên một file Google Sheets nhập tay. Ứng dụng của nhóm thay thế file đó, cho ba đối tượng: sinh viên, trưởng nhóm và giảng viên hướng dẫn. Link Figma và GitHub ở cuối slide.

### Slide 02 · Problem (≈ 45 giây)

Vấn đề lấy từ **file thật của kỳ FA26**: khoảng 1.250 sinh viên, 220 nhóm, tất cả ghép trên một Google Sheet nhập tay. Có 58 yêu cầu đổi nhóm đang chờ duyệt mà kết quả chỉ là ghi chú tự do – không trạng thái, không lịch sử. 7 nhóm cuối do bộ môn ghép tay sau hạn, khoảng 9 nhóm chỉ có một chuyên ngành mà không ai chặn, và SĐT của mọi người đều công khai.

Problem statement: *trưởng nhóm EXE201 cần thay người sát hạn ngay trên điện thoại và luôn biết yêu cầu đổi nhóm đang ở đâu.* Bốn chỉ số đo được trên prototype: gửi yêu cầu **dưới 2 phút**, **5 trên 5** người đọc đúng trạng thái, xin vào nhóm **trong 3 lần chạm**, và **90%** người thử hoàn thành 3 flow.

### Slide 02b · P1 Minh – Primary Persona (≈ 50 giây)

Nhóm có ba persona dùng mobile, mỗi người một slide và đủ 12 trường theo template. Cả ba là **proto-persona, chưa phỏng vấn**, nên mỗi trường gắn nhãn nguồn: O là quan sát từ file, R là quy định môn, I là suy luận, A là giả định.

Persona chính là **Minh – trưởng nhóm chạy deadline, xử lý mọi việc trên điện thoại**. Khi một thành viên không học tiếp sát hạn, Minh muốn gửi một yêu cầu đổi người có lý do để kịp duyệt. Hiện Minh phải chép danh sách vào sheet, tự tính tỷ lệ, rồi mở file nhiều lần để dò trong 65 ghi chú tự do. Minh thường dùng một tay và đọc ngoài nắng, nên tương phản và vùng chạm là ràng buộc thiết kế. Thành công là gửi yêu cầu dưới 2 phút và 5 trên 5 người đọc đúng trạng thái.

### Slide 02c · P2 Vy – Secondary Persona (≈ 40 giây)

**Vy – người mới ngoài ngành, ngại chủ động nhắn người lạ.** Khi sắp đóng đợt mà chưa có nhóm, Vy muốn biết nhóm nào còn chỗ và đang cần người như mình. Hiện Vy chỉ đăng mô tả tự do ở sheet "SV tìm nhóm", không biết nhóm nào còn chỗ, cũng không lọc được.

Vy khác Minh ở chỗ: Vy cần *tìm chỗ*, Minh cần *quản lý thay đổi* – nên hai người dẫn tới hai cấu trúc màn hình khác nhau. Thành công là xin vào nhóm trong 3 lần chạm.

### Slide 02d · P3 Thầy Khoa – Supporting Persona (≈ 40 giây)

**Thầy Khoa – người duyệt nhanh giữa các tiết.** Thầy mở ứng dụng từ thông báo, chỉ vài chục giây mỗi lần. Thầy muốn xem nhanh nhóm rồi nhận hoặc từ chối, và không nhận quá sức chứa. Trong file FA26 có 55 nhóm được điều tiết khác nguyện vọng mà không ai báo lại.

Thành công là phản hồi trong 4 lần chạm và phân biệt đúng nhóm "tôi đã nhận" với nhóm "Bộ môn đã phân công".

### Slide 02e · Persona Coverage ⏩ (≈ 30 giây)

Minh là Primary vì flow đổi thành viên nhiều bước và nhiều điểm lỗi nhất – đáp ứng được Minh thì Vy và thầy Khoa cũng được đáp ứng phần lớn. Persona thứ tư, cô Hạnh, dùng web nên ngoài phạm vi mobile. Bảng dưới nối mỗi nhu cầu với một quyết định UI và một cách kiểm chứng.

### Slide 03 · Information Architecture (≈ 40 giây)

Ứng dụng có một điểm vào là đăng nhập Google, sau đó rẽ theo vai trò. Sinh viên có tab bar 5 mục, giảng viên 4 mục.

Quy tắc điều hướng đơn giản: màn hình cấp 1 nằm trên tab bar, không có nút Back; màn hình cấp 2 có Back về đúng màn cha. Tổng cộng **42 màn hình khác nhau**, cộng thêm 2 bottom sheet và một frame mẫu trạng thái.

### Slide 04 · Three User Flows (≈ 1 phút)

Ba flow ứng với ba persona. Ô trắng là màn hình, ô cam là bottom sheet hoặc dialog.

**F1** – Vy chưa có nhóm: đăng nhập, khám phá, lọc, xem chi tiết nhóm, gửi yêu cầu, rồi vào nhóm.

**F2** – Minh đổi thành viên, là flow **dễ lỗi nhất**. Thay đổi chỉ có hiệu lực khi Bộ môn duyệt, nên màn hình luôn tách "thành viên hiện tại" và "thay đổi đang chờ". Hai nhánh lỗi chính: thiếu lý do thì báo ngay tại ô; mất mạng thì giữ nguyên nội dung và có nút Thử lại.

**F3** – thầy Khoa phản hồi yêu cầu hướng dẫn. Nếu đã đủ sức chứa, nút Chấp nhận bị khóa kèm lý do.

### Slide 05 · AI UI Generation (≈ 45 giây)

Nhóm dùng **Claude** thay cho Google Stitch, đã được thầy đồng ý. Prompt rất ngắn vì năm phần persona, task, platform, constraints và style đã nằm trong ba file đính kèm. Claude đọc ba file rồi viết plugin Figma để vẽ frame, component và prototype.

Bên phải là output ban đầu, trước khi tinh chỉnh. Nhóm xin nói thẳng: prompt của lượt tạo đầu tiên chưa lưu lại được; prompt trên slide là lượt nạp lại file context.

### Slide 06 · AI Iterations (≈ 45 giây)

Nhóm làm ba vòng, mỗi vòng **một mục tiêu khác nhau**.

Vòng 1 – phân cấp thị giác: trang chủ trưởng nhóm chưa có nút chính, giờ có nút "Mời người thay thế".
Vòng 2 – hoàn thành tác vụ: nút của bộ lọc nằm ngoài màn hình, giờ đã về trong khung.
Vòng 3 – accessibility: thẻ giảng viên bị làm mờ, tương phản chữ tăng từ 3,3 lên 6,08.

Mỗi vòng có ảnh trước, sau và số đo trên Figma.

### Slide 06b · Vòng 1 chi tiết (≈ 1 phút)

Đây là một vòng đầy đủ. **Một**, prompt yêu cầu AI phản biện theo Nielsen, accessibility, persona rồi **dừng lại chờ nhóm quyết định**, chưa được sửa. **Hai**, AI chỉ ra năm vấn đề trên từng màn hình cụ thể. **Ba**, nhóm chấp nhận bảy đề xuất và bỏ các phương án thay thế. **Bốn**, đo lại trên Figma.

Có một điểm chưa đạt: hàng nút ở trang chủ giảng viên chỉ lên được 20 px, vẫn phải cuộn. Nhóm ghi lại và xử lý tiếp ở vòng 2.

### Slide 07 · Design Decisions (≈ 50 giây)

Năm quyết định, đủ ba loại.

**Accept** viền ô nhập: AI tự phát hiện viền cũ chỉ đạt 1,31:1, nhóm đổi sang màu đạt 3,57:1.
**Reject** B10: AI đề xuất đổi tab Thông báo của giảng viên thành Hồ sơ cho khớp tài liệu. Nhóm từ chối vì thầy Khoa vào ứng dụng chủ yếu từ thông báo; hồ sơ mở từ icon tài khoản.
**Modify** B13: không làm mờ cả thẻ giảng viên đã đủ nhóm, chỉ khóa nút và thêm chip "Đã đủ".
**Modify** B11: nút đăng xuất đỏ đổi thành nút viền, vì đăng xuất không mất dữ liệu.
**Accept** B2: thêm nút mời ngay trên thẻ sinh viên cho trưởng nhóm.

### Slide 08 · Wireframe → Final UI (≈ 30 giây)

Từ wireframe sang UI cuối, nhóm giữ ba nguyên tắc: hạn chót và việc cần làm nổi nhất, một nút Primary mỗi màn; màu, chữ, khoảng cách đều lấy từ token; và mỗi màn có đủ năm trạng thái – mặc định, đang tải, trống, lỗi, thành công – như ví dụ màn Khám phá và Gửi yêu cầu.

### Slide 09 · Design System (≈ 30 giây)

Thư viện có 20 token màu, 8 text style, 11 token khoảng cách và bo góc, cộng 3 mức độ nổi. Ví dụ: sửa một component Banner hạn chót thì 53 instance trên các màn hình đổi theo.

### Slide 10 · Accessibility & Responsive (≈ 30 giây)

Tương phản chữ đạt 4,5:1 với 23 cặp màu, viền điều khiển đạt 3:1. Không có vùng chạm nào dưới 48 dp trên 171 frame. Chữ nội dung từ 14 sp; cỡ 12 chỉ dùng cho mốc thời gian và nhãn tab. Màn hình được kiểm tra ở cả 360 và 412 dp, và trạng thái không bao giờ chỉ phân biệt bằng màu.

### Slide 11 · Live Prototype (≈ 20 giây)

Prototype chạy được cả ba flow, có dialog, bottom sheet, nhánh lỗi và chuyển cảnh đang gửi sang kết quả. Có hai điểm nhóm biết nhưng chưa sửa, ghi ở góc phải. Phần này em sẽ demo trực tiếp sau.

### Slide 12 · Flutter Handoff (≈ 40 giây)

Ví dụ màn S28 – nhập lý do đổi nhóm. Mỗi màn có đủ sáu mục: layout, components, states, interactions, navigation và UI constraints. Một lập trình viên Flutter chưa biết dự án chỉ cần đọc file này là biết dựng widget gì, có những trạng thái nào và đi tiếp đến đâu. File đầy đủ 18 màn nằm trong `flutter-handoff.md`.

### Slide 12b · Tổng kết ⏩ (≈ 30 giây)

Hai bảng ánh xạ token sang Flutter và component sang widget ở phía trên. Kết quả: 42 màn hình, 3 flow, 13 trên 17 phát hiện của AI đã sửa trong Figma.

Còn tồn tại: ba mục B5, B14, W2 chưa sửa, và persona chưa phỏng vấn thật – bước tiếp theo là phỏng vấn để kiểm chứng các giả định. Em cảm ơn thầy, em xin chuyển sang phần demo.

---

## Phần 2 – Demo trực tiếp (≈ 5 phút)

Theo đúng 7 bước trong hướng dẫn chấm.

1. *(Mở file Figma)* "File chia đúng sáu phần theo thứ tự: 01 User Flow, 02 Wireframe, 03 Final UI, 04 Design System, 05 Components, 06 Prototype." *(Xem lưu ý cuối file về page và section.)*
2. *(Phần 03 Final UI)* "Đây là các màn hình khác nhau, ví dụ S01 Trang chủ, S05 Khám phá, S15 Nhóm của tôi, S28, L01, L03… – biến thể trạng thái không tính thêm."
3. *(Phần 06 Prototype, chạy F2 từ S01 / Là trưởng nhóm)* "Minh xóa thành viên rời nhóm, mời người thay thế, sang màn lý do. Em bấm gửi khi chưa nhập lý do – lỗi hiện ngay tại ô. Nhập lý do, gửi, gặp lỗi mạng – nội dung vẫn còn, bấm Thử lại. Gửi thành công sang màn Chờ duyệt." *(Bấm Back để cho thấy không quay lại form.)*
4. *(Trong lúc chạy F2)* "Đây là dialog dạng overlay, và đây là chuyển cảnh đang gửi sang kết quả."
5. *(Phần 05 Components)* "Component dùng auto layout và có biến thể trạng thái." *(Mở panel Variables)* "Màu, chữ, khoảng cách đều là variables, gắn thật vào màn hình."
6. *(Mở `docs/ux/ai-design-log.md` trên GitHub)* "Vòng 1: prompt, phản biện của AI, quyết định của nhóm, ảnh trước và sau."
7. *(Mở README trên GitHub)* "README liệt kê đủ artifact; đây là `flutter-handoff.md` với hai bảng ánh xạ và spec từng màn."

Nếu còn giờ: chạy nhanh F1 (lọc không có kết quả → Xóa bộ lọc) và F3 (đã đủ sức chứa → nút khóa).

---

## Phần 3 – Chuẩn bị hỏi đáp

Các câu thầy có thể hỏi, theo hướng dẫn chấm.

| Câu hỏi | Trả lời ngắn |
|---|---|
| Persona có nhu cầu gì khiến nhóm chọn cấu trúc điều hướng này? | Minh và Vy dùng điện thoại vài phút giữa giờ, nên trang chủ hiện ngay hạn chót và việc cần làm, còn "Nhóm của tôi" luôn cách một chạm. Thầy Khoa vào app chủ yếu từ thông báo, nên Thông báo là một tab (quyết định B10). |
| Flow nào dễ thất bại nhất? Phục hồi ra sao? | F2 đổi thành viên. Thiếu lý do → lỗi ngay tại ô. Mất mạng → giữ nguyên nội dung, có Thử lại. Lời mời bị từ chối → mời người khác. Bộ môn từ chối → xem lý do, tạo yêu cầu mới; nhóm giữ nguyên. |
| Đề xuất AI nào bị nhóm từ chối? Vì sao? | B10: AI đề xuất đổi tab Thông báo của giảng viên thành Hồ sơ cho khớp tài liệu. Nhóm giữ vì persona thầy Khoa vào app từ thông báo; nhóm sửa tài liệu thay vì sửa giao diện. |
| Ba vòng giải quyết ba vấn đề khác nhau thế nào? | Vòng 1 phân cấp thị giác (thêm nút Primary, bỏ khối Sky). Vòng 2 hoàn thành tác vụ (nút bộ lọc bị đẩy ra ngoài màn hình, thẻ sinh viên không có nút mời). Vòng 3 accessibility (79 trên 203 dòng bị cắt chuyên ngành, tương phản 3,3 → 6,08). |
| Đổi primary color hoặc spacing thì màn nào tự cập nhật? | Tất cả, vì không có giá trị gõ tay – màu, chữ, khoảng cách đều gắn variables. Ví dụ sửa component Banner hạn chót thì 53 instance đổi theo. |
| Chứng minh 412 dp không vỡ layout? | Có 26 frame bản 412 trong section "Kiểm tra 412" cho các màn trong 3 flow. Auto layout dùng Fill theo chiều ngang, theo quy tắc co giãn trong DESIGN.md mục 1.1. |
| Lập trình viên Flutter dựa vào đâu để hiện thực? | `flutter-handoff.md`: hai bảng ánh xạ và sáu mục cho từng màn; kèm frame trong Figma 03 và file Excel danh sách màn hình cho đủ trạng thái. |
| Persona có cơ sở không? | Là proto-persona: số liệu thật từ file FA26 và quy định môn, phần còn lại ghi rõ là giả định. Danh sách giả định A1–A4 cần phỏng vấn nằm trong persona.md mục 8. |

---

## Lưu ý về page Figma

Hướng dẫn chấm ghi "kiểm tra đúng 6 page". Theo `ai-design-log.md`, file Figma đang để `02 Wireframe`, `03 Final UI`, `06 Prototype`… là **section trong page `Mobile`**, vì gói Starter chỉ cho 3 page. Nếu thầy hỏi, nói rõ lý do này; nếu được thì nâng gói hoặc tách thành 6 page trước buổi chấm.
