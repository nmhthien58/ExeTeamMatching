# Persona – EXE Team Matching System

> Artifact cho **Slide 02 – Problem & Persona** (PRM323 Lab 2).
> Theo [Persona Guideline](https://prm393-material.web.app/persona-guideline) của môn: mỗi persona có **12 trường lõi**; với Lab 2, tuổi, vai trò, thói quen thiết bị và nhu cầu accessibility là bắt buộc.
> Cập nhật: 19/10/2026 – chuyển sang template 12 trường, khớp Slide 02.
> **Cập nhật lần 2 (19/10/2026, sau góp ý của giảng viên – E03):** thêm **P5 Mentor**; bổ sung nhu cầu *sau khi đã có nhóm* (trao đổi, file, task, lịch họp) cho P1 và P3, nhu cầu tìm đề tài cho P3, hồ sơ 5 kỹ năng và ý tưởng khi xin vào nhóm cho P2. Phần bổ sung ghi **(bổ sung)** và đều là giả định cần kiểm chứng (mục 8). Nội dung dùng cho Lab 2 và Figma giữ nguyên.

**Trạng thái bằng chứng.** Đây là **proto-persona**: dựng từ dữ liệu vận hành và quy định môn học, **chưa phỏng vấn hay quan sát người dùng**. Tên, tuổi và câu trích dẫn là giả lập. Mỗi trường được gắn nhãn nguồn:

| Nhãn | Nghĩa |
|---|---|
| **O** – Observed | Thấy trực tiếp trong dữ liệu có mã nguồn (E01) |
| **R** – Reported | Ghi trong tài liệu quy định của môn (E02) |
| **I** – Inferred | Nhóm suy luận từ pattern trong O/R, có ghi lý do |
| **A** – Assumed | Chưa có bằng chứng, phải kiểm chứng (xem mục 8) |

---

## 1. Bằng chứng

| Mã | Nguồn | Loại | Ngày lấy |
|---|---|---|---|
| **E01** | File `FA26_EXE201 \| Group List` (Google Sheets, 7 sheet) | Observed – số đếm thật | 09/2026 |
| **E02** | Quy định môn EXE101/EXE201, tóm tắt ở `PROJECT_CONTEXT.md` mục 3 | Reported – quy tắc | 09/2026 |
| **E03** | Góp ý của giảng viên môn ngày 19/10/2026 (10 điểm, xem `PROJECT_CONTEXT.md` mục 13) | Reported – ý kiến chuyên gia, **không phải** dữ liệu người dùng | 19/10/2026 |

**Quan sát từ E01**

| Quan sát | Số liệu | Hệ quả |
|---|---|---|
| Quy mô sheet chính | ~220 nhóm, 1.252 dòng sinh viên, 17 cột | Phải cuộn và dò tay, dễ ghi đè dòng của nhóm khác |
| Sinh viên không tìm được nhóm trước hạn 18h 17/09 | Team 214–220 do bộ môn tự gom | Bộ môn ghép tay, sinh viên bị xếp vào nhóm lạ |
| Nhóm chỉ có 1 chuyên ngành hẹp | ~9 nhóm | Vi phạm quy định liên ngành nhưng không ai chặn |
| Yêu cầu đổi thành viên EXE201 | 26 đã duyệt (đợt 1) + 58 chờ duyệt (đợt 2) | Mỗi yêu cầu là một khối dòng nhập tay; có ô tỷ lệ gõ sai (`20` thay vì `0.2`) |
| Kết quả duyệt | 65 ghi chú tự do kiểu "Đã được PĐT duyệt…", có ghi chú "cần check lại" | Không có trạng thái, không có lịch sử, không biết ai duyệt lúc nào |
| GV bộ môn điều tiết khác GV nhóm mong muốn | 55 nhóm (~25%) | Nhóm chỉ biết khi tự mở lại file |
| Bài đăng tìm người / tìm nhóm | 17 nhóm tìm thành viên, 10 SV tìm nhóm | Mô tả tự do, không lọc được theo chuyên ngành; có email thiếu `@` |
| Dữ liệu cá nhân | Cột SĐT Zalo của leader, email của mọi SV công khai; SĐT lưu dạng số nên mất số 0 đầu | Lộ thông tin, dữ liệu hỏng |

**Phạm vi nền tảng** (E02, E03): Sinh viên, Giảng viên và Mentor dùng cả **Mobile App (Flutter)** và **Web App (React)**; Bộ môn EXE dùng Web App. Phạm vi chấm Lab 2 là mobile.

---

## 2. Persona Coverage Matrix

| # | Archetype (nhãn hành vi) | Ưu tiên | Tác vụ chính | Thiết bị / rào cản | Khác biệt cần thiết kế | Flow |
|---|---|---|---|---|---|---|
| P1 | **Minh** – trưởng nhóm chạy deadline, xử lý mọi việc trên điện thoại | **Primary** | Thay thành viên và gửi yêu cầu đổi nhóm; (bổ sung) điều phối việc, file, lịch họp của nhóm | Android 360 dp, 4G yếu, vài phút giữa tiết | Trạng thái yêu cầu luôn thấy; lỗi mạng không mất dữ liệu | F2 |
| P2 | **Vy** – người mới ngoài ngành, ngại chủ động nhắn người lạ | Secondary | Tìm và vào một nhóm còn chỗ, (bổ sung) kèm ý tưởng của mình | iPhone cũ, buổi tối, ít quen người IT | Biết nhóm nào còn chỗ và cần mình; xin vào không cần nhắn tin | F1 |
| P3 | **Thầy Khoa** – người duyệt nhanh giữa các tiết | Supporting | Nhận / từ chối yêu cầu hướng dẫn (Lab 2; từ 20/10 thay bằng Quan tâm + hệ thống ghép); (bổ sung) tìm đề tài hợp chuyên môn, theo dõi bài nộp, câu hỏi, checkpoint (EXE101) và lịch họp hằng tuần (EXE201) của các nhóm | Điện thoại 412 dp, bị gián đoạn liên tục | Hành động ngay từ thông báo; không nhận quá sức chứa | F3 |
| P4 | **Cô Hạnh** – người vận hành dữ liệu lớn | Supporting (web, ngoài phạm vi Lab 2) | Duyệt đổi nhóm, gom SV lẻ, điều tiết GV, (bổ sung) tạo và gán mentor | Laptop, bảng hàng nghìn dòng | Bảng lọc/so sánh, thao tác hàng loạt, lưu vết | Web |
| P5 | **Anh Tuấn** – mentor doanh nghiệp, hỗ trợ ngoài giờ làm (bổ sung) | Supporting | Từ tuần 6, họp 2–3 buổi với mỗi nhóm; xem tài liệu mới nhất, trả lời câu hỏi | Laptop buổi tối + điện thoại; không phải người của trường | Gom việc cần mình vào một danh sách; chốt lịch họp ít lượt; chỉ xem, không duyệt | F4 |

**Vì sao Minh là Primary.** F2 là flow có nhiều bước và nhiều điểm lỗi nhất (lời mời, thay đổi chờ duyệt, lý do bắt buộc, gửi qua 4G, chờ Bộ môn). Nếu giao diện đáp ứng được Minh – trạng thái luôn thấy, lỗi phục hồi được, hành động chính nổi bật – thì Vy và thầy Khoa cũng được đáp ứng phần lớn. Ngược lại, thiết kế chỉ cho Vy sẽ thiếu phần "thay đổi chờ duyệt" của nhóm kế thừa.

**Vì sao không gộp P1 và P2** dù cùng là sinh viên: Minh đã có nhóm và cần *quản lý thay đổi*; Vy chưa có nhóm và cần *tìm chỗ*. Hai mục tiêu dẫn tới hai cấu trúc màn hình khác nhau (S15/S27–S29 so với S05/S08/S09).

**Vì sao Mentor là persona riêng, không gộp vào thầy Khoa** (bổ sung): cả hai dùng chung màn hình workspace, nhưng mentor không có quyền với nhóm (không nhận yêu cầu, không duyệt, không tạo bài nộp), không quen hệ thống của trường và chỉ có thời gian buổi tối. Khác biệt đó dẫn tới quyết định: mentor do Bộ môn tạo, trang chủ chỉ có "việc cần mình" và lịch họp, không có các mục quản lý.

---

## 3. P1 – Minh · Primary Persona

| # | Trường | Nội dung | Nguồn |
|---|---|---|---|
| 01 | **Identity & Archetype** | Minh – *trưởng nhóm chạy deadline, xử lý mọi việc trên điện thoại* | A |
| 02 | **Persona Priority** | **Primary.** Quyết định thiết kế của F2 (đổi thành viên) và luồng mời người từ S10/S18 | – |
| 03 | **Usage Context** | Mở app trong giờ nghỉ giữa tiết, vài phút mỗi lần, vừa nhắn Zalo với nhóm; thường ở hành lang hoặc ngoài sân | A |
| 04 | **Goals** | 1) Nhóm đủ người, đúng chuyên ngành còn thiếu, trước hạn đổi nhóm. 2) Biết chắc yêu cầu đã tới Bộ môn và đang ở trạng thái nào. 3) Giữ được dự án từ EXE101. 4) **(bổ sung)** Giữ việc, file và lịch họp của nhóm ở một chỗ, không trôi trong Zalo | I (từ E01, E02); 4) A (E03) |
| 05 | **Job to Be Done** | *Khi một thành viên không học tiếp EXE201 sát hạn, tôi muốn gửi một yêu cầu đổi người có lý do, để được duyệt kịp và nhóm không bị Bộ môn xếp lại.* | I |
| 06 | **Current Behaviors** | Chép danh sách thành viên cũ và mới vào sheet "Đăng ký thay đổi nhóm", tự tính số lượng và tỷ lệ thay đổi **(O, E01)**. Tìm người bằng cách đọc bài đăng tự do rồi nhắn Zalo từng người **(I – suy từ cột SĐT Zalo và 17 bài tìm người trong E01)**. **(bổ sung)** Trong kỳ: giao việc và gửi file trong nhóm Zalo, file nằm trên Drive cá nhân **(A)** | O / I / A |
| 07 | **Pain Points** | 1) Kết quả duyệt chỉ là 65 ghi chú tự do, không có trạng thái **(O)** → phải mở file nhiều lần để dò **(I)**. 2) Tự tính tỷ lệ nên dễ sai, đã có ô gõ `20` thay vì `0.2` **(O)**. 3) SĐT của mình công khai trong file hơn 1.000 người xem **(O)**. 4) **(bổ sung)** Câu hỏi gửi giảng viên trôi giữa tin nhắn nhóm, không biết thầy đã đọc chưa; không biết bản file nào là bản mới nhất **(A, E03)** | O / I / A |
| 08 | **Motivations** | Chắc chắn, không bất ngờ trước hạn; giữ quyền tự chọn đồng đội thay vì bị ghép | A |
| 09 | **Constraints** | Chỉ trưởng nhóm được gửi; thay đổi chỉ có hiệu lực khi Bộ môn duyệt **(R, E02 quy tắc 7)**. Mạng 4G chập chờn, thời gian ngắn **(A)** | R / A |
| 10 | **Key Scenarios** | **Chính:** mời người thay thế → người đó đồng ý → thay đổi nằm ở "chờ duyệt" → gửi yêu cầu kèm lý do → nhận kết quả. **Lỗi:** mất mạng khi gửi → lý do vẫn còn, bấm Thử lại; bị từ chối → xem lý do, tạo yêu cầu mới. **Trong kỳ (bổ sung):** đề xuất 2 khung giờ họp với mentor → mọi người chọn → chốt → biên bản thành task | R / A |
| 11 | **Success Criteria** | Trên prototype F2: gửi yêu cầu đổi nhóm **≤ 2 phút, không cần trợ giúp**; **5/5** người thử đọc đúng trạng thái yêu cầu ở S29. Đo bằng usability test 5 người: thời gian, số lỗi thao tác, câu trả lời "yêu cầu đang ở đâu?" | – |
| 12 | **Evidence & Status** | Proto-persona. E01 (O), E02 (R); các trường còn lại là giả định, chưa phỏng vấn. Cần kiểm chứng A1–A4 ở mục 8 | – |

