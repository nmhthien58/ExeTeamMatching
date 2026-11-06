# DB script (BE-01, BE-60)

Script tạo 6 database (mỗi service 1 database) trên một instance PostgreSQL 16. BE-60 (19/10/2026) cập nhật theo bản 4 của `docs/ux/PROJECT_CONTEXT.md` (quy tắc 16–26). Thiết kế: [docs/EXE_Database_Design.pdf](../../docs/EXE_Database_Design.pdf).

| File | Nội dung |
|---|---|
| `00_init.sh` | Tạo login role `identity_svc`, `academic_svc`, `team_svc`, `notification_svc`, `job_svc`, `collab_svc` và 6 database; mỗi role chỉ CONNECT được database của mình |
| `01_identity_db.sql` | Schema `identity`: roles (Student, Lecturer, Admin, Mentor), user_accounts, user_account_roles (một tài khoản nhiều vai trò), refresh_tokens, processed_events, client_error_reports (BE-87) |
| `02_academic_db.sql` | Schema `academic`: danh mục, học kỳ (tuần bắt đầu mentor, số buổi mentor), đợt đăng ký, sinh viên + 5 kỹ năng, giảng viên + domain chuyên môn, mentor + domain, import, rule_settings, outbox |
| `03_team_db.sql` | Schema `team`: đề tài + lịch sử đề tài, nhóm, thành viên, mốc EXE101, lời mời, yêu cầu tham gia + ý tưởng, bài đăng, đổi nhóm (cả đổi đề tài), quan tâm đề tài, giảng viên, mentor, audit log, outbox, trigger ép quy tắc |
| `04_notification_db.sql` | Schema `notification`: contacts, device_tokens, notifications, email_logs, processed_events |
| `05_job_db.sql` | Schema `hangfire` (bảng do Hangfire tự tạo) |
| `06_collab_db.sql` | Schema `collab`: workspace + thành viên (read model), kênh, tin nhắn, câu hỏi cho cố vấn, thư mục, file + phiên bản, việc + bài nộp, họp + khung giờ + biên bản, outbox |
| `90_seed_catalogs.sql` | 4 role, 16 chuyên ngành (tạm), domain D01–D18, mã lớp mẫu, học kỳ FA26, rule_settings |
| `99_seed_admin.sh` | Tài khoản Admin đầu tiên từ biến `ADMIN_EMAIL` (dòng user_accounts + user_account_roles) |

Dữ liệu demo không nằm ở đây: seed qua API, xem [`deploy/seed`](../seed/README.md) (BE-57).

## Chạy

Postgres trong `deploy/docker-compose.yml` mount thư mục này vào `/docker-entrypoint-initdb.d`, nên script **tự chạy một lần khi volume còn trống**.

```bash
cd deploy
cp .env.example .env        # sửa ADMIN_EMAIL thành email Google của Admin
docker compose up -d postgres
```

### Volume cũ cần thêm cột

Script không chạy lại trên volume đã có. Thiếu cột thì API trả 500 (lỗi `42703`). Giữ dữ liệu thì chạy:

```bash
docker exec exe-postgres psql -U postgres -d academic_db -c "SET ROLE academic_svc; ALTER TABLE academic.import_batches ADD COLUMN IF NOT EXISTS imported_by_name varchar(150);"
docker exec exe-postgres psql -U postgres -d team_db -c "SET ROLE team_svc; ALTER TABLE team.audit_logs ADD COLUMN IF NOT EXISTS actor_name varchar(150);"
docker exec -i exe-postgres psql -U postgres -d notification_db < deploy/db/migrations/20261105_be84_device_tokens.sql
```

Thêm bảng mới trên volume cũ: chạy script trong `migrations/`. Thư mục con không tự chạy lúc khởi tạo; volume mới có bảng vì `01_identity_db.sql` gọi script đó bằng `\ir`. Ví dụ BE-87:

