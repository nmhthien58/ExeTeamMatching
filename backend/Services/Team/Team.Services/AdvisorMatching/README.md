# Module AdvisorMatching (BE-80, Thiên)

Ghép cố vấn cho nhóm (rule 9, 19, 20): Job gọi `POST /internal/advisor-matching/{kind}`, Admin Ghép sớm
(`preview` → `runs/{id}/confirm` hoặc `DELETE runs/{id}`), bảng W19/W28 (`{kind}/overview`), đổi giảng viên tay
(`PUT /api/teams/{teamId}/instructor`), đổi / gỡ mentor tay (`PUT` / `DELETE /api/teams/{teamId}/mentor-assignment`).

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

## Mentor (rule 20)

- `MentorAdvisorSource`: danh sách từ Academic `GET /api/mentors` (token dịch vụ, duyệt hết trang). API này trả cả mentor
  đang khóa nên lọc `isActive` ở đây. `maxTeams`, `preferredTeams` lấy từ Academic; `currentTeamCount` của Academic **không dùng**
  (tạm luôn 0) – số nhóm đang mentor đếm từ `mentor_assignments`, **chung hai môn trong học kỳ** (`CountMentorLoadsAsync`).
- Vì sức chứa chung hai môn, khóa `pg_advisory_xact_lock` của mentor dùng phạm vi `ALL` thay cho mã môn (`IAdvisorSource.LockScope`).
- Đầu vào nhóm: domain và quan tâm OPEN của mentor; không có nguyện vọng, không có giảng viên EXE101 (sinh viên không chọn mentor).
  Matching lấp tới số mong muốn trước rồi mới tới tối đa.
- `MentorAssignmentWriter` ghi `mentor_assignments`, nhật ký `MENTOR_ASSIGNED` / `MENTOR_UNASSIGNED` và sự kiện theo events.md.
  Thay mentor: dòng cũ `ENDED` (lưu trước, `uq_mentor_assignments_active`), chỉ phát `MentorAssigned` có `replacedMentorId`
  (Collaboration đổi người trong workspace). Gỡ hẳn mới phát `MentorUnassigned`.
- Đổi tay: mentor đã khóa → 400; đã đủ `maxTeams` → 409 `mentor-full`; vượt `preferredTeams` → 200 kèm `warnings: [ABOVE_PREFERRED]`;
  mentor có `lecturerId` trùng giảng viên hướng dẫn đang gán của nhóm → 409 `mentor-is-instructor`.
- Một người không vừa hướng dẫn vừa mentor cùng một nhóm (so `lecturerId` của mentor với `lecturer_assignments` ASSIGNED,
  `IAdvisorSource.ListBlockedLecturersAsync`). `PlanAdvisorMatching` không có trường loại trừ theo nhóm (không đổi .proto) nên khi
  ghép tự động / Ghép sớm, nhóm bị Matching ghép trùng được **tính lại** với danh sách mentor đã bỏ người đó (mỗi giảng viên một lời gọi,
  tải cộng phần đã ghép của lần đầu); không còn mentor phù hợp thì chưa ghép. Lần tính lại không cạnh tranh công bằng với các nhóm
  của lần đầu (mentor có thể đã hết chỗ). Lúc xác nhận Ghép sớm, nếu giảng viên vừa được đổi thành mentor trong phương án thì nhóm chưa ghép.
- Chiều ngược lại (đổi giảng viên hướng dẫn thành người đang mentor nhóm) **chưa chặn**.
- `matchingAt` của W28 = đầu tuần bắt đầu mentor (`MentoringSchedule.StartsAt`: `startDate + 7·(tuần − 1)` ngày, 00:00 UTC+7).
- Gỡ mentor khi Bộ môn khóa mentor (consumer `MentorRegistered`) là BE-74 của Thành; có thể dùng `MentorAssignmentWriter.End(..., unassigned: true)`.