**Optional details (bắt buộc với Lab 2)**

| Mục | Nội dung | Nguồn |
|---|---|---|
| Tuổi, vai trò | 21 tuổi, năm 3 Kỹ thuật phần mềm; leader nhóm 6 người từ EXE101 | A |
| Thói quen thiết bị | Android tầm trung (~360 dp), 4G; quen Zalo, FAP, Google Sheets trên điện thoại | A |
| Nhu cầu accessibility | Dùng **một tay** khi đang di chuyển → hành động chính ở nửa dưới màn hình, vùng chạm ≥ 48 dp. Đọc **ngoài trời nắng** → tương phản chữ ≥ 4,5:1, chữ nội dung ≥ 14 sp, trạng thái không chỉ phân biệt bằng màu | A |
| Câu nói (giả lập) | "Em gửi yêu cầu đổi thành viên rồi mà không biết bộ môn đã đọc chưa, ngày nào cũng phải mở file ra dò." | Giả lập, không phải phát biểu thật |

---

## 4. P2 – Vy · Secondary Persona

| # | Trường | Nội dung | Nguồn |
|---|---|---|---|
| 01 | Identity & Archetype | Vy – *người mới ngoài ngành, ngại chủ động nhắn người lạ* | A |
| 02 | Priority | **Secondary.** Quyết định F1 (tham gia nhóm) và màn S05/S08/S09 | – |
| 03 | Usage Context | Buổi tối ở ký túc xá, có thời gian lướt danh sách | A |
| 04 | Goals | 1) Vào được một nhóm đang thực sự cần chuyên ngành của mình. 2) Không rơi vào danh sách "bộ môn tự ghép". 3) Giới thiệu được kỹ năng mà không lộ SĐT – **(bổ sung)** bằng 5 thang kỹ năng 1–5 và một đoạn mô tả. 4) **(bổ sung)** Cho trưởng nhóm thấy mình có ý tưởng gì, hoặc nói rõ là chưa có | I (E01: Team 214–220); 3–4) A (E03) |
| 05 | Job to Be Done | *Khi đợt đăng ký sắp đóng mà chưa có nhóm, tôi muốn biết nhóm nào còn chỗ và cần người như mình, để xin vào mà không phải nhắn từng leader.* | I |
| 06 | Current Behaviors | Đăng mô tả tự do ở sheet "SV tìm nhóm" **(O – 10 bài)**, rồi chờ hoặc nhắn Zalo xin vào **(A)** | O / A |
| 07 | Pain Points | 1) Không biết nhóm nào còn chỗ, nhóm nào đã đủ **(O – không có cột số chỗ)**. 2) Không lọc được theo domain hay chuyên ngành **(O)**. 3) Nhắn xin vào rồi không ai trả lời, không biết nên chờ hay tìm nhóm khác **(A)**. 4) **(bổ sung)** Không có chỗ nói ý tưởng của mình ngoài mô tả tự do **(A, E03)** | O / A |
| 08 | Motivations | Được chọn nhóm hợp ý tưởng; tránh cảm giác bị từ chối trực tiếp | A |
| 09 | Constraints | Mỗi SV chỉ thuộc 1 nhóm/kỳ/môn; lời mời và yêu cầu hết hạn sau 72 giờ **(R)**. Ít quen người ngoài ngành **(A)** | R / A |
| 10 | Key Scenarios | **Chính:** đăng nhập Google → hoàn thiện hồ sơ (một màn: 5 kỹ năng + mô tả) → xem nhóm còn chỗ → xin tham gia kèm 1–3 ý tưởng hoặc tích "chưa có ý tưởng" → được chấp nhận. **Lỗi:** email không có trong danh sách học kỳ → A04 hướng dẫn liên hệ bộ môn; bị từ chối / hết hạn → gợi ý nhóm khác | R |
| 11 | Success Criteria | Gửi yêu cầu tham gia **≤ 3 lần chạm** từ danh sách nhóm; người thử chỉ ra đúng "nhóm còn mấy chỗ" ở S05 | – |
| 12 | Evidence & Status | Proto-persona; E01 (10 bài tìm nhóm, Team 214–220). Chưa phỏng vấn | – |

