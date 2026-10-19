# Quy trình hệ thống EXE Team Matching – từng luồng

> Viết ngày 20/10/2026 theo nghiệp vụ đã chốt. Nguồn chuẩn khi có mâu thuẫn: `docs/ux/PROJECT_CONTEXT.md` (quy tắc 1–27). Mã màn hình theo `docs/ux/user-flow.md`.
> Mỗi luồng ghi: **ai làm**, **khi nào**, **các bước** (người dùng làm gì → hệ thống làm gì), **chặn / ngoại lệ**, **kết quả**.

---

## 0. Bức tranh chung

**Bốn vai trò:**
- **Sinh viên.** Một người trong nhóm là trưởng nhóm.
- **Giảng viên hướng dẫn** (instructor).
- **Mentor:** người ngoài trường, hoặc giảng viên trong trường dùng chung một tài khoản.
- **Bộ môn** (Admin).

Giảng viên lớp trên FAP không có trong hệ thống.

**Mỗi nhóm có:**
- 4–6 thành viên, có một trưởng nhóm, thuộc ít nhất 2 chuyên ngành hẹp.
- Một giảng viên hướng dẫn và một mentor.
- Một đề tài.
- Một workspace gồm tin nhắn, việc, bài nộp, file và lịch họp.

### Dòng thời gian một học kỳ

| Thời điểm | Chuyện gì xảy ra | Luồng |
|---|---|---|
| Trước kỳ | Bộ môn tạo kỳ, import danh sách, khai báo giảng viên và mentor | 1 |
| Mở đợt lập nhóm | Sinh viên đăng nhập, làm hồ sơ, lập nhóm hoặc vào nhóm. Giảng viên bấm *Quan tâm*, nhóm xếp nguyện vọng | 2, 3, 4, 5 |
| Hạn lập nhóm | Hệ thống khóa đợt, chốt nhóm, ghép sinh viên còn lại, **ghép giảng viên** | 6 |
| Tuần 1–2 | Workspace mở. EXE201: giảng viên đăng lịch họp, nhóm chọn slot | 7, 9 |
| Suốt kỳ | EXE101: trình bày Checkpoint 1–4 trên lớp. Cả hai môn: nhắn tin, việc, bài nộp, file | 7, 8 |
| Tuần 4–5 | Mentor xem đề tài và bấm *Quan tâm* | 10 |
| Tuần 6 | Hệ thống **ghép mentor**; mentor họp 2–3 buổi với mỗi nhóm | 10 |
| Kỳ sau (EXE201) | Nhóm EXE101 lên EXE201 cùng đề tài; đổi thành viên hoặc đề tài qua yêu cầu đổi nhóm | 11, 4 |

Luồng 12 (thông báo và nhắc lịch) chạy xuyên suốt cả kỳ.

---

## 1. Chuẩn bị học kỳ (Bộ môn, web)

**Khi nào:** trước khi mở đợt lập nhóm.

1. **Tạo học kỳ và cấu hình** (W03, W05):
   - Môn EXE101 / EXE201.
   - Sĩ số 4–6, số chuyên ngành tối thiểu, hạn lời mời 72 giờ.
   - Số nguyện vọng giảng viên (3) và giới hạn quan tâm.
   - Tuần bắt đầu mentor (6), số buổi mentor (3).
   - Hạn chọn slot họp (cuối tuần 2), thời lượng slot (15 phút).
   - Tuần mục tiêu của Checkpoint 1–4.
2. **Import sinh viên** từ Excel (W06–W08):
   - Hệ thống kiểm tra MSSV (2 chữ + 6 số), email, chuyên ngành; báo lỗi từng dòng trước khi lưu.
   - Lưu xong thì phát `StudentImported`; Identity tạo sẵn tài khoản ở trạng thái *chờ đăng nhập*.
3. **Khai báo giảng viên** (W11):
   - Nhập email, chuyên môn (domain) và **số nhóm tối đa theo môn**. Có thể để trống = không giới hạn.
   - Hệ thống phát `LecturerRegistered` để giảng viên đăng nhập được.
4. **Tạo mentor** (W27):
   - Nhập họ tên, tổ chức, email, chuyên môn, trong trường hay ngoài trường, **số nhóm tối đa**.
   - Hệ thống phát `MentorRegistered` và gửi email mời đăng nhập.
   - Nếu mentor là giảng viên trong trường thì tài khoản cũ có thêm vai trò Mentor.
