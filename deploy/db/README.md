# DB script (BE-01)

Script tạo 5 database (mỗi service 1 database) trên một instance PostgreSQL 16. Thiết kế gốc: [docs/EXE_Database_Design.pdf](../../docs/EXE_Database_Design.pdf).

| File | Nội dung |
|---|---|
| `00_init.sh` | Tạo login role `identity_svc`, `academic_svc`, `team_svc`, `notification_svc`, `job_svc` và 5 database; mỗi role chỉ CONNECT được database của mình |
| `01_identity_db.sql` | Schema `identity`: roles, user_accounts, refresh_tokens, processed_events |
| `02_academic_db.sql` | Schema `academic`: danh mục, học kỳ, đợt đăng ký, sinh viên, giảng viên, import, rule_settings, outbox |
| `03_team_db.sql` | Schema `team`: nhóm, thành viên, mốc EXE101, lời mời, yêu cầu tham gia, bài đăng, đổi nhóm, giảng viên, audit log, outbox, trigger ép quy tắc nhóm |
| `04_notification_db.sql` | Schema `notification`: contacts, notifications, email_logs, processed_events |
| `05_job_db.sql` | Schema `hangfire` (bảng do Hangfire tự tạo) |
| `90_seed_catalogs.sql` | Role, 16 chuyên ngành (tạm), domain D01–D18, mã lớp mẫu, học kỳ FA26, rule_settings |
| `99_seed_admin.sh` | Tài khoản Admin đầu tiên từ biến `ADMIN_EMAIL` |

## Chạy

Postgres trong `deploy/docker-compose.yml` mount thư mục này vào `/docker-entrypoint-initdb.d`, nên script **tự chạy một lần khi volume còn trống**.

```bash
cd deploy
cp .env.example .env        # sửa ADMIN_EMAIL thành email Google của Admin
docker compose up -d postgres
```

Sửa script xong muốn chạy lại từ đầu (xóa hết dữ liệu):

```bash
docker compose down -v
docker compose up -d postgres
```

Chuỗi kết nối cho từng service (dev):

```
Host=localhost;Port=5432;Database=team_db;Username=team_svc;Password=team_dev_pw
```

Mỗi role đã có `search_path` trỏ tới schema của service (`team`, `academic`…), nên EF Core chỉ cần `HasDefaultSchema("team")`.
Không dùng EF migration để tạo bảng: entity và DbContext map đúng bảng trong script; muốn đổi bảng thì sửa script, cập nhật PDF thiết kế và mở PR cho Thiên duyệt.

## Ràng buộc trả về tên cố định

Trigger và constraint dùng tên ổn định để service map lỗi PostgreSQL (`SqlState` 23505 / 23514 / 23503 + `ConstraintName`) sang ProblemDetails, ví dụ:

| Constraint | Ý nghĩa |
|---|---|
| `uq_team_members_semester_student` | Sinh viên đã thuộc một nhóm trong học kỳ (rule 4) |
| `ck_teams_member_count` | Nhóm đã đủ 6 người |
| `uq_team_members_one_leader`, `ck_team_exactly_one_leader` | Nhóm phải có đúng 1 trưởng nhóm (kiểm tra lúc COMMIT, đổi leader trong cùng transaction được) |
| `ck_team_finalized_size`, `ck_team_finalized_specializations` | Chốt nhóm cần 4–6 người và ≥ 2 chuyên ngành (rule 1, 2) |
| `uq_invitations_pending`, `uq_join_requests_pending` | Đã có lời mời / yêu cầu đang chờ |
| `uq_change_requests_pending` | Nhóm đã có yêu cầu đổi nhóm đang chờ duyệt |
| `ex_registration_rounds_overlap` | Đợt đăng ký bị chồng thời gian |
| `ck_audit_logs_append_only` | Audit log không được sửa / xóa |

## Khác với PDF thiết kế (theo PROJECT_CONTEXT, tài liệu ưu tiên cao hơn)

| Chỗ | PDF | Script | Lý do |
|---|---|---|---|
| Trạng thái nhóm | FORMING / FINALIZED / DISBANDED | DRAFT / ACTIVE / NEEDS_REVIEW / FINALIZED / DISBANDED | Rule 13, 15: nhóm không hợp lệ sau hạn chót chuyển `NeedsReview` |
| Mốc EXE101 | Chỉ có `teams.source_team_id` | Thêm bảng `team_baseline_members` và cột `teams.is_inherited` | §5.4: mốc EXE101 import từ Excel, không có sẵn trong hệ thống |
| Thay đổi chờ duyệt | Item luôn thuộc một change request | `change_request_items.team_id`, `change_request_id` cho phép NULL, thêm `source` | Rule 7: thay đổi đã được hai bên đồng ý nằm chờ trước khi leader gửi yêu cầu |
| Yêu cầu đổi nhóm | – | Thêm `round_id`, `changed_member_count`, `changed_percent` | W17 lọc theo đợt, hiển thị số lượng và % thay đổi |
| Nguyện vọng giảng viên | ≤ 3 nguyện vọng có thứ tự, không có trạng thái | 1 nguyện vọng đang hiệu lực, có `status` PENDING / ACCEPTED / DECLINED / CANCELLED, lời nhắn, lý do từ chối | Rule 9: giảng viên chấp nhận / từ chối, bị từ chối thì chọn người khác |
| Phân công giảng viên | ASSIGNED / CONFIRMED / DECLINED / REVOKED | ASSIGNED / REVOKED | Rule 9: phân công của Admin là quyết định cuối |
| Giảng viên | Không có sức chứa | Thêm `expertise`, `max_teams`, `is_accepting_teams` | §5.3, màn S23, L01, L06 |
| Sinh viên | – | Thêm `is_seeking_team` | Công tắc "Tôi đang tìm nhóm" (S03) |
| Cấu hình | – | Thêm bảng `academic.rule_settings` | Màn W05 |
| Danh bạ thông báo | – | Thêm `contacts.email_enabled` | Công tắc nhận email (A07) |
| `team_members.join_source` | – | Thêm giá trị `ADMIN` | Admin thêm / chuyển thành viên (W16, W21) |

Các thay đổi này cần được cập nhật vào PDF thiết kế khi có file nguồn.