**Optional:** 20 tuổi, năm 2 Thiết kế mỹ thuật số, lần đầu học EXE · iPhone đời cũ, buổi tối dùng thêm laptop (web) · Hồ sơ (bổ sung): Thiết kế UI/UX 4/5, Plan truyền thông 3/5, Công nghệ 1/5 · Accessibility: đọc buổi tối trên màn hình nhỏ → chữ ≥ 14 sp, chip chuyên ngành không bị cắt chữ.
**Khác P1:** Vy cần *tìm chỗ*, Minh cần *quản lý thay đổi* của nhóm đã có.

---

## 5. P3 – Thầy Khoa · Supporting Persona

| # | Trường | Nội dung | Nguồn |
|---|---|---|---|
| 01 | Identity & Archetype | Thầy Khoa – *người duyệt nhanh giữa các tiết* | A |
| 02 | Priority | **Supporting.** Quyết định F3 và màn L01–L04 | – |
| 03 | Usage Context | Mở app từ thông báo, giữa hai tiết dạy, vài chục giây | A |
| 04 | Goals | 1) Trả lời yêu cầu hướng dẫn mà không nhận quá số nhóm cho phép. 2) Biết nhóm nào chính thức là của mình. 3) **(bổ sung)** Tìm được đề tài hợp chuyên môn mà không phải tranh với đồng nghiệp. 4) **(bổ sung)** Theo dõi câu hỏi, bài nộp, lịch họp của các nhóm ở một chỗ. 5) **(bổ sung 20/10)** Không phải xử lý từng yêu cầu: bày tỏ quan tâm một lần, hệ thống ghép; nắm được nhóm nào chưa trình bày checkpoint (EXE101) và lịch họp 15 phút của ~40 nhóm (EXE201) | R / I; 3–4) A (E03); 5) R |
| 05 | Job to Be Done | *Khi có nhóm nhờ hướng dẫn, tôi muốn xem nhanh nhóm đó rồi nhận hoặc từ chối, để không phải theo dõi nhiều kênh.* **(bổ sung)** *Khi mở ứng dụng giữa hai tiết, tôi muốn thấy ngay nhóm nào đang chờ tôi trả lời hoặc trễ hạn nộp, để xử lý việc gấp trước.* | I / A |
| 06 | Current Behaviors | Lọc cột "Điều tiết GV của bộ môn (Final)" trong sheet 1.250 dòng **(O)**; nhận lời nhờ qua Zalo, email, gặp trực tiếp **(A)**. **(bổ sung)** Đầu kỳ chờ nhóm tự tìm tới; trong kỳ nhận báo cáo qua email và nhóm Zalo của từng nhóm **(A)**. **(bổ sung 20/10, R)** EXE101: nhóm lên trình bày Checkpoint 1–4 trên lớp bất kỳ buổi nào thầy dạy (T2–T6), điểm nhập trên FAP. EXE201: sinh viên không lên lớp, thầy chốt lịch họp online 15 phút mỗi tuần với từng nhóm trong khoảng 2 tuần đầu; một thầy có thể hướng dẫn 38–40 nhóm EXE201, EXE101 còn nhiều hơn | O / A / R |
| 07 | Pain Points | 1) 55 nhóm (~25%) được điều tiết khác nguyện vọng **(O)** → đã đồng ý với nhóm nhưng bị phân nhóm khác mà không ai báo **(I)**. 2) Thành viên nhóm đổi giữa chừng không được thông báo **(A)**. 3) **(bổ sung)** Hai giảng viên cùng muốn một đề tài: ai liên hệ trước thì được, người kia mất công **(A, E03)**. 4) **(bổ sung)** Câu hỏi và file của 8 nhóm rải ở nhiều kênh, khó biết nhóm nào đang chờ mình **(A)** | O / I / A |
| 08 | Motivations | Ít thao tác, không phải nhớ đã hứa với nhóm nào | A |
| 09 | Constraints | Số nhóm do Bộ môn quyết (có thể đặt tối đa, có thể không); quyết định cuối thuộc Bộ môn **(R, quy tắc 9)**. Từ 20/10: không còn bước chấp nhận/từ chối, giảng viên chỉ bấm Quan tâm | R |
| 10 | Key Scenarios | **Chính:** nhận thông báo → xem chi tiết nhóm → chấp nhận. **Lỗi:** đã đủ sức chứa → nút Chấp nhận bị khóa kèm lý do, chỉ có thể từ chối. **(bổ sung, sửa 20/10)** Mở *Đề tài* → quan tâm vài đề tài, xem tab "Nhóm chọn tôi" → hết hạn lập nhóm, hệ thống ghép, thầy thấy nhóm mới kèm lý do "Hai bên cùng chọn"; EXE201: đăng khung họp, các nhóm tự chọn slot; EXE101: trong giờ dạy lọc "Chưa trình bày CP2" và đánh dấu từng nhóm; trong kỳ tạo bài nộp "Báo cáo tuần 3" và duyệt bản nộp | R / A |
| 11 | Success Criteria | Phản hồi một yêu cầu trong ≤ 4 lần chạm từ thông báo; người thử phân biệt đúng "Tôi đã nhận" và "Bộ môn đã phân công" ở L04. **(bổ sung)** Từ trang chủ, chỉ ra nhóm đang chờ mình trả lời hoặc trễ hạn trong ≤ 10 giây | – |
| 12 | Evidence & Status | Proto-persona; E01 (55 nhóm điều tiết khác). Chưa phỏng vấn | – |