5. **Chỉ với EXE201 – import mốc nhóm EXE101** (W06):
   - Hệ thống tạo nhóm EXE201 "kế thừa" với thành viên như mốc và gắn vào **đề tài** cũ theo mã nhóm.
   - Dòng nào không khớp thì nằm ở W29 để Bộ môn gắn tay.
6. **Mở đợt lập nhóm** (W04), đặt hạn chót. Môn chuyển sang giai đoạn *Lập nhóm*.

**Chặn:**
- Email không có trong danh sách thì không đăng nhập được (quy tắc 5).
- Chuyên ngành và domain chỉ chọn từ danh mục, không gõ tự do.

---

## 2. Đăng nhập và hồ sơ lần đầu (mọi vai trò)

1. Người dùng bấm *Đăng nhập Google* (A03).
2. Identity kiểm tra email trong danh sách tài khoản tạo sẵn:
   - Không có → màn hình A04 "Tài khoản chưa có trong danh sách", có nút đăng nhập bằng tài khoản khác.
   - Có → hệ thống cấp JWT, trong đó claim `roles` là danh sách vai trò, rồi vào app theo vai trò.
3. **Sinh viên lần đầu – A05, chỉ một màn hình:**
   - Xem thông tin đã import (chỉ đọc).
   - Kéo **5 thanh kỹ năng 1–5**: Công nghệ, UI/UX, Quản trị, Marketing, Plan truyền thông. Thanh nào chưa chạm thì chưa lưu được.
   - Viết mô tả (≤ 500 ký tự).
   - **Không có ô số điện thoại hay Zalo.** Số điện thoại lấy từ file import và chỉ người trong nhóm thấy.
4. Giảng viên và mentor vào thẳng trang chủ: giảng viên L01, mentor M01.

**Kết quả:** sinh viên có hồ sơ để trưởng nhóm đánh giá và để hệ thống gợi ý ghép.

---

## 3. Lập nhóm (sinh viên, giai đoạn lập nhóm)

**Khi nào:** khi đợt lập nhóm của môn đang mở. Ngoài đợt, tab *Khám phá* biến mất (quy tắc 18).

**3a. Tạo nhóm** (S13, S14):
1. Sinh viên chưa có nhóm bấm *Tạo nhóm*, chọn domain và nhập ý tưởng.
2. Người tạo thành trưởng nhóm; nhóm ở trạng thái *Nháp* (dưới 4 người).
3. Với EXE101, lúc này tạo luôn **đề tài** (Project) của nhóm.

**3b. Tìm và mời thành viên** (S18, S19, S21):
1. Hệ thống (Matching) gợi ý sinh viên bù chuyên ngành còn thiếu và **kỹ năng yếu nhất** của nhóm.
2. Trưởng nhóm có thể tìm theo MSSV, hoặc đăng bài tuyển thành viên.
3. Trưởng nhóm gửi lời mời → sinh viên nhận thông báo → chấp nhận hoặc từ chối trong **72 giờ**. Quá hạn thì lời mời tự hết hiệu lực.

**3c. Xin vào nhóm** (S05–S09):
1. Sinh viên lọc nhóm còn chỗ, mở chi tiết nhóm, bấm *Xin tham gia*.
2. Bắt buộc chọn một trong hai:
   - **1–3 ý tưởng** (tiêu đề + mô tả);
   - **tích "Chưa có ý tưởng"**.
3. Trưởng nhóm xem yêu cầu ở S20: 5 kỹ năng của người xin, có đánh dấu chỗ nhóm đang thiếu, kèm các ý tưởng → Nhận hoặc Từ chối.

**3d. Sinh viên tự đăng "Tôi đang tìm nhóm"** (S10, S11) để nhóm khác tìm thấy mình.

**Chặn:**
- Mỗi sinh viên chỉ thuộc một nhóm mỗi môn.
- Nhóm tối đa 6 người.
- Khi đã vào một nhóm, sinh viên không chấp nhận thêm lời mời nào khác được (S12 có dialog nhắc).

**Kết quả:** nhóm đủ 4–6 người chuyển sang trạng thái *Hoạt động*.

---

## 4. Nhóm kế thừa EXE201 và yêu cầu đổi nhóm

**Khi nào:** chỉ EXE201, trong đợt đổi nhóm.

1. Nhóm kế thừa bắt đầu với thành viên đúng như mốc EXE101.
2. Mọi thay đổi chỉ là **thay đổi chờ duyệt**, chưa có hiệu lực ngay:
   - mời người mới, nhận người xin vào;
   - xóa thành viên, thành viên tự rời;
   - **đổi đề tài** (pivot hoặc ý tưởng mới).
