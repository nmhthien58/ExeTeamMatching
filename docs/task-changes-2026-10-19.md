# Thay đổi task ngày 19/10/2026 – theo góp ý của giảng viên

> File `EXE_Team_Matching_Phan_chia_task.xlsx` đã được cập nhật theo danh sách này. **Google Sheet bản chính chưa đổi** – người giữ sheet dán các dòng dưới đây vào sheet tương ứng (cột theo thứ tự của sheet: Mã · Giai đoạn · Module · Task · Mô tả · Tham chiếu · Ưu tiên · Thành viên · Phụ thuộc · Chờ người khác · Trạng thái · Ghi chú).
> Lý do và quyết định: `docs/ux/PROJECT_CONTEXT.md` mục 13, `docs/ux/design-decisions.md` Phần E. Figma không vẽ lại.

## Số task mỗi người sau thay đổi

| Thành viên | Tổng task |
|---|---|
| Thiên | 34 |
| Nam | 34 |
| Cường | 32 |
| Nguyên | 33 |
| Thành | 33 |

Collaboration Service chia theo module như Team Service: **Thiên** dựng service và hạ tầng, **Cường** module Messages, **Nguyên** module Files và Tasks, **Nam** module Meetings. `CollabDbContext` và DB script của `collab_db` chỉ Thiên sửa.

## Task mới (35)