**Optional:** 38 tuổi, giảng viên dạy 4–5 lớp · điện thoại ~412 dp · Accessibility: thao tác nhanh một tay, nút hành động đủ lớn, không phải cuộn để thấy việc cần làm.
**Khác P1:** không tạo hay sửa nhóm; chỉ phản hồi và theo dõi.

---

## 6. P4 – Cô Hạnh · Supporting Persona (web, ngoài phạm vi chấm Lab 2)

| # | Trường | Nội dung | Nguồn |
|---|---|---|---|
| 01 | Identity & Archetype | Cô Hạnh – *người vận hành dữ liệu lớn* | A |
| 02 | Priority | **Supporting, web.** Không thuộc phạm vi mobile của Lab 2; giữ lại để thiết kế mobile không mâu thuẫn với quy trình duyệt | – |
| 03 | Usage Context | Văn phòng, laptop/màn hình rời; cao điểm là tuần đóng đợt | A |
| 04 | Goals | Mọi SV thuộc một nhóm hợp lệ đúng hạn; duyệt đổi nhóm có căn cứ và lưu vết; phân công GV cân bằng | R |
| 05 | Job to Be Done | *Khi đóng đợt đăng ký, tôi muốn thấy ngay ai chưa có nhóm và yêu cầu nào đang chờ, để xử lý hết trong một màn hình.* | I |
| 06 | Current Behaviors | Khóa/mở cột bằng tay, ghi kết quả bằng ghi chú tự do, tự gom Team 214–220 **(O)** | O |
| 07 | Pain Points | Không có trạng thái, không có lịch sử; SV sửa nhầm dòng nhóm khác; không cảnh báo nhóm thiếu chuyên ngành **(O / I)** | O / I |
| 08 | Motivations | Chính xác, có căn cứ khi bị hỏi lại | A |
| 09 | Constraints | Gộp vai trò Bộ môn và Phòng Đào tạo **(R)** | R |
| 10 | Key Scenarios | Import danh sách → mở đợt → duyệt change request → rà soát `NeedsReview` → soát kết quả ghép giảng viên (W19, có thể "Ghép sớm") và mentor (W28), sửa ngoại lệ | R |
| 11 | Success Criteria | Trung vị xử lý một change request ≤ 24 giờ; 100% có trạng thái và người duyệt | – |
| 12 | Evidence & Status | Proto-persona; E01. Chưa phỏng vấn | – |