3. S15 và S17 tách rõ "thành viên hiện tại" và "thay đổi đang chờ".
4. Trưởng nhóm tạo **yêu cầu đổi nhóm** (S27, S28):
   - Hệ thống tính số người và % thay đổi.
   - Bắt buộc nhập lý do.
   - Nhóm còn dưới 4 người thì bị chặn; thiếu chuyên ngành thứ hai chỉ bị cảnh báo.
5. Bộ môn duyệt ở W17, W18:
   - **Duyệt** → thay đổi có hiệu lực, ghi nhật ký; nếu có đổi đề tài thì ghi một phiên bản đề tài mới.
   - **Từ chối** (kèm lý do) → nhóm giữ nguyên như mốc; trưởng nhóm có thể tạo yêu cầu mới.
6. Trưởng nhóm theo dõi ở S29 bằng dòng thời gian: Đã gửi → Chờ Bộ môn duyệt → Kết quả.

Sinh viên EXE201 không có nhóm EXE101 thì lập nhóm tự do như luồng 3.

---

## 5. Quan tâm của giảng viên và nguyện vọng của nhóm

**Khi nào:** trong đợt lập nhóm, cho cả EXE101 và EXE201.

**Phía giảng viên** (L07 – tab *Đề tài*):
1. Tab **Gợi ý**: các nhóm có domain trùng chuyên môn của giảng viên và chưa có giảng viên hướng dẫn.
2. Giảng viên bấm **Quan tâm** những nhóm muốn hướng dẫn:
   - Không giữ chỗ.
   - Rút lại được (có Hoàn tác).
   - Có giới hạn: số nhóm còn nhận + 2, hoặc 10 nếu không đặt tối đa.
3. Giảng viên chỉ thấy **có bao nhiêu** người khác cũng quan tâm nhóm đó, không thấy là ai.
4. Tab **Nhóm chọn tôi**: nhóm nào đã xếp mình làm nguyện vọng, và ở nguyện vọng mấy.
5. **Không có bước chấp nhận hay từ chối.**

**Phía nhóm** (S23–S25, chỉ trưởng nhóm sửa):
1. Đầu danh sách giảng viên có hai mục:
   - **"Quan tâm nhóm bạn"**;
   - với EXE201: **"Đã hướng dẫn ở EXE101"** (giảng viên cũ của đề tài).
2. Trưởng nhóm xếp **nguyện vọng 1, 2, 3**, đổi thứ tự được tới khi nhóm được ghép.
3. Không bắt buộc đủ 3 nguyện vọng, cũng không bắt buộc có nguyện vọng nào.

**Kết quả:** trước hạn, hệ thống có đủ hai tín hiệu: giảng viên muốn nhóm nào, và nhóm muốn giảng viên nào.

---

## 6. Đóng đợt lập nhóm và ghép giảng viên hướng dẫn

**Khi nào:** đúng hạn chót do Bộ môn đặt. Các bước dưới đây do Job Service tự chạy theo thứ tự.

1. **Khóa đợt.** Mọi thao tác lập nhóm bị chặn: API trả `409 round-closed`, tab *Khám phá* đổi thành *Dự án*.
2. **Chốt nhóm:**
   - Nhóm đủ 4–6 người và ≥ 2 chuyên ngành → *Đã chốt*.
   - Nhóm không đủ điều kiện → *Cần xem lại*.
3. **Ghép sinh viên còn lại:**
   - Trước hết đưa sinh viên chưa có nhóm vào bù cho các nhóm *Cần xem lại*.
   - Số còn lại được chia thành nhóm mới hợp lệ.
   - Nhóm nào vẫn chưa hợp lệ thì Bộ môn sửa tay (W20–W22).
4. **Ghép giảng viên hướng dẫn** cho từng nhóm chưa có. Hệ thống xét từ bậc 1 xuống, gặp giảng viên còn chỗ đầu tiên thì ghép:

   | Bậc | Ứng viên | Nhãn lý do |
   |---|---|---|
   | 1 | Giảng viên **vừa nằm trong nguyện vọng vừa quan tâm** nhóm (nguyện vọng cao trước) | Hai bên cùng chọn |
   | 2 | Nguyện vọng 1 → 2 → 3 | Nguyện vọng n |
   | 3 | Giảng viên quan tâm nhóm (ai ít nhóm hơn trước) | Giảng viên quan tâm |
   | 4 | EXE201: giảng viên EXE101 của cùng đề tài; còn lại: giảng viên trùng chuyên môn nhiều nhất, ít nhóm nhất | Theo chuyên môn |

   - **Giảng viên đã đủ số nhóm tối đa** thì nhóm chuyển xuống ứng viên tiếp theo.
   - **Tranh chấp trong cùng một bậc** (nhiều nhóm cùng muốn một giảng viên sắp đủ chỗ): giảng viên giữ nhóm xếp mình cao hơn, rồi tới nhóm trùng chuyên môn nhiều hơn.
