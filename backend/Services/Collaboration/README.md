# Collaboration Service (BE-62)

Workspace của nhóm: tin nhắn, việc và bài nộp, file, lịch họp, checkpoint. Cổng `5007`, database `collab_db` (schema `collab`).

## Cấu trúc 3 lớp và module

| Project | Nội dung |
|---|---|
| `Collab.Api` | Controller theo module (`Controllers/<Module>/`), request/response model, hub SignalR (BE-63) |
| `Collab.Services` | Nghiệp vụ theo module (`<Module>/`), ghi Outbox, handler sự kiện Kafka |
| `Collab.Repositories` | `Context/CollabDbContext.cs`, entity và repository theo module (`<Module>/`) |

| Module | Người làm | Bảng |
|---|---|---|
| Workspaces (khung) | Thiên | `workspaces`, `workspace_members` |
| Messages | Cường | `channels`, `messages`, `message_attachments`, `message_reads`, `advisor_questions` |
| Files | Nguyên | `folders`, `project_files`, `file_versions`, `file_comments` |
| Tasks | Nguyên | `task_items`, `task_assignees`, `task_comments`, `deliverable_submissions` |
| Meetings | Nam | `meetings`, `meeting_slots`, `meeting_responses`, `meeting_minutes`, `meeting_reminders`, `meeting_schedules`, `schedule_windows`, `schedule_slots`, `slot_bookings` |
| Checkpoints | Thành | `checkpoint_reviews` |

Entity trong `Collab.Repositories/<Module>/` được sinh từ `deploy/db/06_collab_db.sql` (tên cột snake_case qua `UseSnakeCaseNamingConvention`). `CollabDbContext` và DB script chỉ Thiên sửa; cần thêm bảng, cột hoặc quan hệ điều hướng thì đề xuất qua PR.

## Đã có sẵn trong khung

- **Quyền workspace (rule 21):** consumer `exe.team` và `exe.assignment` dựng `workspace_members` (TeamCreated tạo workspace và hai kênh TEAM, ADVISORS). Giảng viên làm mentor được lưu theo `lecturer_id`.
- **Policy `WorkspaceMember`:** gắn `[Authorize(Policy = CollabPolicies.WorkspaceMember)]` lên action có `{workspaceId}` trong route; lấy vai trò bằng `HttpContext.GetWorkspaceAccess()` (`IsLeader`, `IsInstructor`, `IsMentor`, `IsReadOnly` cho Admin). Người ngoài nhận 403 `not-workspace-member`.
- **Outbox:** gọi `db.AddOutboxMessage(Topics.Collab, "TaskAssigned", "Workspace", workspaceId, payload)` trước `SaveChangesAsync`.
- **Đăng ký service của module:** thêm vào `Collab.Services/ServiceCollectionExtensions.cs` (một dòng mỗi module).

## Chạy thử

```bash
docker compose -f deploy/docker-compose.yml up -d postgres kafka
dotnet run --project backend/Services/Collaboration/Collab.Api
```

`GET http://localhost:5007/api/workspaces/me` với JWT có claim `student_id` / `lecturer_id` / `mentor_id`.