---

## 6a. P5 – Anh Tuấn · Supporting Persona (bổ sung 19/10/2026)

| # | Trường | Nội dung | Nguồn |
|---|---|---|---|
| 01 | Identity & Archetype | Anh Tuấn – *mentor doanh nghiệp, hỗ trợ ngoài giờ làm* | A |
| 02 | Priority | **Supporting.** Quyết định flow F4 (họp với mentor) và trang chủ mentor M01; dùng chung màn hình workspace với giảng viên | – |
| 03 | Usage Context | Chỉ tham gia một cửa sổ ngắn của kỳ – **từ tuần 6, 2–3 buổi họp mỗi nhóm** **(R – thông tin từ nhóm, 19/10)**; buổi tối sau giờ làm, trên laptop (web); điện thoại khi nhận nhắc lịch **(A)** | R / A |
| 04 | Goals | 1) Vào cuộc nhanh khi được gán giữa kỳ: hiểu nhóm đang ở đâu mà không phải đọc lại cả nửa kỳ. 2) Chốt giờ cho 2–3 buổi họp nhanh. 3) Đọc bản tài liệu mới nhất trước buổi họp | A (E03) |
| 05 | Job to Be Done | *Khi chuẩn bị buổi mentor, tôi muốn xem nhanh tiến độ và tài liệu mới nhất của nhóm và chốt được giờ họp, để buổi họp 30 phút thật sự có giá trị.* | A |
| 06 | Current Behaviors | Được thêm vào nhóm Zalo của từng nhóm, nhận link Drive, hẹn giờ bằng tin nhắn qua lại | A |
| 07 | Pain Points | 1) Hẹn một buổi họp mất nhiều tin nhắn qua lại giữa 6–7 người. 2) Link Drive hết quyền xem hoặc không rõ bản nào mới. 3) Nhóm chat ồn ào, câu hỏi thực sự dành cho mình bị trôi. 4) Được gán từ tuần 6 nên thiếu bối cảnh: không biết nhóm đã làm gì từ đầu kỳ (và từ EXE101 nếu là EXE201) | A |
| 08 | Motivations | Đóng góp cho sinh viên mà không tốn thời gian hành chính | A |
| 09 | Constraints | Có thể là người ngoài trường hoặc giảng viên của trường; không có trong danh sách import → Bộ môn tạo hồ sơ; mentor bấm quan tâm đề tài trước tuần 6, hệ thống ghép, sinh viên không chọn mentor **(R, 20/10)**; **mỗi nhóm một mentor**; số nhóm tối đa do Bộ môn đặt riêng cho từng mentor, mentor tự chọn số nhóm mong muốn **(R – quy tắc 20)**. Chỉ xem và góp ý, không duyệt **(R)** | R |
| 10 | Key Scenarios | Bộ môn tạo hồ sơ → email mời đăng nhập Google → tuần 4–5 xem *Đề tài*, quan tâm 4 đề tài hợp chuyên môn → tuần 6 hệ thống ghép → M01 thấy 3 nhóm, mỗi nhóm "Buổi mentor 0/3" → mở tóm tắt nhóm (đề tài, tiến độ, file mới nhất) → đề xuất 2 khung giờ → nhóm chọn → chốt → nhắc 1 giờ trước → ghi biên bản, chuyển 2 việc thành task → "1/3" | R / A |
| 11 | Success Criteria | Hiểu được nhóm đang ở đâu trong ≤ 2 phút từ lúc được gán; chốt lịch họp trong ≤ 2 lượt tương tác; mở bản file mới nhất trong ≤ 3 chạm; mọi câu hỏi dành cho mentor nằm trong "Cần trả lời" | – |
| 12 | Evidence & Status | Proto-persona dựng từ góp ý E03; **chưa có dữ liệu thật** về mentor. Kiểm chứng A5 | – |