```bash
docker exec -i exe-postgres psql -U postgres -d identity_db -v ON_ERROR_STOP=1 < deploy/db/migrations/BE-87_client_error_reports.sql
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
| `ck_join_requests_ideas`, `ck_join_request_ideas_position` | Yêu cầu tham gia cần 1–3 ý tưởng hoặc "Chưa có ý tưởng" (rule 17, kiểm tra lúc COMMIT) |
| `uq_topic_interests_open` | Cố vấn đã quan tâm nhóm này (rule 19) |
| `uq_lecturer_preferences_lecturer`, `pk_lecturer_preferences` | Trùng giảng viên / trùng thứ hạng nguyện vọng (rule 9) |
| `uq_slot_bookings_slot_active` | Slot vừa có nhóm khác đặt → `slot-taken` (rule 25) |
| `uq_slot_bookings_workspace_active` | Nhóm đã có slot – Services hủy dòng cũ trước khi đổi |
| `uq_checkpoint_reviews_workspace` | Checkpoint đã được đánh dấu (rule 27) |
| `uq_mentor_assignments_active`, `uq_workspace_members_one_mentor` | Nhóm đã có mentor – mỗi nhóm một mentor (rule 20) |
| `uq_teams_project_semester` | Đề tài đã có nhóm trong học kỳ (rule 26) |
| `uq_change_request_items_project_pending` | Nhóm đã có thay đổi đề tài đang chờ |
| `ck_file_versions_size`, `ck_project_files_type` | File quá 25 MB / sai định dạng (rule 24) |
| `ck_task_items_deliverable_creator` | Chỉ giảng viên hướng dẫn tạo bài nộp (rule 23) |

## Khác với PDF thiết kế bản 19/10/2026 (theo PROJECT_CONTEXT, tài liệu ưu tiên cao hơn)

| Chỗ | PDF | Script | Lý do |
|---|---|---|---|
| Trạng thái nhóm | FORMING / FINALIZED / DISBANDED | DRAFT / ACTIVE / NEEDS_REVIEW / FINALIZED / DISBANDED | Rule 13, 15: nhóm không hợp lệ sau hạn chót chuyển `NeedsReview` |
| Mốc EXE101 | Chỉ có `teams.source_team_id` | Thêm bảng `team_baseline_members` và cột `teams.is_inherited` | §5.4: mốc EXE101 import từ Excel, không có sẵn trong hệ thống |
| Thay đổi chờ duyệt | Item luôn thuộc một change request | `change_request_items.team_id`, `change_request_id` cho phép NULL, thêm `source` | Rule 7: thay đổi đã được hai bên đồng ý nằm chờ trước khi leader gửi yêu cầu |
| Yêu cầu đổi nhóm | – | Thêm `round_id`, `changed_member_count`, `changed_percent` | W17 lọc theo đợt, hiển thị số lượng và % thay đổi |
| Nguyện vọng giảng viên (BE-78) | ≤ 3 nguyện vọng có thứ tự, không có trạng thái | Như PDF: khóa chính (team_id, rank), không có trạng thái | Rule 9 (20/10/2026): bỏ bước giảng viên chấp nhận / từ chối |
| Phân công giảng viên (BE-78) | ASSIGNED / CONFIRMED / DECLINED / REVOKED | ASSIGNED / REVOKED, thêm `reason`, `preference_rank`, `source` (AUTO / MANUAL), `matching_run_id` | Rule 9: hệ thống ghép, Admin đổi tay |
| Lần ghép cố vấn (BE-78) | – | Bảng `advisor_matching_runs` (PREVIEW / APPLIED / DISCARDED, plan jsonb) | Rule 9, 20: Ghép sớm xem trước rồi chốt |
| Giảng viên | Không có sức chứa | Thêm `max_teams_exe101`, `max_teams_exe201` (NULL = không giới hạn) | Rule 9: Admin đặt, tùy chọn |
| Sinh viên | – | Thêm `is_seeking_team` | Công tắc "Tôi đang tìm nhóm" (S03) |
| Cấu hình | – | Thêm bảng `academic.rule_settings` | Màn W05 |
| Danh bạ thông báo | – | Thêm `contacts.email_enabled` | Công tắc nhận email (A07) |
| `team_members.join_source` | – | Thêm giá trị `ADMIN` | Admin thêm / chuyển thành viên (W16, W21) |
| Vai trò tài khoản (BE-60) | `user_accounts.role_id` | Bảng `user_account_roles`; thêm role Mentor | Rule 20: giảng viên vừa là mentor dùng một tài khoản |
| Hồ sơ sinh viên (BE-60) | `skills` dạng mảng chữ tự do | Bảng `student_skills` (5 kỹ năng 1–5); `bio` → `description`; `profile_completed_at` | Rule 16 |
| Chuyên môn giảng viên (BE-60) | `lecturers.expertise` chữ tự do | Bảng `lecturer_domains` | Rule 12, 19: gợi ý đề tài theo domain |
| Mentor (BE-60) | – | `mentors`, `mentor_domains`, `team.mentor_assignments`; `semesters.mentoring_start_week`, `expected_mentor_sessions` | Rule 20 |
| Đề tài (BE-60) | `teams.idea_title`, `idea_description` | Bảng `projects`, `project_versions`; `teams.project_id`; `change_request_items` có `PROJECT_CHANGE` | Rule 26 |
| Ý tưởng khi xin vào nhóm (BE-60) | – | `join_requests.has_no_idea`, bảng `join_request_ideas` | Rule 17 |
| Quan tâm đề tài (BE-60, BE-78) | – | Bảng `topic_interests` cho cả giảng viên và mentor (`advisor_kind`, `advisor_id`) | Rule 19 |
| Mentor ghép tự động (BE-78) | – | `mentor_assignments` thêm `reason`, `source`, `matching_run_id`, `note` | Rule 20 |
| Lịch họp hằng tuần (BE-78) | – | `meeting_schedules`, `schedule_windows`, `schedule_slots`, `slot_bookings`; `meetings.schedule_booking_id`, `week_no` | Rule 25 |
| Checkpoint EXE101 (BE-78) | – | Bảng `checkpoint_reviews` (không có điểm) | Rule 27 |
| Cài đặt kỳ (BE-78) | – | `semesters.slot_booking_deadline_week`, `meeting_slot_minutes`, `checkpoint_target_weeks`; `rule_settings.max_interests_unlimited`, `max_lecturer_preferences` | Rule 9, 19, 25, 27 |
| Cấu hình (BE-60) | – | `rule_settings.max_ideas_per_join_request`, `interest_extra_slots`, `max_file_size_mb` | Rule 17, 19, 24 |
| Collaboration (BE-60) | – | Database `collab_db` mới | §5.8 |

Từ 20/10/2026 PDF thiết kế được **sinh tự động từ script này** (mục 11 của PDF tóm tắt các thay đổi trên). Sửa script xong thì chạy lại:

```bash
python docs/db-design/build.py
```

Cần Docker (dựng PostgreSQL tạm từ thư mục này) và Chrome hoặc Edge (in PDF). Mô tả bảng lấy từ comment `--` ngay trên `CREATE TABLE`, ghi chú cột lấy từ comment cuối dòng; phần giải thích viết tay ở `docs/db-design/content.py`.
