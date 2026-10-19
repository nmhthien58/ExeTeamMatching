# EXE Team Matching System

Ứng dụng ghép nhóm môn EXE101 / EXE201 tại Đại học FPT, thay cho file Google Sheets nhập tay (~220 nhóm, ~1.250 sinh viên mỗi kỳ).
Sau khi lập nhóm, mỗi nhóm có một **Dự án** (workspace) để làm việc với giảng viên hướng dẫn và mentor: tin nhắn, việc và bài nộp, file, lịch họp. Đề tài được theo dõi xuyên kỳ từ EXE101 sang EXE201.

| Vai trò | Ứng dụng |
|---|---|
| Sinh viên, Giảng viên hướng dẫn, Mentor | **Mobile App (Flutter)** và **Web App (React)** – cùng mã màn hình |
| Bộ môn EXE | **Web App (React)** |

> **19/10/2026:** tài liệu đã cập nhật theo góp ý của giảng viên (bảng màu cam/xanh FPT, workspace, mentor, đề tài xuyên kỳ…) – xem [PROJECT_CONTEXT.md](docs/ux/PROJECT_CONTEXT.md) mục 13. **Figma vẫn là bản Lab 2**, chưa vẽ lại.

- **Figma:** https://www.figma.com/design/tX0K0MdkTKt5wJcvWnPbxr
- **Môn:** PRM323 Lab 2 – Thiết kế UI/UX có hỗ trợ AI · PRN232 (backend)

## Chạy bằng Docker

Cần Docker Desktop. Từ thư mục gốc repo:

```bash
cp deploy/.env.example deploy/.env
docker compose -f deploy/docker-compose.yml up -d --build
```

| Địa chỉ | Nội dung |
|---|---|
| http://localhost:3000 | Web (React) |
| http://localhost:8080 | API Gateway – client chỉ gọi địa chỉ này; Swagger gộp ở `/swagger` |
| http://localhost:5001 … 5007 | Từng service (Identity, Academic, Team, Matching gRPC, Notification, Jobs, Collab), mỗi service có `/health` |
| http://localhost:8085 · 8025 · 9001 | Kafka UI · Mailpit (email dev) · giao diện file S3 |

Mọi service .NET dùng chung `deploy/docker/dotnet.Dockerfile`. Bảng cổng đầy đủ: [PROJECT_CONTEXT.md](docs/ux/PROJECT_CONTEXT.md) mục 9. Lấy JWT thử không cần Google: `POST http://localhost:8080/api/dev/token` ([docs/setup/google-sign-in.md](docs/setup/google-sign-in.md) mục 6).

DB script chỉ chạy khi volume Postgres còn trống. Nếu volume tạo từ script cũ (ví dụ thiếu `collab_db`), tạo lại (mất dữ liệu local): `docker compose -f deploy/docker-compose.yml down -v` rồi `up -d --build`.

## Artifact Lab 2 (`docs/ux/`)

| Artifact | File | Slide |
|---|---|---|
| Persona (P1–P5, gồm Mentor), problem statement, chỉ số thành công | [persona.md](docs/ux/persona.md) | 02–02e |
| Information architecture, user flow (F1–F3 Lab 2, F4–F7 bổ sung) | [user-flow.md](docs/ux/user-flow.md), [ảnh Figma 01](docs/ux/user-flow-figma-01.png) | 03, 04 |
| Design system bản 4 (token, component, mẫu tương tác, web cho mọi vai trò) | [DESIGN.md](docs/ux/DESIGN.md) | 09, 10 |
| AI log: prompt, phản biện, quyết định, ảnh trước/sau | [ai-design-log.md](docs/ux/ai-design-log.md), [ai-log-truoc-sau/](docs/ux/ai-log-truoc-sau/) | 05, 06, 06b |
| Design decisions (Phần E: quyết định theo góp ý của giảng viên), accessibility | [design-decisions.md](docs/ux/design-decisions.md) | 07, 10 |
| Flutter handoff: 2 bảng ánh xạ, 6 mục cho mỗi màn hình | [flutter-handoff.md](docs/ux/flutter-handoff.md) | 12, 12b |
| Danh sách màn hình (A01–L07, C01–C08, M01 cho mobile và web; W01–W29 cho Bộ môn) | [EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx](docs/ux/EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx) | 03 |
| Context cho AI coding assistant | [PROJECT_CONTEXT.md](docs/ux/PROJECT_CONTEXT.md) | – |

## Khác

| Thư mục / file | Nội dung |
|---|---|
| [AGENTS.md](AGENTS.md) | Quy tắc cho thành viên và AI agent: tài liệu phải đọc, ai phụ trách thư mục nào, quy tắc nhánh, commit, PR |
| [Google Sheet phân công task](https://docs.google.com/spreadsheets/d/17i2s3hWEHvSUk5tS-TCRMvDZ9nsaShP44PwQjivsZ7I/edit?usp=sharing) | Phân công task cho 5 thành viên (Backend, Frontend Web, Mobile) – bản chính; [bản xlsx](docs/EXE_Team_Matching_Phan_chia_task.xlsx) trong repo là bản chụp; thay đổi ngày 19/10/2026 cần dán vào sheet: [task-changes-2026-10-19.md](docs/task-changes-2026-10-19.md) |
| [docs/EXE_Database_Design.pdf](docs/EXE_Database_Design.pdf) | Thiết kế database (mỗi service 1 database) – chưa có `collab_db` và các bảng bản 4 (task BE-60) |
| [docs/contracts/](docs/contracts/) | Contract API, proto, sự kiện Kafka – chưa có bản 4 (task BE-61) |
| [docs/lab2-slide/](docs/lab2-slide/) | Slide trình bày Lab 2 và hướng dẫn chấm |
| [docs/c4-architecture/](docs/c4-architecture/) | Sơ đồ kiến trúc C4 (C0, C1) |

Figma file gồm 6 page: `01 User Flow`, `02 Wireframe`, `03 Final UI`, `04 Design System`, `05 Components`, `06 Prototype`.