**Optional:** 32 tuổi, product manager ở một startup, mentor tình nguyện 2–3 nhóm EXE · laptop + điện thoại · Accessibility: đọc trên cả laptop và điện thoại; ít thông báo, gom thành email tóm tắt; không cần học thuật ngữ nội bộ của trường.
**Khác P3:** cùng màn hình workspace nhưng không có nguyện vọng của nhóm, checkpoint, lịch họp hằng tuần hay bài nộp; trang chủ chỉ có việc cần mình và lịch họp.

---

## 7. Problem statement

> **Trưởng nhóm EXE201 cần thay người sát hạn ngay trên điện thoại và luôn biết yêu cầu đổi nhóm đang ở đâu.** Hiện tại việc ghép nhóm cho ~1.250 sinh viên mỗi kỳ nằm trên một Google Sheet nhập tay, không kiểm tra quy định, không có trạng thái duyệt và không có lịch sử (E01).
> Hệ thống cũng phải giúp sinh viên chưa có nhóm tìm được nhóm còn chỗ trước hạn (P2) và giảng viên phản hồi yêu cầu hướng dẫn trong vài thao tác (P3).
> **(bổ sung)** Sau khi lập nhóm, trao đổi giữa nhóm, giảng viên và mentor, cùng file, task và lịch họp đang rải rác trên Zalo, email và Drive cá nhân. Hệ thống cần một **workspace cho mỗi nhóm** để câu hỏi không bị trôi, bản file mới nhất luôn rõ và lịch họp chốt trong vài thao tác (P1, P3, P5).

---

## 8. Giả định cần kiểm chứng (research backlog)

| # | Giả định | Persona | Cách kiểm chứng |
|---|---|---|---|
| A1 | Trưởng nhóm xử lý việc nhóm chủ yếu trên điện thoại, giữa giờ học | P1 | Phỏng vấn 3 trưởng nhóm EXE201: "Lần gần nhất đổi thành viên, bạn làm trên thiết bị nào, ở đâu?" |
| A2 | Không biết trạng thái duyệt là nỗi lo lớn nhất khi đổi nhóm | P1 | Phỏng vấn + đếm số lần mở lại file trong tuần đổi nhóm (nếu có lịch sử phiên bản) |
| A3 | Sinh viên chưa có nhóm ngại nhắn tin trực tiếp cho leader | P2 | Phỏng vấn 3 SV thuộc Team 214–220 |
| A4 | Giảng viên chủ yếu vào app từ thông báo | P3 | Phỏng vấn 2 giảng viên; quyết định B10 phụ thuộc giả định này |
| A5 | Hẹn lịch họp với mentor/giảng viên tốn nhiều lượt tin nhắn; câu hỏi dành cho cố vấn bị trôi trong nhóm chat | P1, P3, P5 | Phỏng vấn 2 mentor và 3 trưởng nhóm; đếm số tin nhắn để chốt một buổi họp trong một nhóm Zalo thật (ẩn danh) |
| A6 | Hai giảng viên cùng muốn một đề tài xảy ra đủ thường để cần cơ chế riêng | P3 | Hỏi Bộ môn số trường hợp ở kỳ FA26; đối chiếu 55 nhóm điều tiết khác nguyện vọng trong E01 |
| A7 | Thang 1–5 cho 5 kỹ năng đủ để trưởng nhóm đánh giá người xin vào | P1, P2 | Thử với 5 trưởng nhóm: chọn 2 trong 5 hồ sơ, hỏi lý do |
| A8 | Sinh viên muốn đưa ý tưởng khi xin vào nhóm, và trưởng nhóm thực sự đọc | P1, P2 | Theo dõi tỷ lệ yêu cầu có ý tưởng và tỷ lệ được nhận khi chạy thử |

---

## 9. Chỉ số thành công (đo được)

