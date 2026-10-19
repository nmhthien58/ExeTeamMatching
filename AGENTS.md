# AGENTS.md – Quy tắc làm việc cho thành viên và AI agent

> File này dành cho mọi AI coding agent (Claude Code, Codex, Cursor, Copilot…) và cho 5 thành viên nhóm.
> Đọc hết file này và các tài liệu ở mục 1 **trước khi** viết code, tạo nhánh hay mở PR.

Dự án: **EXE Team Matching System** – PRN232 Final Assignment (backend .NET 8 microservices) + PRM323 (Flutter, React).
Nhóm: Thiên (trưởng nhóm), Nam, Cường, Nguyên, Thành.

---

## 1. Tài liệu phải đọc

Đọc theo thứ tự. Khi các tài liệu mâu thuẫn nhau, tin tài liệu đứng trước trong danh sách và **báo lại cho Thiên**, không tự đoán.

| # | File | Đọc khi | Nội dung |
|---|---|---|---|
| 1 | [docs/ux/PROJECT_CONTEXT.md](docs/ux/PROJECT_CONTEXT.md) | Luôn đọc | Nghiệp vụ, 27 quy tắc bắt buộc, kiến trúc, service, sự kiện Kafka, công nghệ, quy ước code, cổng Docker |
| 2 | [docs/EXE_Database_Design.pdf](docs/EXE_Database_Design.pdf) | Đụng tới DB, entity, DbContext | Mỗi service 1 database; bảng, cột, ràng buộc, index, trigger; tham chiếu chéo service |
| 3 | [Google Sheet phân công task](https://docs.google.com/spreadsheets/d/17i2s3hWEHvSUk5tS-TCRMvDZ9nsaShP44PwQjivsZ7I/edit?usp=sharing) (**bản chính**) | Luôn đọc | Danh sách task (BE-xx, FE-xx, MB-xx), người phụ trách, phụ thuộc, tiêu chí hoàn thành, trạng thái. File [docs/EXE_Team_Matching_Phan_chia_task.xlsx](docs/EXE_Team_Matching_Phan_chia_task.xlsx) chỉ là bản chụp, có thể đã cũ. Lịch sử thay đổi task (19/10 theo góp ý giảng viên, 20/10 đợt 2): [docs/task-changes-2026-10-19.md](docs/task-changes-2026-10-19.md) |
| 3a | [docs/quy-trinh-he-thong.md](docs/quy-trinh-he-thong.md) | Luôn đọc | Quy trình hệ thống theo từng luồng (ai làm, khi nào, các bước, màn hình, service, sự kiện) |
| 4 | [docs/c4-architecture/PRN232 - Final Assignment.pdf](docs/c4-architecture/PRN232%20-%20Final%20Assignment.pdf) | Luôn đọc | Đề bài, yêu cầu kỹ thuật, tiêu chí chấm |
| 5 | [docs/c4-architecture/c1.mmd](docs/c4-architecture/c1.mmd) (ảnh `c1.png`), [EXE_Team_Matching_Kien_truc_C4.docx](docs/c4-architecture/EXE_Team_Matching_Kien_truc_C4.docx) | Làm backend, giao tiếp giữa service | Sơ đồ container, ma trận giao tiếp, proto gRPC, sự kiện, background job |
| 6 | [docs/ux/DESIGN.md](docs/ux/DESIGN.md) | Làm web hoặc mobile | Design token, component, quy ước giao diện |
| 7 | [docs/ux/flutter-handoff.md](docs/ux/flutter-handoff.md) | Làm mobile | Ánh xạ token/component sang Flutter, spec từng màn hình, checklist |
| 8 | [docs/ux/EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx](docs/ux/EXE_Team_Matching_Danh_sach_man_hinh_v2.xlsx) | Làm web hoặc mobile | Nội dung và trạng thái cần có của mọi màn hình (A01–L07, M01, C01–C09, S01–S31, W01–W29) |
| 9 | [docs/ux/user-flow.md](docs/ux/user-flow.md), [docs/ux/design-decisions.md](docs/ux/design-decisions.md) | Làm web hoặc mobile | Luồng điều hướng, lý do các quyết định thiết kế, accessibility |
| 10 | [docs/ux/persona.md](docs/ux/persona.md) | Khi cần hiểu người dùng | Persona, vấn đề, chỉ số thành công |

Figma: https://www.figma.com/design/tX0K0MdkTKt5wJcvWnPbxr (page `03 Final UI` cho giao diện chuẩn).

---

## 2. Cấu trúc repo và người phụ trách

Chia theo **nghiệp vụ**: mỗi người sở hữu trọn một mảng (backend + web + mobile). Chỉ sửa code trong vùng của mình. Muốn sửa vùng của người khác thì mở PR và để người đó review.

```
backend/                     ASP.NET Core 8 (solution backend/ExeTeamMatching.sln)
  BuildingBlocks/            Thiên   – JWT, logging, exception, Kafka, Outbox, event contracts
  ApiGateway/                Thiên
  Protos/                    Thiên   – *.proto dùng chung
  Services/
    Identity/                Thiên
    Academic/                Nam
    Team/                    Cường (lõi) – xem bảng chia module bên dưới
    Matching/                Nguyên – module AdvisorMatching: Thiên
    Jobs/                    Nguyên – job ghép cố vấn: Thiên
    Notification/            Thành
    Collaboration/           Thiên (khung, hub, lưu file S3) – module Messages: Cường · Files, Tasks: Nguyên · Meetings: Nam · Checkpoints: Thành
web/                         React (Bộ môn, Sinh viên, Giảng viên, Mentor – cùng mã màn hình với mobile)
  src/app, src/shared, src/features/auth, layout   Thiên
  src/features/<feature>     theo bảng bên dưới
mobile/                      Flutter (Sinh viên, Giảng viên, Mentor)
  lib/core, lib/shared, lib/features/auth          Thiên
  lib/features/<feature>     theo bảng bên dưới (tên thư mục snake_case)
deploy/
  docker-compose*.yml        Thiên
  db/                        Thiên   – DB script cho 6 database
docs/                        Thiên   – mọi người được đề xuất sửa qua PR
```

| Người | Backend | Web (`web/src/features/…`) | Mobile (`mobile/lib/features/…`) |
|---|---|---|---|
| **Thiên** | BuildingBlocks, Gateway, Identity (gồm role Mentor), Protos, `deploy/`, DB script; khung Collaboration Service, SignalR hub, lưu file S3; **ghép cố vấn tự động** (từ 20/10/2026): Matching – module AdvisorMatching, Team – module AdvisorMatching, Jobs – `LecturerMatchingJob`, `MentorMatchingJob` | `auth`, khung trang, theme, khung theo vai trò, component dùng chung (W01, W25, W26); `lecturer-matching`, `mentor-matching` (W19, W28) | `auth`, theme, shell theo giai đoạn và vai trò, widget dùng chung (A03, A04, A08) |
| **Nam** | Academic Service (gồm mentor, chuyên môn giảng viên, giai đoạn môn, hồ sơ kỹ năng); Collaboration – module Meetings (gồm lịch họp hằng tuần EXE201) | `semesters`, `rounds`, `imports`, `students`, `lecturers`, `mentors`, `catalogs`, `settings` (W03–W14, W27); `profile`, `meetings` (A05, S02–S04, L06, C07, C08) | `profile` (A05, S02–S04), `settings` (A07), `lecturer_profile` (L06), `meetings` (C07, C08) |
| **Cường** | Team Service – module Teams, Members, Invitations, JoinRequests (gồm ý tưởng), Posts, AuditLog, Outbox; Collaboration – module Messages | `teams`, `audit-log` (W15, W16, W24); `home`, `discover`, `invitations`, `my_team`, `messages`, `meeting-schedule` (S01–S22, C02, C03, C09); realtime client | `home` (S01), `discover` (S05, S06, S08–S11), `invitations` (S12), `my_team` (S13–S17, S20–S22), `messages` (C02, C03), `meeting_schedule` (C09); realtime client |
| **Nguyên** | Matching (gồm gợi ý đề tài), Jobs (gồm nhắc họp, hạn việc); Team Service – module Finalization, AutoAssign, Statistics, Export; Collaboration – module Files, Tasks; test và kịch bản demo | `dashboard`, `unmatched`, `auto-assign`, `finalize` (W02, W20–W22); `workspace`, `tasks`, `files`, `suggestions` (C01, C04–C06, S07, S18, S19, S30, S31) | `suggestions` (S07, S18, S19), `round_closed` (S30, S31), `workspace`, `tasks`, `files` (C01, C04–C06); build APK demo |
| **Thành** | Notification (gồm thông báo workspace); Team Service – module ChangeRequests, BaselineImport, Projects (đề tài xuyên kỳ), LecturerPreferences, TopicInterests, LecturerAssignments, MentorAssignments (đọc / API cho nhóm và cố vấn; ghép do Thiên); Collaboration – module Checkpoints | `change-requests`, `announcements`, `projects` (W17, W18, W23, W29); `lecturers`, `change_requests`, `notifications`, `lecturer`, `mentor` (S23–S29, A06, L01–L05, L07, M01) | `onboarding` (A01, A02), `notifications` (A06), `lecturers` (S23–S25), `change_requests` (S26–S29), `lecturer` (L01, L04, L05 gồm Checkpoint, L07), `mentor` (M01) |

Team Service do 3 người cùng làm, nên mỗi module nằm trong thư mục riêng ở cả 3 project (`Team.Api/Controllers/<Module>`, `Team.Services/<Module>`, `Team.Repositories/<Module>` – entity của module nằm trong `Team.Repositories/<Module>`). `TeamDbContext` và migration/DB script của Team chỉ Thiên và Cường được sửa.

Collaboration Service (thêm 19/10/2026) chia giống vậy: mỗi module Messages, Tasks, Files, Meetings, Checkpoints một thư mục trong 3 project `Collab.*`. Thiên dựng khung, hub SignalR và lưu file S3; `CollabDbContext` và DB script của `collab_db` chỉ Thiên được sửa.

Web cho Sinh viên, Giảng viên, Mentor dùng **cùng mã màn hình** với mobile; người làm màn hình mobile của một mảng làm luôn bản web của mảng đó (`DESIGN.md` mục 8.2).

---

## 3. Quy tắc nhánh git

### 3.1 Nhánh cố định
| Nhánh | Mục đích | Ai được merge |
|---|---|---|
| `main` | Bản nộp, luôn chạy được bằng `docker compose up` | Chỉ Thiên, merge từ `develop` khi xong một giai đoạn |
| `develop` | Nhánh tích hợp hằng ngày | Qua PR đã được duyệt |

Không commit trực tiếp lên `main` hoặc `develop`. Không `push --force` lên hai nhánh này.

### 3.2 Nhánh làm việc
- Tạo từ `develop` mới nhất:
  ```bash
  git switch develop && git pull
  git switch -c feat/BE-29-invitations
  ```
- Tên nhánh: `<loại>/<mã-task>-<mô-tả-ngắn>`, chữ thường, gạch ngang, không dấu.
  - `<loại>`: `feat` (tính năng), `fix` (sửa lỗi), `refactor`, `test`, `docs`, `chore` (cấu hình, build).
  - `<mã-task>`: mã trong Google Sheet phân công task (`BE-29`, `FE-21`, `MB-14`). Một nhánh làm nhiều task thì ghi mã chính.
  - Ví dụ: `feat/BE-01-db-script`, `feat/MB-14-discover-teams`, `fix/FE-24-reject-dialog`.
- Một nhánh cho một task hoặc một nhóm task liền nhau; không gom việc của nhiều mảng vào một nhánh.
- Cập nhật nhánh mỗi ngày để tránh conflict lớn:
  ```bash
  git fetch origin
  git rebase origin/develop
  ```
  Chỉ rebase nhánh của chính mình. Sau khi rebase, đẩy lên bằng `git push --force-with-lease` (chỉ trên nhánh của mình).
- Xóa nhánh sau khi PR đã merge.

### 3.3 Commit
Theo Conventional Commits, kèm mã task:
```
<loại>(<phạm-vi>): <mô tả ngắn, tiếng Việt hoặc tiếng Anh> (<mã-task>)
```
- `<phạm-vi>`: `identity`, `academic`, `team`, `matching`, `jobs`, `notification`, `gateway`, `building-blocks`, `db`, `web`, `mobile`, `docs`, `deploy`.
- Ví dụ: `feat(team): gửi và chấp nhận lời mời (BE-29)`, `fix(mobile): giữ lời nhắn khi gửi lỗi (MB-16)`.
- Commit nhỏ, mỗi commit build được. Không commit file sinh ra (`bin/`, `obj/`, `node_modules/`, `build/`, `.dart_tool/`).

### 3.4 Pull request
- Mở PR vào `develop`. Tiêu đề giống commit chính: `feat(team): gửi và chấp nhận lời mời (BE-29)`.
- Mô tả PR gồm: mã task, việc đã làm, cách kiểm tra (lệnh chạy, request mẫu), ảnh chụp màn hình nếu có UI, các điểm chưa xong.
- Trước khi mở PR: build không lỗi, test liên quan chạy qua, đã rebase lên `develop` mới nhất.
- Cần **ít nhất 1 người duyệt**:
  - Code trong vùng của người khác → người đó duyệt.
  - Sửa contract (OpenAPI, `.proto`, sự kiện Kafka), DB script, BuildingBlocks, Docker Compose → **Thiên** duyệt.
- Merge kiểu **Squash and merge**. Người mở PR tự merge sau khi được duyệt và CI (nếu có) xanh.
- Sau khi merge: cập nhật cột Trạng thái của task trong Google Sheet thành `Xong`.

### 3.5 Đổi task giữa các thành viên
- Chỉ sửa cột **Thành viên** của task trong Google Sheet (chọn từ danh sách) và ghi vào cột Ghi chú, ví dụ `Đổi từ Nam sang Cường, 25/10`. Sheet Phân công tự tính lại số task.
- Cột "Chờ người khác" và bảng "Điểm bàn giao" tính theo phân công ban đầu, không tự cập nhật; người đổi task tự sửa nếu cần.
- Nhận task thuộc mảng của người khác thì vẫn làm trong thư mục của mảng đó và để chủ mảng duyệt PR (mục 2). Chỉ khi chuyển hẳn cả một mảng mới sửa bảng phụ trách ở mục 2.

---

## 4. Quy tắc code (tóm tắt, chi tiết ở PROJECT_CONTEXT.md §7–8)

- **Backend:** ASP.NET Core 8, kiến trúc 3 lớp API – Services – Repositories như các bài lab PRN232 (không có project Domain; entity đặt trong Repositories). Controller không chứa nghiệp vụ. EF Core (Npgsql) map đúng bảng trong DB script, không tự đổi tên bảng/cột. Lỗi trả ProblemDetails. Log bằng Serilog kèm CorrelationId. Route số nhiều, kebab-case (`/api/change-requests`). Giờ lưu UTC.
- **Giao tiếp:** client chỉ gọi API Gateway. Giữa các service: REST nội bộ; gọi Matching bằng gRPC; báo sự kiện qua Kafka (ghi Outbox trong cùng transaction). Không service nào đọc database của service khác.
- **Database:** mỗi service một database (`identity_db`, `academic_db`, `team_db`, `notification_db`, `job_db`, `collab_db`; script ở `deploy/db/`). Muốn thay đổi bảng thì sửa DB script trong `deploy/db/` và cập nhật `docs/EXE_Database_Design.pdf`, PR do Thiên duyệt.
- **Web (React):** dùng component và API client dùng chung trong `web/src/shared`, không gọi `fetch` trực tiếp trong màn hình. Màn hình có đủ trạng thái đang tải / trống / lỗi như trong danh sách màn hình.
- **Mobile (Flutter):** lấy màu, chữ, khoảng cách qua `Theme.of(context)`, `AppSpacing`, `AppRadius`; không hardcode `Color(...)` hay `fontSize`. Làm đủ trạng thái và chạy checklist ở flutter-handoff.md §5.
- **Ngôn ngữ:** tên biến, class, API, commit type bằng tiếng Anh; chữ hiển thị cho người dùng bằng tiếng Việt; tài liệu bằng tiếng Việt.
- **Bí mật:** không commit mật khẩu, token, khóa Google OAuth, chuỗi kết nối thật. Dùng `appsettings.Development.json` với giá trị giả và biến môi trường / file `.env` (đã gitignore).
- **Dữ liệu demo:** chỉ dùng dữ liệu ẩn danh (không SĐT thật, email giả).

---

## 5. Quy tắc riêng cho AI agent

1. Đọc mục 1 trước. Nếu được giao một task, tìm task đó trong Google Sheet phân công (nếu không mở được thì dùng file xlsx trong `docs/` và nói rõ có thể đã cũ) và làm đúng phần "Mô tả / tiêu chí hoàn thành"; không làm thêm việc ngoài task.
2. Hỏi người dùng tên mình và mã task trước khi bắt đầu nếu chưa rõ. Chỉ sửa file trong vùng của người đó (mục 2).
3. Không tự đổi contract API, `.proto`, tên/payload sự kiện Kafka, DB script hay BuildingBlocks. Cần đổi thì dừng lại, ghi rõ đề xuất để người dùng mở PR cho Thiên duyệt.
4. Tài liệu mâu thuẫn hoặc thiếu thông tin: nêu rõ chỗ mâu thuẫn, đề xuất cách xử lý và hỏi lại, không tự đoán.
5. Làm việc trên nhánh đúng quy tắc mục 3. Không commit lên `main`/`develop`, không `push --force` lên nhánh chung, không merge PR thay người dùng.
6. Trước khi báo xong: build, chạy test liên quan, và nói rõ những gì chưa kiểm tra được.
7. Không bỏ qua hook, không tắt kiểm tra (lint, test) để cho qua.