5. **Áp dụng kết quả trong một lần:**
   - Mỗi nhóm có giảng viên kèm nhãn lý do.
   - Nguyện vọng bị khóa.
   - Các lượt quan tâm khác của nhóm tự đóng; giảng viên đó được báo "nhóm đã có giảng viên".
   - Giảng viên được thêm vào workspace của nhóm.
6. **Bộ môn soát ở W19:**
   - Bảng gồm nhóm · giảng viên · lý do · nguyện vọng · quan tâm, kèm thanh tải của từng giảng viên.
   - Lọc "Chưa có giảng viên" để gán tay những nhóm mà mọi giảng viên đều đã đầy.
   - Có thể **đổi giảng viên của bất kỳ nhóm nào**; bắt buộc nhập lý do; nhãn lý do thành "Bộ môn phân công".

**Ghép sớm:**
- Trước hạn, Bộ môn có thể bấm **"Ghép sớm"** ở W19 → xem trước kết quả → chốt.
- Chỉ những nhóm đã có lúc đó được ghép; nhóm lập sau được ghép lúc hết hạn.

**Kết quả:**
- Sinh viên thấy giảng viên của nhóm và lý do ở S25, S15.
- Giảng viên thấy danh sách nhóm của mình ở L04.
- Môn chuyển sang giai đoạn *Học kỳ*.

---

## 7. Làm việc trong workspace (suốt học kỳ)

**Ai:** thành viên nhóm, giảng viên hướng dẫn, mentor (từ tuần 6); Bộ môn chỉ xem.

- **Tin nhắn** (C02, C03):
  - Mỗi nhóm có 2 kênh: **Nhóm** (chỉ thành viên) và **Cố vấn** (thành viên + giảng viên + mentor).
  - Thành viên đánh dấu một tin là **câu hỏi cho cố vấn**. Câu hỏi nằm trong mục "Cần trả lời" của giảng viên / mentor (L01, M01) cho tới khi có người trả lời trong luồng.
  - Tin thường không tạo thông báo.
- **Việc** (C04, C05):
  - Thành viên tạo việc, giao người, đặt hạn.
  - Trạng thái: Cần làm → Đang làm → Xong.
- **Bài nộp** (C04, C05):
  - Chỉ giảng viên hướng dẫn tạo, có hạn.
  - Nhóm nộp một phiên bản file.
  - Giảng viên chọn **Đạt** hoặc **Cần sửa** (bắt buộc bình luận).
  - Nộp trễ vẫn được nhưng giữ nhãn *Trễ hạn*.
- **File** (C06):
  - Thư mục theo nhóm; mỗi file ≤ 25 MB; định dạng pdf, docx, pptx, xlsx, png, jpg, zip.
  - Tải lên trùng tên tạo **phiên bản mới**; danh sách chỉ hiện bản mới nhất.
  - Giảng viên và mentor chỉ xem và bình luận.
- **Họp đề xuất** (C07, C08) – dùng cho buổi mentor và họp phát sinh:
  - Người tổ chức đưa ra 1–3 khung giờ.
  - Mọi người chọn Có / Không cho từng khung → người tổ chức chốt → hệ thống nhắc trước 24 giờ và 1 giờ.
  - Ghi biên bản; chọn dòng trong biên bản → **tạo việc** ngay.

---

## 8. EXE101 – Checkpoint trên lớp

**Ai:** giảng viên hướng dẫn và nhóm EXE101. **Khi nào:** giờ dạy của giảng viên (T2–T6), suốt kỳ.

1. Nhóm lên trình bày Checkpoint n bất kỳ buổi nào giảng viên dạy. **Không cần đặt lịch.**
2. Giảng viên mở L04, lọc **"Chưa trình bày CP n"** → mở nhóm (L05) → bấm *Đánh dấu Checkpoint*.
3. Trong bottom sheet: chọn CP, ngày (mặc định hôm nay), nhận xét ngắn (không bắt buộc) → Lưu.
4. Hệ thống ghi nhận và báo cho nhóm. Nhóm thấy **"Checkpoint n/4"** và nhận xét ở S15, C01.