| Mã | Sheet | Giai đoạn | Module | Task | Ưu tiên | Thành viên | Phụ thuộc |
|---|---|---|---|---|---|---|---|
| BE-60 | Backend | GĐ1 – Thiết kế | Database | Cập nhật thiết kế DB và DB script (bản 4) | P0 | Thiên | BE-01 |
| BE-61 | Backend | GĐ1 – Thiết kế | Thiết kế | Cập nhật contract API, proto, sự kiện (bản 4) | P0 | Thiên | BE-02, BE-60 |
| BE-62 | Backend | GĐ2 – Nền tảng | Collaboration | Dựng Collaboration Service | P0 | Thiên | BE-04, BE-07, BE-60, BE-61 |
| BE-63 | Backend | GĐ2 – Nền tảng | DevOps | SignalR qua Gateway và MinIO | P0 | Thiên | BE-08, BE-09, BE-62 |
| BE-64 | Backend | GĐ3 – Nghiệp vụ lõi | Identity | Vai trò Mentor | P0 | Thiên | BE-13, BE-66 |
| BE-65 | Backend | GĐ4 – Tích hợp | Collaboration – Files | File dự án | P0 | Nguyên | BE-62, BE-63 |
| BE-66 | Backend | GĐ3 – Nghiệp vụ lõi | Academic | Quản lý mentor và chuyên môn giảng viên | P0 | Nam | BE-20, BE-60 |
| BE-67 | Backend | GĐ3 – Nghiệp vụ lõi | Academic | Giai đoạn môn học và hồ sơ kỹ năng | P0 | Nam | BE-19, BE-60 |
| BE-68 | Backend | GĐ4 – Tích hợp | Collaboration – Meetings | Lịch họp | P1 | Nam | BE-62, BE-70 |
| BE-69 | Backend | GĐ4 – Tích hợp | Collaboration – Messages | Tin nhắn nhóm và cố vấn | P0 | Cường | BE-62, BE-63 |
| BE-70 | Backend | GĐ4 – Tích hợp | Collaboration – Tasks | Việc và bài nộp | P0 | Nguyên | BE-62, BE-65 |
| BE-71 | Backend | GĐ4 – Tích hợp | Matching | Gợi ý đề tài cho giảng viên, gợi ý theo kỹ năng | P0 | Nguyên | BE-35, BE-61, BE-66 |
| BE-72 | Backend | GĐ5 – Tự động hóa | Job Service | Nhắc họp và hạn việc | P1 | Nguyên | BE-51, BE-68, BE-70 |
| BE-73 | Backend | GĐ5 – Tự động hóa | Team | Quan tâm đề tài của giảng viên | P0 | Thành | BE-46, BE-71 |
| BE-74 | Backend | GĐ5 – Tự động hóa | Team | Gán mentor cho nhóm | P0 | Thành | BE-27, BE-66 |
| BE-75 | Backend | GĐ5 – Tự động hóa | Notification | Thông báo cho workspace và nhắc lịch | P0 | Thành | BE-40, BE-61 |
| FE-32 | Frontend Web | GĐ2 – Nền tảng | Nền tảng | Theme bản 4 và khung theo vai trò | P0 | Thiên | FE-02, FE-05, BE-67 |
| FE-33 | Frontend Web | GĐ4 – Tích hợp | Nền tảng | Client realtime dùng chung (web) | P0 | Cường | FE-03, BE-63 |
| FE-34 | Frontend Web | GĐ3 – Nghiệp vụ lõi | Dữ liệu | W27 – Mentor | P0 | Nam | FE-07, BE-66 |
| FE-35 | Frontend Web | GĐ3 – Nghiệp vụ lõi | Hồ sơ | Web hồ sơ và onboarding | P0 | Nam | FE-32, BE-67 |
| FE-36 | Frontend Web | GĐ4 – Tích hợp | Workspace | Web lịch họp C07, C08 | P1 | Nam | FE-32, BE-68 |
| FE-37 | Frontend Web | GĐ3 – Nghiệp vụ lõi | Sinh viên | Web sinh viên – trang chủ, khám phá, nhóm | P0 | Cường | FE-32, BE-27, BE-29 |
| FE-38 | Frontend Web | GĐ4 – Tích hợp | Workspace | Web tin nhắn C02, C03 | P0 | Cường | FE-33, BE-69 |
| FE-39 | Frontend Web | GĐ4 – Tích hợp | Workspace | Web dự án C01, bảng việc C04, C05 | P0 | Nguyên | FE-32, BE-70 |
| FE-40 | Frontend Web | GĐ4 – Tích hợp | Workspace | Web file C06 và gợi ý (S07, S18, S19, S30, S31) | P0 | Nguyên | FE-32, BE-65, BE-39 |
| FE-41 | Frontend Web | GĐ5 – Tự động hóa | Giảng viên | Web giảng viên và mentor | P0 | Thành | FE-32, BE-73, BE-74 |
| FE-42 | Frontend Web | GĐ5 – Tự động hóa | Sinh viên | Web sinh viên – giảng viên, đổi nhóm, thông báo | P0 | Thành | FE-32, BE-45, BE-73 |
| FE-43 | Frontend Web | GĐ5 – Tự động hóa | Giảng viên | W28 – Gán mentor; W19 hiện quan tâm | P1 | Thành | FE-07, BE-73, BE-74 |
| MB-37 | Mobile | GĐ2 – Nền tảng | Nền tảng | Theme bản 4, shell theo giai đoạn và vai trò Mentor | P0 | Thiên | MB-02, MB-05, BE-67 |
| MB-38 | Mobile | GĐ4 – Tích hợp | Nền tảng | Client realtime và tải file (mobile) | P0 | Cường | MB-04, BE-63 |
| MB-39 | Mobile | GĐ4 – Tích hợp | Workspace | C02, C03 Tin nhắn | P0 | Cường | MB-38, BE-69 |
| MB-40 | Mobile | GĐ4 – Tích hợp | Workspace | C01 Dự án, C04 Bảng việc, C05 Chi tiết việc / bài nộp | P0 | Nguyên | MB-37, BE-70 |
| MB-41 | Mobile | GĐ4 – Tích hợp | Workspace | C06 File dự án | P0 | Nguyên | MB-38, BE-65 |
| MB-42 | Mobile | GĐ4 – Tích hợp | Workspace | C07, C08 Lịch họp | P1 | Nam | MB-37, BE-68 |
| MB-43 | Mobile | GĐ5 – Tự động hóa | Giảng viên | L07 Đề tài phù hợp; giảng viên quan tâm ở S23, S25 | P0 | Thành | MB-27, BE-73 |
| MB-44 | Mobile | GĐ5 – Tự động hóa | Mentor | M01 Trang chủ mentor; bổ sung L01, L04, L05 | P0 | Thành | MB-32, MB-34, BE-74, BE-69, BE-70 |

