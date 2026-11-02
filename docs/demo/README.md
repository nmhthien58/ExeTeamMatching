# Kịch bản demo end-to-end (BE-59)

Postman collection chạy các luồng demo ở [PROJECT_CONTEXT.md](../ux/PROJECT_CONTEXT.md) mục 11 trên hệ thống chạy bằng Docker Compose. Collection **tự tạo dữ liệu** qua API (học kỳ, đợt, import sinh viên), nên không phụ thuộc seed demo (BE-57) và chạy lại được nhiều lần.

| File | Nội dung |
|---|---|
| [EXE_Team_Matching_Demo.postman_collection.json](EXE_Team_Matching_Demo.postman_collection.json) | Collection (Postman v2.1): 9 thư mục, mỗi request có test kiểm tra kết quả |
| [sinh-vien-exe101-demo.xlsx](sinh-vien-exe101-demo.xlsx) | 10 sinh viên EXE101 **ẩn danh** (tên ghép ngẫu nhiên, email `@example.com`, không SĐT) theo mẫu import W07 |
| [sinh-vien-exe201-demo.xlsx](sinh-vien-exe201-demo.xlsx) | 6 sinh viên EXE201 ẩn danh: 4 thành viên nhóm A ở EXE101 (cùng MSSV) và 2 bạn mới |
| [moc-exe101-exe201-demo.csv](moc-exe101-exe201-demo.csv) | File mốc nhóm EXE101 → EXE201 (W06): tên nhóm, đề tài, trưởng nhóm, thành viên |
| [ke-hoach-du-an-demo.pdf](ke-hoach-du-an-demo.pdf) | File nhỏ để thử tải file lên dự án |

> Chỉ chạy trên môi trường **local / demo**. Collection đặt học kỳ demo làm học kỳ hiện hành và khóa đợt lập nhóm EXE101 đang mở (nếu có). JWT lấy qua `POST /api/dev/token`, endpoint này chỉ có khi `ASPNETCORE_ENVIRONMENT=Development`.

## Chạy

1. Bật hệ thống (xem [README](../../README.md) mục "Chạy bằng Docker"):
   ```bash
   docker compose -f deploy/docker-compose.yml up -d --build
   ```
   Chờ mọi container `healthy` (`docker compose -f deploy/docker-compose.yml ps`). Mới khởi động lại Matching thì chờ thêm khoảng 30 giây cho consumer Kafka nhận partition.
2. Chạy cả collection:
   - **Dòng lệnh** (Node.js, không cần cài gì thêm):
     ```bash
     npx newman@6 run docs/demo/EXE_Team_Matching_Demo.postman_collection.json --working-dir docs/demo
     ```
   - **Postman:** *Settings → General → Working directory* chọn thư mục `docs/demo` (để Postman tìm được file `.xlsx`, `.pdf`), Import file collection rồi *Run collection* (Collection Runner).
3. Cả lượt chạy mất khoảng 3–4 phút, vì luồng 4 chờ đợt lập nhóm **tự khóa đúng hạn chót**. Hạn chót = lúc tạo đợt + biến `roundMinutes` (mặc định 3 phút).

Các request tên "Chờ…" và vài request đọc dữ liệu sau sự kiện Kafka tự thử lại (`postman.setNextRequest`). Cơ chế này **chỉ chạy trong Runner / newman**. Bấm từng request bằng tay thì phải tự đợi rồi bấm lại.

Muốn trình bày từng bước bằng tay:
- Đặt biến collection `roundMinutes` lớn hơn (ví dụ `30`).
- Tới luồng 4 (thư mục 6), mở Hangfire http://localhost:5006/hangfire → *Scheduled* → `RoundAutoCloseJob` → *Enqueue now* để khóa đợt ngay.
- Email xem ở Mailpit http://localhost:8025, Kafka ở http://localhost:8085, file ở http://localhost:9001.

## Các luồng trong collection