| # | Chỉ số | Hiện tại (E01) | Mục tiêu | Cách đo |
|---|---|---|---|---|
| 1 | Thời gian gửi yêu cầu đổi nhóm (P1) | Không đo được | ≤ 2 phút, không trợ giúp | Usability test 5 người trên prototype F2 |
| 2 | Người thử đọc đúng trạng thái yêu cầu (P1) | Không có trạng thái | 5/5 | Hỏi "yêu cầu của bạn đang ở đâu?" tại S29 |
| 3 | Thao tác gửi yêu cầu tham gia nhóm (P2) | Đọc sheet + nhắn Zalo | ≤ 3 lần chạm từ danh sách nhóm | Kiểm thử prototype F1 |
| 4 | Tỷ lệ hoàn thành 3 flow trên prototype | — | ≥ 90% không cần trợ giúp | Usability test 5 người |
| 5 | Sinh viên chưa có nhóm khi đóng đợt | ~7 nhóm phải ghép tay (Team 214–220) | Giảm ≥ 50%, phần còn lại ghép tự động | Số SV qua `AutoAssignUnmatchedJob` |
| 6 | Nhóm vi phạm quy định khi chốt (4–6 người, ≥ 2 chuyên ngành) | ~9 nhóm thiếu chuyên ngành thứ hai | 0 nhóm `Finalized` vi phạm | Kết quả `ValidateComposition` |
| 7 | Thời gian xử lý một change request | Không đo được (ghi chú tự do) | Trung vị ≤ 24 giờ, 100% có trạng thái và người duyệt | `ChangeRequest` submitted → approved/rejected |
| 8 | ~~Thời gian giảng viên phản hồi yêu cầu hướng dẫn~~ – bỏ từ 20/10/2026 vì không còn bước chấp nhận/từ chối; thay bằng chỉ số 15 | Không đo được | – | – |
| 9 | Dữ liệu sai định dạng (email, MSSV, SĐT) | Có email thiếu `@`, SĐT mất số 0 | 0 bản ghi sai | Validation khi nhập/import |
| 10 | SĐT hiển thị cho người ngoài nhóm | Công khai toàn bộ | 0 | Kiểm tra phân quyền theo màn hình |
| 11 | Thời gian hoàn tất onboarding (bổ sung) | Hai bước, có SĐT/Zalo | ≤ 1 phút, một màn hình | Đo trên prototype A05 mới |
| 12 | Lượt tương tác để chốt một buổi họp (bổ sung) | Nhiều tin nhắn qua lại (A5) | ≤ 2 lượt của người tổ chức | `MeetingProposed` → `MeetingScheduled` |
| 13 | Thời gian cố vấn trả lời câu hỏi (bổ sung) | Không đo được | Trung vị ≤ 24 giờ | `AdvisorQuestionAsked` → `AdvisorQuestionAnswered` |
| 14 | Đề tài có nhiều giảng viên quan tâm phải xử lý tay (bổ sung) | Không đo được | 0 trường hợp Bộ môn phải can thiệp vì tranh chấp | `TopicInterestOpened` ≥ 2 trên cùng nhóm → ghép theo nguyện vọng → các quan tâm khác tự đóng |
| 15 | Nhóm được ghép đúng một nguyện vọng hoặc giảng viên quan tâm (bổ sung 20/10) | ~75% được đúng nguyện vọng (55 nhóm điều tiết khác, E01) | ≥ 90%; nhóm Bộ môn sửa tay ≤ 5% | Lý do ghép trong `LecturerAssigned` (`Mutual`, `Preference1–3`, `LecturerInterest` so với `Domain`, `Manual`) |
| 16 | Nhóm EXE201 có lịch họp hằng tuần trước hạn chọn slot (bổ sung 20/10) | Chốt qua tin nhắn, không đo được | ≥ 90% tự chọn, còn lại xếp tự động; 0 nhóm không có lịch sau tuần 2 | `MeetingSlotBooked` trước `SlotBookingCloseJob` |

*Chỉ số 1–4 đo được ngay trong Lab 2 bằng prototype; chưa có kết quả test. Các chỉ số còn lại đo khi hệ thống chạy thật.*

---

## 10. Persona → Need → UI decision → Test

