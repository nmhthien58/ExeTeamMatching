# Module AdvisorMatching (BE-80, Thiên)

Ghép cố vấn cho nhóm (rule 9, 19, 20): Job gọi `POST /internal/advisor-matching/{kind}`, Admin Ghép sớm
(`preview` → `runs/{id}/confirm` hoặc `DELETE runs/{id}`), bảng W19/W28 (`{kind}/overview`), đổi giảng viên tay
(`PUT /api/teams/{teamId}/instructor`).

## Luồng chung

1. Lấy nhóm chưa giải tán, chưa có cố vấn cùng loại của môn trong học kỳ.
2. `IAdvisorSource` của loại cố vấn cho danh sách cố vấn đang hoạt động, số nhóm đang có và đầu vào từng nhóm.
3. Gọi Matching `PlanAdvisorMatching` (gRPC, BE-79) – Matching chỉ tính.
4. Áp dụng trong **một transaction**, giữ `pg_advisory_xact_lock` theo (loại, học kỳ, môn): ghi assignment AUTO,
   nhật ký `LECTURER_ASSIGNED`, `LecturerAssigned`, `ITopicInterestService.CloseInterestsOnAssignmentAsync`
   (hàm này tự SaveChanges nên phải chạy trong transaction), rồi `AdvisorMatchingCompleted` (key = runId).
   Nguyện vọng bị khóa vì `LecturerPreferencesService` trả `preferences-locked` khi nhóm đã có giảng viên.
5. Job gọi lại: đã có lần SCHEDULED được áp dụng của (loại, học kỳ, môn) thì trả lần đó, không ghép thêm.

Ghép sớm khi xác nhận bỏ qua nhóm đã giải tán hoặc đã có cố vấn trong lúc chờ; cố vấn đã nghỉ hoặc đã đủ số tối đa
thì nhóm thành chưa ghép. `matchedCount` / `unmatchedCount` của lần chạy là kết quả thực tế.

## Nối mentor (khi Academic có API mentor – BE-66 của Nam)

`MentorAdvisorSource` hiện trả 501 `mentor-matching-unavailable` cho mọi thao tác. Cần:

- Academic: API danh sách mentor đang hoạt động kèm domain, **số nhóm tối đa** và **số mong muốn** (duyệt hết trang).
- Thay `MentorAdvisorSource` bằng lớp gọi API trên: `CountLoadsAsync` đếm `mentor_assignments` (mentor tính chung hai môn
  trong học kỳ – xem `TopicInterests/README.md`), `BuildTeamsAsync` không có nguyện vọng / giảng viên EXE101,
  `StageAutoAssignment` ghi `MentorAssignment` + `MentorAssigned` theo events.md.
- `GetOverviewAsync` đang đọc `lecturer_assignments`; khi nối mentor cần đọc `mentor_assignments` và trả `preferredTeams`.
- `PUT /api/teams/{teamId}/mentor-assignment` chưa làm.