| Thư mục | Luồng (§11) | Diễn ra |
|---|---|---|
| 0. Chuẩn bị dữ liệu | – | Health check, JWT Bộ môn, tạo học kỳ + đợt EXE101 và mở đợt (`RoundOpened` → Jobs hẹn giờ khóa), import 10 sinh viên (`StudentImported` → Matching), lấy JWT cho 10 sinh viên |
| 1. Tạo nhóm và mời | 1 | SV1 tạo nhóm A (Team hỏi Academic đợt có mở) → gợi ý thành viên (Team → Matching **gRPC**) → mời SV2, SV4 (`InvitationSent`) → chấp nhận (`MemberJoined`) |
| 2. Xin vào nhóm kèm ý tưởng | 2 | SV3 tìm nhóm (tìm kiếm, lọc, sắp xếp, phân trang) → gửi yêu cầu kèm 2 ý tưởng → trưởng nhóm xem ý tưởng và chấp nhận → nhóm A đủ 4 người, 4 chuyên ngành |
| 3. Dự án của nhóm | 7 (phần sinh viên) | Collaboration tạo dự án từ sự kiện Kafka → tin nhắn kênh Nhóm → việc giao cho SV2, kéo sang Đang làm → tải file lên S3, lấy link tải |
| 4. Họp nhóm | 6 (phần nhóm) | SV1 đề xuất họp 2 khung giờ (`MeetingProposed`) → SV2, SV3 trả lời → SV1 chốt khung 1 (`MeetingScheduled`, có thông báo) → kích hoạt `MeetingReminderJob` qua Hangfire → SV2 nhận đúng **một** nhắc họp trước 1 giờ (`MeetingReminder`) → biên bản → tạo 2 việc từ biên bản |
| 5. Thông báo | 9 | Thông báo trong app của SV2 (Notification consume `InvitationSent`), số chưa đọc, email trong Mailpit |
| 6. Hết hạn, chốt nhóm, ghép tự động | 4 | Nhóm B chỉ 2 người → dashboard, nhắc sinh viên chưa có nhóm → **đợt tự khóa** (`RoundAutoCloseJob`) → Team `finalize-round` (A `FINALIZED`, B `NEEDS_REVIEW`) → `AutoAssignUnmatchedJob` gọi Matching `PlanAutoAssignment` → 4 sinh viên còn lại được lấp vào B → xuất Excel → sinh viên gọi API của Bộ môn bị 403 (ProblemDetails) |
| 7. Sang EXE201 | 8 | Học kỳ kế tiếp: import sinh viên EXE201 (4 bạn cũ giữ nguyên id) → **import mốc** chạy thử rồi ghi (`TeamCreated`, `MemberJoined`, `TeamLinkedToProject`) → nhóm kế thừa dùng tiếp đề tài EXE101 (chạy demo lần sau Bộ môn gắn tay như W29 vì trùng tên nhóm) → dự án EXE201 có thư mục "EXE101 (kỳ trước)": đọc được file, tải lên bị 409 `archive-read-only` |
| 8. Đổi nhóm EXE201 | 3 | Mở đợt `CHANGE_REQUEST` → trưởng nhóm bỏ SV4, mời SV11 (chấp nhận nhưng chờ duyệt), đổi đề tài → 3 thay đổi chờ duyệt → gửi yêu cầu (`ChangeRequestSubmitted`) → Bộ môn duyệt (`ChangeRequestApproved`): thành viên và đề tài (phiên bản mới) đổi, có lịch sử, có thông báo → lần hai thêm SV12 → Bộ môn từ chối → nhóm giữ nguyên |

### Chưa có trong collection

| Luồng (§11) | Lý do |
|---|---|
| 5. Nguyện vọng → ghép giảng viên | Chưa có API quan tâm đề tài và ghép giảng viên (module AdvisorMatching) |
| 6. Phần mentor (mentor quan tâm nhóm, ghép mentor, buổi mentor) | Chờ ghép mentor (module AdvisorMatching). Phần họp của nhóm đã có ở thư mục 4 |
| 7. Bài nộp (deliverable) | Bài nộp do giảng viên hướng dẫn của nhóm tạo, nên cần ghép giảng viên trước (như luồng 5) |
| 10. Lịch họp hằng tuần EXE201 | Chờ BE-82 |
| 11. Checkpoint EXE101 | Giảng viên chấm checkpoint, cần ghép giảng viên trước |

## Đối chiếu yêu cầu PRN232 (PROJECT_CONTEXT §10)

| Yêu cầu | Chỗ trong code | Request chứng minh |
|---|---|---|
| REST API, CRUD, kiến trúc 3 lớp | `backend/Services/*/*.Api` → `*.Services` → `*.Repositories` | Gần như mọi request; tạo / sửa / đọc: học kỳ, đợt, nhóm, việc, file |
| Xác thực JWT, phân quyền | Identity phát token (`Identity.Api`), Gateway + từng service kiểm tra (`BuildingBlocks/Auth`) | Mọi request có `Authorization: Bearer`; "Kiểm tra quyền: sinh viên không xem được dashboard" (403) |
| Tìm kiếm, lọc, sắp xếp, phân trang | `Team.Services/Teams` (`TeamQuery`), Collaboration tin nhắn (cursor) | "SV3 tìm nhóm còn chỗ…", "SV2 đọc tin nhắn…" |
| Background job | `backend/Services/Jobs/Jobs.Host/Jobs` (Hangfire), Outbox dispatcher (`BuildingBlocks/Outbox`) | "Chờ đợt tự khóa (RoundAutoCloseJob)", "Chờ ghép tự động…", "Kích hoạt MeetingReminderJob ngay (Hangfire)" |
| Message broker: ≥ 1 producer, ≥ 1 consumer | Kafka: Academic / Team / Collaboration phát, Notification / Matching / Collaboration / Jobs nhận (`BuildingBlocks/Messaging`) | "Mở đợt…" (Jobs nhận `RoundOpened`), "SV3 (vào nhóm sau cùng…) thấy dự án…" (Collaboration nhận `MemberJoined`), "SV2 xem thông báo…" (Notification nhận `InvitationSent`) |
| Service gRPC độc lập được gọi từ REST API | `backend/Protos/matching.proto`, `Matching.Grpc`; Team gọi qua `Team.Services/Matching/MatchingClient.cs` | "Gợi ý thành viên cho nhóm A…", luồng 4 (`PlanAutoAssignment`) |
| Triển khai Docker | `deploy/docker-compose.yml`, `deploy/docker/dotnet.Dockerfile` | "Health check các service" |
| Thêm (không bắt buộc) | SignalR `/hubs/collab`, lưu file S3 (SeaweedFS) | "SV1 gửi tin…", "SV2 tải file lên dự án…" |