**Quy tắc:**
- **Không lưu điểm**, vì điểm nằm trên FAP.
- Đánh dấu nhầm thì *Hoàn tác* ngay, hoặc bỏ đánh dấu trong ngày.
- Quá tuần mục tiêu mà nhóm chưa trình bày thì nhóm hiện trong danh sách nhắc của giảng viên.

---

## 9. EXE201 – Lịch họp cố định hằng tuần

**Ai:** giảng viên hướng dẫn và trưởng nhóm EXE201. **Khi nào:** tuần 1–2 sau khi ghép giảng viên.

1. Giảng viên mở C09 (từ thẻ "Đăng lịch họp hằng tuần" ở L01) và thêm **khung giờ**, ví dụ Thứ Ba 09:00–11:00, Thứ Năm 14:00–16:00.
2. Hệ thống cắt khung thành **slot 15 phút** và báo số slot so với số nhóm, ví dụ "32 slot · 40 nhóm".
   - Thiếu slot thì có cảnh báo, nhưng giảng viên vẫn đăng được.
3. Giảng viên dán link họp online → **Đăng lịch**. Các nhóm nhận thông báo kèm hạn chọn.
4. Trưởng nhóm mở C09, chọn **một slot trống**:
   - Ai chọn trước được trước; một slot chỉ một nhóm.
   - Đổi được tới hạn.
   - Hai nhóm bấm cùng lúc thì nhóm sau thấy "Slot vừa có nhóm khác chọn" và lưới tự cập nhật.
5. **Tới hạn chọn slot** (mặc định cuối tuần 2):
   - Nhóm chưa chọn được xếp vào slot trống.
   - Không đủ slot thì giảng viên được nhắc thêm khung giờ.
6. Hệ thống **tự tạo họp mỗi tuần** cho từng nhóm tới tuần cuối, nhắc trước 24 giờ và 1 giờ, hiện ở C07 với nhãn "Hằng tuần".
7. Trong kỳ:
   - Giảng viên có thể đổi slot cho một nhóm, hoặc hủy / dời một tuần (nghỉ lễ, thi) kèm lý do.
   - Nhóm được báo.
   - Biên bản họp vẫn ghi được như luồng 7.

---

## 10. Mentor: quan tâm → ghép tuần 6 → buổi mentor

**Ai:** mentor, Bộ môn, nhóm. **Sinh viên không chọn mentor.**

1. **Tuần 4–5:** mentor đăng nhập.
   - M01 hiện "Đang quan tâm n đề tài · Ghép vào tuần 6".
   - Mentor vào tab *Đề tài* (L07): xem gợi ý theo chuyên môn và bấm **Quan tâm**. Giới hạn = số nhóm tối đa + 2.
   - Mentor đặt **số nhóm mong muốn** (không vượt tối đa do Bộ môn đặt).
2. **Tuần bắt đầu mentor** (mặc định tuần 6), hệ thống ghép mentor cho mỗi nhóm chưa có:
   - Thứ tự ứng viên: mentor quan tâm nhóm → mentor trùng chuyên môn.
   - Trong mỗi bậc, ưu tiên mentor đang ít nhóm so với số mong muốn.
   - Mỗi mentor được lấp **tới số mong muốn trước**; chỉ vượt lên (tới tối đa) khi còn nhóm chưa có mentor.
   - Mỗi nhóm đúng **một mentor**, kèm nhãn lý do.
3. **Bộ môn soát ở W28:**
   - Có thể "Ghép sớm" trước tuần 6.
   - Đổi mentor tay: cảnh báo khi vượt số mong muốn, chặn khi vượt tối đa.
   - Mentor cũ mất quyền vào workspace ngay; lịch sử vẫn giữ.
4. **Làm việc với nhóm:**
   - Mentor mở C01, xem thẻ **"Tóm tắt cho mentor"**: đề tài và lịch sử đề tài, tiến độ việc, 3 file mới nhất, biên bản gần nhất.
   - Mentor đề xuất họp 1–3 khung giờ (luồng 7) → nhóm chọn → chốt → họp → biên bản → tạo việc.
   - Mỗi buổi do mentor tổ chức được đếm: **"Buổi mentor 1/3"**.
5. Mentor chỉ xem và góp ý: không tạo bài nộp, không duyệt yêu cầu, không đổi thành viên.

---

