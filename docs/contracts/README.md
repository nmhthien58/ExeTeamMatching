# Hợp đồng giữa các service (BE-02, BE-61)

Bản nháp chốt ở tuần 1 để 5 người làm song song; BE-61 (19/10/2026) cập nhật theo bản 4 của `docs/ux/PROJECT_CONTEXT.md` (mentor, đề tài xuyên kỳ, quan tâm đề tài, workspace). Mọi thay đổi phải qua PR do Thiên duyệt (AGENTS.md mục 3.4).

| File | Nội dung |
|---|---|
| [openapi/identity.yaml](openapi/identity.yaml) | Đăng nhập Google → JWT (claim `roles` là danh sách), refresh, logout |
| [openapi/academic.yaml](openapi/academic.yaml) | Học kỳ, giai đoạn môn, đợt đăng ký, import, sinh viên (5 kỹ năng), giảng viên (domain chuyên môn), mentor, danh mục, cấu hình, endpoint nội bộ |
| [openapi/team.yaml](openapi/team.yaml) | Đề tài xuyên kỳ, nhóm, thành viên, lời mời, yêu cầu tham gia (kèm ý tưởng), bài đăng, gợi ý, đổi nhóm (cả đổi đề tài), quan tâm đề tài, giảng viên, mentor, chốt nhóm, ghép tự động, thống kê, endpoint nội bộ |
| [openapi/collab.yaml](openapi/collab.yaml) | Workspace nhóm: tin nhắn (+ SignalR hub), việc và bài nộp, file dự án (MinIO), lịch họp, endpoint nội bộ cho Job |
| [openapi/notification.yaml](openapi/notification.yaml) | Thông báo trong ứng dụng, cài đặt email, thông báo chung |
| [events.md](events.md) | Envelope, topic, payload sự kiện Kafka |
| [../../backend/Protos/matching.proto](../../backend/Protos/matching.proto) | gRPC Matching Service |

## Quy ước chung

- **Gateway:** client chỉ gọi `http://localhost:8080`. Route `/api/*` đi qua Gateway; route `/internal/*` chỉ gọi trong mạng Docker (Job → Team, Team → Academic), không mở ra ngoài.
- **Xác thực:** header `Authorization: Bearer <accessToken>`. JWT có claim `sub` (id tài khoản), `email`, `name`, `roles` (danh sách: `Student` | `Lecturer` | `Mentor` | `Admin` – một tài khoản có thể vừa Lecturer vừa Mentor), `student_id`, `lecturer_id`, `mentor_id` (có khi có vai trò tương ứng). Leader không phải role: Team Service kiểm tra theo dữ liệu nhóm. Hub SignalR nhận token qua query `access_token`.
- **Gọi nội bộ:** Job dùng token dịch vụ (client credentials, claim `role=Service`).
- **JSON:** camelCase; thời gian ISO 8601 UTC (`2026-10-19T03:15:00Z`); id là uuid dạng chuỗi.
- **Phân trang:** query `page` (từ 1), `pageSize` (mặc định 20, tối đa 100); kết quả `{ items, page, pageSize, totalCount, totalPages }`.
- **Sắp xếp:** `sort=name` tăng dần, `sort=-createdAt` giảm dần.
- **Lỗi:** `application/problem+json` (RFC 7807) gồm `type`, `title`, `status`, `detail`, `traceId`; lỗi validate có thêm `errors` theo field. Lỗi nghiệp vụ trả 409 với `type` dạng `https://exe-team-matching/errors/<mã>`:

| Mã | Khi nào |
|---|---|
| `round-closed` | Đợt đăng ký chưa mở hoặc đã khóa (rule 6) |
| `already-in-team` | Sinh viên đã thuộc một nhóm trong học kỳ (rule 4) |
| `team-full` | Nhóm đã đủ 6 người |
| `not-leader` | Thao tác chỉ dành cho trưởng nhóm |
| `pending-exists` | Đã có lời mời / yêu cầu / yêu cầu đổi nhóm đang chờ |
| `composition-invalid` | Chốt nhóm khi chưa đủ 4–6 người hoặc 2 chuyên ngành (rule 1, 2) |
| `change-request-not-allowed` | Đổi nhóm với nhóm EXE101 hoặc nhóm không kế thừa (rule 7) |
| `lecturer-full` | Admin đổi tay sang giảng viên đã đủ số nhóm tối đa (rule 9) |
| `preferences-locked` | Sửa nguyện vọng khi nhóm đã được ghép giảng viên hoặc đợt đã đóng (rule 9) |
| `run-not-preview` | Áp dụng (`POST /api/advisor-matching/runs/{id}/confirm`) hoặc bỏ (`DELETE /api/advisor-matching/runs/{id}`) lần ghép cố vấn không còn ở trạng thái PREVIEW (rule 9, 20) |
| `interest-closed`, `team-has-advisor` | Quan tâm ngoài giai đoạn / nhóm đã có cố vấn cùng loại (rule 19) |
| `slot-taken`, `booking-closed`, `slot-booked` | Slot vừa có nhóm khác đặt / quá hạn chọn slot / sửa khung bỏ slot đã có nhóm (rule 25) |
| `checkpoint-locked` | Bỏ đánh dấu checkpoint sau ngày đã đánh dấu (rule 27) |
| `not-in-list` | Email không có trong danh sách học kỳ (rule 5) |
| `ideas-required` | Yêu cầu tham gia thiếu ý tưởng và không tích "Chưa có ý tưởng" (rule 17) |
| `interest-limit` | Giảng viên đã đạt giới hạn quan tâm đề tài (rule 19) |
| `team-has-mentor`, `mentor-full` | Nhóm đã có mentor / mentor đã đủ số nhóm tối đa (rule 20) |
| `project-change-pending` | Nhóm đã có thay đổi đề tài đang chờ (rule 26) |
| `skills-incomplete` | Hồ sơ chưa đánh giá đủ 5 kỹ năng (rule 16) |
| `not-workspace-member` (403) | Không thuộc workspace của nhóm (rule 21) |
| `workspace-read-only` (403) | Admin thao tác ghi trong workspace – Admin chỉ xem (rule 21) |
| `not-workspace-advisor` (403) | Thao tác chỉ dành cho giảng viên hướng dẫn hoặc mentor của nhóm |
| `archive-read-only` | Ghi vào thư mục / việc EXE101 kỳ trước (rule 26) |
| `instructor-only` (403) | Chỉ giảng viên hướng dẫn tạo / duyệt bài nộp (rule 23) |
| `file-too-large` (413), `file-type-not-allowed` (415) | File quá 25 MB / sai định dạng (rule 24) |
| `matching-unavailable` (503) | Team gọi Matching (gRPC) lỗi hoặc quá 5 giây: chốt nhóm, ghép tự động, gợi ý |
| `academic-unavailable` (503) | Team gọi Academic lỗi khi lấy danh sách cố vấn (giảng viên, mentor) để ghép cố vấn tự động |

## Dùng dữ liệu giả khi API chưa xong

Web và mobile có thể chạy mock server từ file OpenAPI:

```bash
npx @stoplight/prism-cli@5.12.0 mock docs/contracts/openapi/team.yaml --port 4010
```

Trỏ `VITE_API_BASE_URL` (web) hoặc `API_BASE_URL` (mobile) sang `http://localhost:4010` để có dữ liệu mẫu theo schema.