Mô tả / tiêu chí hoàn thành đầy đủ của từng task nằm ở cột "Mô tả" trong file xlsx.

## Task cũ cần sửa mô tả (37)

Thêm vào cuối cột "Mô tả / tiêu chí hoàn thành" câu bắt đầu bằng "Bản 4:", và ghi "Sửa 19/10/2026 theo góp ý của giảng viên" ở cột Ghi chú.

| Mã | Thêm vào mô tả |
|---|---|
| BE-03 | Bản 4: Vẽ lại C1 cho bản 4 (Collaboration Service, MinIO, web cho mọi vai trò, WebSocket) và render lại c1.png. |
| BE-08 | Bản 4: Thêm collab-api :5007, minio :9000 / :9001, collab_db (BE-63). |
| BE-09 | Bản 4: Thêm route /api/workspaces/* và hub /hubs/collab (WebSocket) – BE-63. |
| BE-13 | Bản 4: Thêm MentorRegistered → role Mentor (BE-64). |
| BE-14 | Bản 4: Thêm role Mentor và policy cố vấn của workspace. |
| BE-17 | Bản 4: File import có cột SĐT tùy chọn (chuỗi) – nguồn liên lạc duy nhất (quy tắc 11). |
| BE-19 | Bản 4: Hồ sơ = 5 kỹ năng 1–5 + mô tả, không có SĐT/Zalo (quy tắc 16; BE-67). |
| BE-20 | Bản 4: Thêm domain chuyên môn của giảng viên (BE-66). |
| BE-26 | Bản 4: Ngoài giai đoạn lập nhóm trả 409 round-closed (quy tắc 18). |
| BE-29 | Bản 4: Yêu cầu có 1–3 ý tưởng hoặc cờ "chưa có ý tưởng" (quy tắc 17); trả kèm kỹ năng người xin cho S20; chặn ngoài giai đoạn lập nhóm. |
| BE-31 | Bản 4: Chỉ trong giai đoạn lập nhóm (quy tắc 18). |
| BE-36 | Bản 4: Dùng kỹ năng 1–5 để bù kỹ năng yếu của nhóm (BE-71). |
| BE-46 | Bản 4: Nhóm gửi nguyện vọng trực tiếp từ danh sách giảng viên quan tâm (BE-73). |
| BE-47 | Bản 4: Phân công chính thức đóng các quan tâm còn mở (BE-73); LecturerAssigned dùng cho Collaboration. |
| BE-57 | Bản 4: Health cho collab-api, minio; seed mentor, workspace, tin nhắn, việc, họp mẫu. |
| BE-59 | Bản 4: Thêm luồng demo 2, 5, 6, 7 ở PROJECT_CONTEXT §11. |
| FE-02 | Bản 4: Token theo DESIGN.md bản 4 (FE-32). |
| FE-17 | Bản 4: W11 thêm cột chuyên môn (domain) của giảng viên. |
| FE-22 | Bản 4: W16 thêm mục Mentor của nhóm (API BE-74). |
| FE-25 | Bản 4: W19 hiện giảng viên quan tâm của từng nhóm (FE-43, BE-73). |
| FE-31 | Bản 4: Kiểm thử thêm khung Sinh viên / Giảng viên / Mentor ở 3 mức bề rộng. |
| MB-02 | Bản 4: ColorScheme theo DESIGN.md bản 4 (MB-37). |
| MB-05 | Bản 4: Giảng viên 5 tab, Mentor 4 tab, Khám phá ↔ Dự án theo giai đoạn (MB-37). |
| MB-08 | Bản 4: A05 còn một màn: thông tin import chỉ đọc + 5 SkillSlider + giới thiệu; bỏ SĐT/Zalo (quy tắc 16). |
| MB-12 | Bản 4: S02 hiện SkillBars, S03 sửa bằng SkillSlider; bỏ mục liên lạc. |
| MB-13 | Bản 4: S04 không có SĐT; email chỉ hiện với người trong nhóm. |
| MB-14 | Bản 4: Chỉ có trong giai đoạn lập nhóm (quy tắc 18). |
| MB-16 | Bản 4: S09 thêm IdeaListField: 1–3 ý tưởng hoặc "Chưa có ý tưởng" (quy tắc 17). |
| MB-17 | Bản 4: Chỉ có trong giai đoạn lập nhóm (quy tắc 18). |
| MB-20 | Bản 4: S15 thêm mục Cố vấn và lối tắt Mở dự án. |
| MB-23 | Bản 4: S18 gợi ý theo kỹ năng yếu của nhóm. |
| MB-24 | Bản 4: S20 hiện SkillBars (đánh dấu kỹ năng nhóm thiếu) và ý tưởng của người xin. |
| MB-27 | Bản 4: S23 thêm mục giảng viên quan tâm (MB-43). |
| MB-28 | Bản 4: S25 hiện giảng viên đang quan tâm (MB-43). |
| MB-32 | Bản 4: L01 thêm Cần trả lời, Trễ hạn nộp, Họp sắp tới (MB-44). |
| MB-35 | Bản 4: L06 thêm domain chuyên môn – đầu vào gợi ý đề tài. |
| MB-36 | Bản 4: Kiểm thử thêm C01–C08, L07, M01. |

## Thứ tự nên làm

1. **Tuần này – Thiên:** BE-60 (DB), BE-61 (contract) để mọi người làm song song; sau đó BE-62, BE-63, FE-32, MB-37.
2. **Không cần chờ contract mới:** sửa các task cũ (ý tưởng S09, khóa Khám phá, hồ sơ kỹ năng) vì chỉ đổi nghiệp vụ trong service sẵn có – nhưng vẫn phải đợi BE-60 nếu đụng bảng mới (JoinRequestIdea, StudentSkill).
3. **Sau BE-62, BE-63:** các module Collaboration (BE-65, BE-68, BE-69, BE-70) chạy song song, rồi web và mobile của từng module.
4. **GĐ5:** quan tâm đề tài (BE-73) sau gợi ý đề tài (BE-71); gán mentor (BE-74) sau quản lý mentor (BE-66); nhắc lịch (BE-72) và thông báo (BE-75) sau cùng.

## Bổ sung lần 2 (19/10/2026) – mentor và đề tài xuyên kỳ

Sau khi nhóm chốt: một mentor mỗi nhóm, sức chứa riêng cho từng mentor + số nhóm mong muốn, mentor từ tuần 6 và 2–3 buổi, quản lý theo đề tài từ EXE101 sang EXE201 (quy tắc 26).

### Task mới (4)

| Mã | Sheet | Giai đoạn | Module | Task | Ưu tiên | Thành viên | Phụ thuộc |
|---|---|---|---|---|---|---|---|
| BE-76 | Backend | GĐ5 – Tự động hóa | Team | Đề tài xuyên kỳ (EXE101 → EXE201) | P0 | Thành | BE-43, BE-45, BE-60 |
| BE-77 | Backend | GĐ5 – Tự động hóa | Collaboration – Files | Kho EXE101 trong workspace EXE201 | P1 | Nguyên | BE-65, BE-76 |
| FE-44 | Frontend Web | GĐ5 – Tự động hóa | Nhóm | W29 – Đề tài qua các kỳ | P1 | Thành | FE-07, BE-76 |
| MB-45 | Mobile | GĐ5 – Tự động hóa | SV – Nhóm | S15 thẻ đề tài và lịch sử đề tài | P1 | Cường | MB-20, BE-76 |

### Thêm vào mô tả task đã có

| Mã | Thêm vào mô tả |
|---|---|
| BE-60 | Bổ sung lần 2: team_db thêm Project, ProjectVersion, Team.ProjectId và PreviousTeamId, ChangeRequestItem loại ProjectChange; MentorAssignment UNIQUE theo nhóm (một mentor mỗi nhóm); academic_db: Mentor.max_teams (Bộ môn đặt), Mentor.preferred_teams (mentor chọn), Mentor.is_internal, Semester.mentoring_start_week, Semester.expected_mentor_sessions; collab_db: Workspace.previous_workspace_id. |
| BE-61 | Bổ sung lần 2: API đề tài và lịch sử đề tài, ProjectChange trong change request, sự kiện ProjectCreated / ProjectChanged / TeamLinkedToProject; JWT claim roles là danh sách (Lecturer + Mentor). |
| BE-62 | Bổ sung lần 2: consume TeamLinkedToProject để gắn PreviousWorkspaceId. |
| BE-64 | Bổ sung lần 2: một tài khoản có thể giữ cả Lecturer và Mentor. |
| BE-66 | Bổ sung lần 2: số nhóm tối đa đặt riêng cho từng mentor; mentor tự đặt số nhóm mong muốn (≤ tối đa); cài đặt kỳ: tuần bắt đầu mentor (mặc định 6), số buổi mentor dự kiến (mặc định 3). |
| BE-68 | Bổ sung lần 2: họp do mentor tổ chức tính là buổi mentor; trả "Buổi mentor n/m". |
| BE-74 | Bổ sung lần 2: một mentor mỗi nhóm (gán mới thay mentor cũ); cảnh báo vượt số mong muốn, chặn vượt tối đa; API "nhóm chưa có mentor" cho W28. |
| BE-43 | Bổ sung lần 2: gắn nhóm EXE201 với đề tài của nhóm EXE101 theo mã nhóm; dòng không khớp trả về để gắn tay (BE-76). |
| BE-45 | Bổ sung lần 2: yêu cầu đổi nhóm có thể chứa thay đổi đề tài (ProjectChange); duyệt thì ghi ProjectVersion mới (BE-76). |
| MB-30 | Bổ sung lần 2: S27 / S28 có loại thay đổi "Đổi đề tài". |
| MB-44 | Bổ sung lần 2: M01 có "Buổi mentor n/3", sửa số nhóm mong muốn; trước tuần 6 hiện màn trống. |
| FE-43 | Bổ sung lần 2: một mentor mỗi nhóm, cảnh báo / chặn theo sức chứa từng mentor. |
| FE-34 | Bổ sung lần 2: số nhóm tối đa riêng cho từng mentor; cài đặt tuần bắt đầu mentor và số buổi dự kiến. |
| FE-24 | Bổ sung lần 2: W18 hiện cả thay đổi đề tài. |

---

## Đợt 2 – ngày 20/10/2026: ghép giảng viên / mentor tự động, Checkpoint EXE101, lịch họp hằng tuần EXE201

> Nghiệp vụ: `docs/ux/PROJECT_CONTEXT.md` quy tắc 9, 19, 20, 25, 27 (cập nhật 20/10); lý do: `docs/ux/design-decisions.md` E12–E15; màn hình: `docs/ux/user-flow.md` mục 1.1a, F5, F8, F9; quy trình từng luồng: `docs/quy-trinh-he-thong.md`.
> **Đã cập nhật vào `EXE_Team_Matching_Phan_chia_task.xlsx`** (cột Ghi chú ghi "Sửa / Thêm 20/10/2026 – đợt 2"); file này cũng đã gộp trạng thái mới nhất từ Google Sheet, nên có thể import đè lên Sheet. BE-60, BE-61 đã merge theo luồng cũ; phần chênh lệch gom vào BE-78.

**Cách chia:** mỗi mảng mới giao trọn cho một người để ít phải chờ nhau. Thiên nhận trọn **ghép cố vấn tự động** (DB và contract, thuật toán, áp dụng, job, W19, W28). Nam nhận backend lịch họp hằng tuần. Cường (đang ít task nhất, sẵn có client realtime) làm giao diện C09. Thành nhận trọn **Checkpoint** (backend + L04, L05 vốn là màn hình của Thành).

**Số task sau đợt 2:** Thiên 40 · Nam 35 · Cường 34 · Nguyên 33 · Thành 32 (tổng 174).

### Task mới (8)

| Mã | Sheet | Module | Task | Ưu tiên | Thành viên | Phụ thuộc | Chờ người khác |
|---|---|---|---|---|---|---|---|
| BE-78 | Backend | Thiết kế | DB script và contract đợt 2 | P0 | Thiên | BE-60, BE-61 | – |
| BE-79 | Backend | Matching | Thuật toán ghép giảng viên và mentor | P0 | Thiên | BE-34, BE-78 | BE-34 (Nguyên) – chỉ cần khung gRPC |
| BE-80 | Backend | Team | Áp dụng kết quả ghép, Ghép sớm, đổi tay | P0 | Thiên | BE-78, BE-79 | – |
| BE-81 | Backend | Job Service | Job ghép giảng viên và mentor | P1 | Thiên | BE-51, BE-80 | BE-51 (Nguyên) – chỉ cần Jobs.Host |
| BE-82 | Backend | Collaboration – Meetings | Lịch họp hằng tuần EXE201 | P0 | Nam | BE-68, BE-78 | BE-78 (Thiên) |
| BE-83 | Backend | Collaboration – Checkpoints | Checkpoint EXE101 | P1 | Thành | BE-62, BE-78 | BE-62, BE-78 (Thiên) |
| FE-45 | Frontend Web | Workspace | Web C09 lịch họp hằng tuần | P0 | Cường | FE-33, BE-82 | BE-82 (Nam) |
| MB-46 | Mobile | Workspace | C09 Lịch họp hằng tuần | P0 | Cường | MB-38, BE-82 | BE-82 (Nam) |

Mô tả / tiêu chí hoàn thành đầy đủ nằm trong file xlsx.

### Task cũ thay đổi

| Mã | Thay đổi |
|---|---|
| MB-33 | **Thay nội dung:** L02, L03 bỏ (không còn chấp nhận / từ chối) → "Checkpoint EXE101 ở L04, L05" (Thành) |
| FE-25 | **Chuyển Thành → Thiên**, viết lại thành "W19 – Ghép giảng viên hướng dẫn" |
| FE-43 | **Chuyển Thành → Thiên**, viết lại thành "W28 – Ghép mentor" |
| BE-46 | **Viết lại:** nguyện vọng 1–3 có thứ tự, không còn gửi yêu cầu / chấp nhận / từ chối; sự kiện LecturerPreferencesUpdated |
| BE-47 | **Viết lại:** "Cố vấn của nhóm và nhóm của giảng viên" – API đọc kết quả ghép cho S15, S25, L04 |
| BE-74 | **Viết lại:** "Mentor của nhóm" – API cho nhóm và mentor, gỡ khi khóa mentor; ghép / đổi tay ở BE-80 |
| MB-27, MB-28 | **Viết lại:** S23 nguyện vọng, S24 chi tiết giảng viên, S25 giảng viên của nhóm + lý do ghép |
| BE-20, BE-23, BE-68, BE-71, BE-73, BE-75 | Bổ sung mô tả đợt 2 (số nhóm tối đa tùy chọn, cấu hình mới, họp đề xuất chỉ cho mentor, gợi ý cho cả mentor, quan tâm của mentor, thông báo mới) |
| FE-11, FE-17, FE-31, FE-39, FE-41, FE-42 | Bổ sung mô tả đợt 2 |
| MB-05, MB-32, MB-34, MB-36, MB-40, MB-43, MB-44 | Bổ sung mô tả đợt 2; MB-34 bỏ phụ thuộc MB-33; MB-40 thêm phụ thuộc BE-82, BE-83 |

Bảng "Điểm bàn giao" ở sheet Phân công thêm 6 dòng (BE-79 ← BE-34, BE-81 ← BE-51, FE-45 / MB-46 ← BE-82, MB-40 ← BE-82 / BE-83). Bảng phụ trách ở `AGENTS.md` mục 2 đã đổi theo.