## 11. Đề tài xuyên kỳ: từ EXE101 lên EXE201

1. Đề tài (Project) được tạo khi nhóm EXE101 đăng ký ý tưởng. Mỗi thay đổi đề tài được ghi thành một phiên bản: đổi gì, vì sao, ai đổi, khi nào.
2. Kỳ sau, Bộ môn import mốc EXE101 (luồng 1, bước 5) → nhóm EXE201 gắn vào đề tài cũ.
3. Nhóm EXE201 thấy:
   - dòng **"Tiếp tục từ EXE101 · SP26"** và dòng thời gian của đề tài;
   - workspace có thư mục **"EXE101 (kỳ trước)" chỉ đọc**: file, việc đã xong, biên bản cũ.
4. Khi xếp nguyện vọng, giảng viên EXE101 của đề tài hiện đầu danh sách. Lúc ghép, người này là ứng viên ở bậc 4 (luồng 6).
5. Muốn đổi đề tài thì đi qua yêu cầu đổi nhóm (luồng 4). Sửa lỗi chính tả trong mô tả thì không cần duyệt.
6. Bộ môn xem toàn cảnh ở W29: chuỗi nhóm EXE101 → EXE201, % thành viên giữ lại, lịch sử đề tài, giảng viên và mentor của từng kỳ.

---

## 12. Thông báo và nhắc lịch (xuyên suốt)

| Sự kiện | Ai nhận | Kênh |
|---|---|---|
| Lời mời, yêu cầu tham gia, được nhận / từ chối, hết hạn | Sinh viên, trưởng nhóm | App + email |
| Hạn lập nhóm còn 72 giờ / 24 giờ | Sinh viên chưa có nhóm, nhóm dưới 4 người | App + email |
| Yêu cầu đổi nhóm được duyệt / từ chối | Thành viên, giảng viên | App + email |
| Được ghép giảng viên / mentor (kèm lý do); quan tâm đã đóng | Nhóm, giảng viên, mentor | App + email |
| Ghép xong, còn nhóm chưa có cố vấn | Bộ môn | App |
| Lịch họp hằng tuần đã đăng; slot bị đổi; họp hủy / dời | Nhóm | App |
| Nhắc họp trước 24 giờ và 1 giờ | Người tham dự | App + email |
| Câu hỏi cho cố vấn, được nhắc tên (@) | Cố vấn / người được nhắc | App |
| Việc được giao, bài nộp mới / được duyệt / trễ hạn | Thành viên, giảng viên | App |
| Checkpoint đã ghi nhận | Nhóm | App |

Tin nhắn thường **không** tạo thông báo, để tránh quá tải.

---

## Phụ lục – Hệ thống làm gì phía sau (cho người code)

| Luồng | Service chính | Sự kiện Kafka | Job |
|---|---|---|---|
| 1 | Academic, Identity, Team (import mốc) | StudentImported, LecturerRegistered, MentorRegistered, TeamLinkedToProject | – |
| 3, 4 | Team, Matching (gRPC gợi ý) | InvitationSent…, MemberJoined, ChangeRequestApproved | InvitationExpiryJob, DeadlineReminderJob |
| 5 | Team (TopicInterest, LecturerPreference), Matching (SuggestTopicsForLecturer) | TopicInterestOpened / Closed, LecturerPreferencesUpdated | – |
| 6 | Team, Matching (ValidateComposition, PlanAutoAssignment, PlanAdvisorMatching) | RoundClosed, TeamFinalized, LecturerAssigned, AdvisorMatchingCompleted | RoundAutoCloseJob → AutoAssignUnmatchedJob → LecturerMatchingJob |
| 7 | Collaboration (SignalR hub, MinIO) | exe.collab | MeetingReminderJob, TaskDueReminderJob |
| 8 | Collaboration – Checkpoints | CheckpointReviewed | – |
| 9 | Collaboration – Meetings | MeetingSlotsPublished, MeetingSlotBooked | SlotBookingCloseJob, MeetingReminderJob |
| 10 | Team, Matching (PlanAdvisorMatching) | MentorAssigned / Unassigned | MentorMatchingJob |
| 11 | Team (Projects), Collaboration (workspace kỳ trước) | ProjectCreated, ProjectChanged, TeamLinkedToProject | – |
| 12 | Notification | (tiêu thụ mọi topic trên) | – |

Người phụ trách từng phần: `AGENTS.md` mục 2. Task: `docs/EXE_Team_Matching_Phan_chia_task.xlsx`.
