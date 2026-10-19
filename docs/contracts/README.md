# Hợp đồng giữa các service (BE-02)

Bản nháp chốt ở tuần 1 để 5 người làm song song. Mọi thay đổi phải qua PR do Thiên duyệt (AGENTS.md mục 3.4).

| File | Nội dung |
|---|---|
| [openapi/identity.yaml](openapi/identity.yaml) | Đăng nhập Google → JWT, refresh, logout |
| [openapi/academic.yaml](openapi/academic.yaml) | Học kỳ, đợt đăng ký, import, sinh viên, giảng viên, danh mục, cấu hình, endpoint nội bộ |
| [openapi/team.yaml](openapi/team.yaml) | Nhóm, thành viên, lời mời, yêu cầu, bài đăng, gợi ý, đổi nhóm, giảng viên, chốt nhóm, ghép tự động, thống kê, endpoint nội bộ |
| [openapi/notification.yaml](openapi/notification.yaml) | Thông báo trong ứng dụng, cài đặt email, thông báo chung |
| [events.md](events.md) | Envelope, topic, payload sự kiện Kafka |
| [../../src/Protos/matching.proto](../../src/Protos/matching.proto) | gRPC Matching Service |

## Quy ước chung

- **Gateway:** client chỉ gọi `http://localhost:8080`. Route `/api/*` đi qua Gateway; route `/internal/*` chỉ gọi trong mạng Docker (Job → Team, Team → Academic), không mở ra ngoài.
- **Xác thực:** header `Authorization: Bearer <accessToken>`. JWT có claim `sub` (id tài khoản), `email`, `name`, `role` (`Student` | `Lecturer` | `Admin`), `student_id` hoặc `lecturer_id`. Leader không phải role: Team Service kiểm tra theo dữ liệu nhóm.
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
| `lecturer-full` | Giảng viên đã đủ sức chứa (rule 9) |
| `not-in-list` | Email không có trong danh sách học kỳ (rule 5) |

## Dùng dữ liệu giả khi API chưa xong

Web và mobile có thể chạy mock server từ file OpenAPI:

```bash
npx @stoplight/prism-cli@5.12.0 mock docs/contracts/openapi/team.yaml --port 4010
```

Trỏ `VITE_API_BASE_URL` (web) hoặc `API_BASE_URL` (mobile) sang `http://localhost:4010` để có dữ liệu mẫu theo schema.