| Persona | User need (kết quả mong muốn) | Quyết định / giả thuyết UI | Màn hình | Cách kiểm chứng |
|---|---|---|---|---|
| P1 | Biết chắc yêu cầu đang ở đâu | Dòng thời gian Đã gửi → Chờ Bộ môn duyệt → Kết quả; mọi yêu cầu có trạng thái và mốc thời gian | S29, S15 | 5/5 người thử đọc đúng trạng thái |
| P1 | Không mất công khi mạng chập chờn | Lỗi gửi hiện tại chỗ, giữ nguyên lý do, có nút Thử lại; không dùng snackbar cho lỗi (A5) | S28 / Lỗi mạng | Ngắt mạng khi gửi, đo người thử có gửi lại được không |
| P1 | Không bị bất ngờ vì thay đổi chưa có hiệu lực | Tách "Thành viên hiện tại" và "Thay đổi đang chờ duyệt" (A2) | S15, S17, S27 | Hỏi người thử "ai đang là thành viên chính thức?" |
| P1 | Làm việc gấp nhất trước hạn | Thẻ việc cần làm có một nút Primary ngay dưới banner hạn chót (vòng 1, mục 1.4) | S01 / Là trưởng nhóm | Đo thời gian từ trang chủ tới S18 |
| P1, P2 | Đọc được ngoài nắng, dùng một tay | Tương phản chữ ≥ 4,5:1, viền điều khiển ≥ 3:1 (A1), vùng chạm 48 dp, trạng thái có icon + chữ (A3) | Toàn bộ | Đo tự động trong Figma; thử trên điện thoại thật ngoài trời |
| P2 | Biết nhóm nào còn chỗ và cần mình | Thẻ nhóm ghi "n/6 thành viên · còn k chỗ" và chuyên ngành đang thiếu; ẩn nhóm đã đủ (B6) | S05, S08 | Người thử chỉ ra nhóm phù hợp trong ≤ 30 giây |
| P2 | Xin vào nhóm mà không phải nhắn tin | Bottom sheet xin tham gia, nhắc thời hạn 72 giờ | S09 | ≤ 3 lần chạm từ S05 |
| P3 | Không nhận quá sức chứa | Bộ đếm "Đã nhận 6/8"; nút Chấp nhận khóa khi đủ, kèm lý do (B13) | L01, L03 | Người thử giải thích được vì sao không chấp nhận được |
| P3 | Phân biệt cam kết của mình với quyết định chính thức | Hai mục "Tôi đã nhận" (tham khảo) và "Bộ môn đã phân công" (chính thức) | L04 | Hỏi người thử nhóm nào là chính thức |
| P1 | (bổ sung) Đánh giá người xin vào nhanh | Yêu cầu tham gia hiện 5 thanh kỹ năng so với chỗ trống của nhóm và 1–3 ý tưởng | S20 | Trưởng nhóm quyết định trong ≤ 1 phút mỗi yêu cầu |
| P2 | (bổ sung) Nói được mình có ý tưởng gì, hoặc chưa có | Form xin tham gia: thêm ý tưởng 1, 2, 3 hoặc tích "Chưa có ý tưởng" | S09 | Tỷ lệ gửi thành công không giảm so với form cũ |
| P2 | (bổ sung) Hồ sơ nhanh, không lộ liên lạc | Onboarding một màn: 5 thanh kéo 1–5 + mô tả; không có ô SĐT | A05, S03 | Hoàn tất ≤ 1 phút |
| P1, P2 | (bổ sung) Không bị dẫn vào chức năng đã hết hạn | Khi đợt lập nhóm đóng, tab Khám phá đổi thành tab Dự án; link cũ hiện hộp nhắc | Tab bar, S05 | Không người thử nào vào được màn Khám phá sau hạn |
| P3 | (bổ sung) Tìm đề tài hợp chuyên môn, không tranh với đồng nghiệp | Danh sách đề tài gợi ý; nút "Quan tâm" không giữ chỗ; hiện số giảng viên khác cùng quan tâm; nhóm xếp nguyện vọng, hệ thống ghép "hai bên cùng chọn" trước | L07, S23 | Người thử giải thích đúng điều gì xảy ra khi nhóm xếp người khác lên trước |
| P1, P3, P4 | (bổ sung 20/10) Hiểu vì sao được ghép với người này | Mỗi kết quả ghép có nhãn lý do ("Hai bên cùng chọn", "Nguyện vọng 2", "Theo chuyên môn", "Bộ môn phân công") | S25, L04, W19 | 5/5 người thử đọc đúng lý do |
| P3 | (bổ sung 20/10) Xếp lịch họp 15 phút cho ~40 nhóm EXE201 mà không tạo từng cuộc họp | Đăng khung giờ → hệ thống cắt slot → nhóm tự chọn → sinh họp hằng tuần | C09, C07 | Giảng viên đăng lịch trong ≤ 3 phút; 0 slot trùng |
| P3 | (bổ sung 20/10) Biết nhóm nào chưa trình bày checkpoint ngay trong giờ dạy | L04 lọc "Chưa trình bày CP n"; bottom sheet đánh dấu 3 chạm, không nhập điểm | L04, L05 | Đánh dấu một nhóm trong ≤ 15 giây |
| P3, P5 | (bổ sung) Biết nhóm nào đang chờ mình | Trang chủ có "Cần trả lời", "Trễ hạn nộp", "Họp sắp tới" gom từ mọi nhóm | L01, M01 | Chỉ ra đúng trong ≤ 10 giây |
| P1, P3, P5 | (bổ sung) Chốt lịch họp nhanh | Đề xuất 1–3 khung giờ, mọi người chọn Có/Không, người tổ chức chốt | C07, C08 | ≤ 2 lượt tương tác của người tổ chức |
| P5 | (bổ sung) Vào cuộc nhanh khi được gán giữa kỳ | C01 có thẻ "Tóm tắt cho mentor": đề tài và lịch sử đề tài, tiến độ việc, 3 file mới nhất, biên bản gần nhất, "Buổi mentor n/3" | C01, M01 | Mentor mô tả đúng tình trạng nhóm sau ≤ 2 phút |
| P1, P3 | (bổ sung) Đề tài EXE101 không bị đứt khi sang EXE201 | Đề tài là trục xuyên kỳ: nhóm EXE201 thấy "Tiếp tục từ EXE101", lịch sử đề tài và kho file EXE101 chỉ đọc; giảng viên kỳ trước được gợi ý trước | S15, C06, L07, W29 | Người thử tìm được file EXE101 trong ≤ 3 chạm |
| P1, P3, P5 | (bổ sung) Luôn mở đúng bản file mới nhất | Tải file trùng tên tạo phiên bản mới; danh sách chỉ hiện bản mới nhất, lịch sử ở chi tiết | C06 | Mở bản mới nhất trong ≤ 3 chạm |
