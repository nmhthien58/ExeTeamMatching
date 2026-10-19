# Sự kiện Kafka (BE-02, BE-61)

Nguồn: PROJECT_CONTEXT §6, thiết kế DB mục 8. BE-61 (19/10/2026) thêm sự kiện của bản 4: mentor, đề tài xuyên kỳ, quan tâm đề tài, workspace, nhắc lịch. Mọi thay đổi tên topic, tên sự kiện hoặc payload phải qua PR do Thiên duyệt.

## Envelope

Mọi message có cùng envelope (JSON, camelCase). Message key = `aggregateId` để giữ thứ tự theo đối tượng.

```json
{
  "eventId": "6f1c2a8e-3b0e-4b8f-9a51-2f7c1d0e9b11",
  "eventType": "InvitationSent",
  "occurredAt": "2026-10-19T03:15:00Z",
  "correlationId": "b7d9c0b1f2e34a5c",
  "producer": "team",
  "payload": { }
}
```

- `eventId`: uuid, chính là `outbox_messages.id`; consumer dùng để chống xử lý trùng.
- `occurredAt`: UTC, ISO 8601.
- Consumer retry 3 lần rồi đẩy sang `<topic>.dlq`.

## Topic

| Topic | Sự kiện | Producer | Consumer |
|---|---|---|---|
| `exe.student` | StudentImported, StudentProfileUpdated, LecturerRegistered, LecturerProfileUpdated, MentorRegistered | Academic (outbox) | Identity (tạo sẵn tài khoản), Notification (contacts), Matching (read model) |
| `exe.registration` | RoundOpened, RoundClosed | Academic (outbox) | Notification, Job (lên lịch đóng đợt) |
| `exe.registration` | DeadlineApproaching | Job (publish trực tiếp) | Notification |
| `exe.team` | TeamCreated, MemberJoined, MemberLeft, LeaderChanged, TeamFinalized, TeamNeedsReview, ProjectCreated, ProjectChanged, TeamLinkedToProject | Team (outbox) | Matching, Notification, Collaboration |
| `exe.invitation` | InvitationSent, InvitationAccepted, InvitationDeclined, InvitationExpired, JoinRequestSubmitted, JoinRequestAccepted, JoinRequestDeclined | Team (outbox) | Notification |
| `exe.change-request` | ChangeRequestSubmitted, ChangeRequestApproved, ChangeRequestRejected | Team (outbox) | Notification |
| `exe.assignment` | TopicInterestOpened, TopicInterestClosed, LecturerRequested, LecturerAccepted, LecturerDeclined, LecturerAssigned, MentorAssigned, MentorUnassigned | Team (outbox) | Notification, Matching (số quan tâm, nhóm đã có giảng viên), Collaboration (quyền workspace) |
| `exe.collab` | AdvisorQuestionAsked, AdvisorQuestionAnswered, UserMentioned, TaskAssigned, DeliverableCreated, DeliverableSubmitted, DeliverableReviewed, FileUploaded, MeetingProposed, MeetingScheduled, MeetingCancelled | Collaboration (outbox) | Notification |
| `exe.reminder` | MeetingReminder, TaskDueSoon, DeliverableOverdue | Job (publish trực tiếp) | Notification |

Tin nhắn thường **không** có sự kiện Kafka: Collaboration đẩy qua SignalR hub `/hubs/collab` (`messageCreated`, `messageUpdated`, `readUpdated`, `typing`) và không tạo thông báo (PROJECT_CONTEXT §5.7).

`InvitationExpired` do Team phát: Job gọi endpoint nội bộ `POST /internal/invitations/expire-overdue` của Team (Job không đụng database của Team).

## Payload

Kiểu: `uuid` là chuỗi uuid, thời gian là chuỗi UTC ISO 8601.

### exe.student

```jsonc
// StudentImported – mỗi sinh viên một message, key = studentId. phone từ file import (tùy chọn, chuỗi)
{ "studentId": "uuid", "studentCode": "SE184118", "fullName": "Nguyễn Văn A", "email": "a@gmail.com",
  "phone": "0901234567", "specializationId": 1, "semesterId": "uuid", "semesterEndDate": "2026-12-26",
  "courseCode": "EXE101", "classCode": "EXE101SE01" }

// StudentProfileUpdated – key = studentId. skills: đủ 5 kỹ năng (rule 16) hoặc rỗng khi chưa đánh giá
{ "studentId": "uuid", "fullName": "Nguyễn Văn A", "email": "a@gmail.com", "specializationId": 1,
  "skills": [ { "skill": "TECHNOLOGY", "level": 4 }, { "skill": "UI_UX_DESIGN", "level": 2 },
              { "skill": "MANAGEMENT", "level": 3 }, { "skill": "MARKETING", "level": 1 },
              { "skill": "COMMUNICATION_PLANNING", "level": 2 } ],
  "isSeekingTeam": true, "profileCompleted": true }

// LecturerRegistered – Admin thêm giảng viên, key = lecturerId
{ "lecturerId": "uuid", "fullName": "Trần Thị B", "email": "b@fe.edu.vn", "domainIds": [4, 7], "maxTeams": 8, "isActive": true }

// LecturerProfileUpdated – giảng viên / Admin sửa chuyên môn, sức chứa (Matching cập nhật read model), key = lecturerId
{ "lecturerId": "uuid", "domainIds": [4, 7], "maxTeams": 8, "isAcceptingTeams": true, "isActive": true }

// MentorRegistered – Admin tạo hoặc sửa mentor (rule 20), key = mentorId. Identity tạo / khóa tài khoản role Mentor;
// lecturerId != null → thêm role Mentor vào tài khoản sẵn có của giảng viên đó.
{ "mentorId": "uuid", "fullName": "Lê Văn Tuấn", "email": "tuan@startup.vn", "organization": "Startup X",
  "isInternal": false, "lecturerId": null, "maxTeams": 4, "isActive": true }
```

### exe.registration

```jsonc
// RoundOpened / RoundClosed – key = roundId
{ "roundId": "uuid", "semesterId": "uuid", "courseCode": "EXE101", "roundType": "TEAM_FORMATION",
  "opensAt": "...", "deadlineAt": "..." }

// DeadlineApproaching – key = roundId
{ "roundId": "uuid", "semesterId": "uuid", "courseCode": "EXE101", "deadlineAt": "...", "hoursLeft": 24,
  "studentIdsWithoutTeam": ["uuid"], "teamIdsUnderMinSize": ["uuid"] }
```

### exe.team (key = teamId)

```jsonc
// TeamCreated – Collaboration tạo workspace + 2 kênh; leader vào workspace_members
{ "teamId": "uuid", "semesterId": "uuid", "courseCode": "EXE101", "name": "Nhóm A", "leaderStudentId": "uuid",
  "leaderName": "Nguyễn Văn A", "projectId": "uuid|null", "isAutoAssigned": false, "isInherited": false,
  "sourceTeamId": "uuid|null" }
// MemberJoined
{ "teamId": "uuid", "semesterId": "uuid", "studentId": "uuid", "fullName": "Trần Văn C", "specializationId": 1,
  "joinSource": "INVITATION", "memberCount": 5 }
// MemberLeft
{ "teamId": "uuid", "semesterId": "uuid", "studentId": "uuid", "reason": "LEAVE", "memberCount": 4 }   // reason: LEAVE | REMOVED | CHANGE_REQUEST | ADMIN
// LeaderChanged
{ "teamId": "uuid", "oldLeaderStudentId": "uuid", "newLeaderStudentId": "uuid" }
// TeamFinalized
{ "teamId": "uuid", "semesterId": "uuid", "memberStudentIds": ["uuid"], "finalizedBy": "SYSTEM" }       // SYSTEM | ADMIN
// TeamNeedsReview
{ "teamId": "uuid", "semesterId": "uuid", "violations": ["TOO_FEW_MEMBERS"] }
// ProjectCreated – nhóm EXE101 đăng ký ý tưởng (rule 26)
{ "projectId": "uuid", "teamId": "uuid", "name": "EduSpark", "domainIds": [4], "semesterId": "uuid", "courseCode": "EXE101" }
// ProjectChanged – sửa nhỏ (EDITED) hoặc đổi đề tài đã duyệt (PIVOT)
{ "projectId": "uuid", "teamId": "uuid", "version": 2, "changeType": "PIVOT", "name": "EduSpark AI",
  "reason": "...", "changeRequestId": "uuid|null" }
// TeamLinkedToProject – nhóm EXE201 gắn với đề tài EXE101 (import mốc hoặc Admin gắn tay).
// Collaboration đặt previous_workspace_id = workspace của previousTeamId.
{ "teamId": "uuid", "projectId": "uuid", "previousTeamId": "uuid", "linkedBy": "IMPORT" }   // IMPORT | ADMIN
```

### exe.invitation

```jsonc
// InvitationSent – key = invitationId
{ "invitationId": "uuid", "teamId": "uuid", "teamName": "Nhóm A", "inviteeStudentId": "uuid",
  "invitedByStudentId": "uuid", "expiresAt": "..." }
// InvitationAccepted / InvitationDeclined / InvitationExpired
{ "invitationId": "uuid", "teamId": "uuid", "inviteeStudentId": "uuid", "leaderStudentId": "uuid" }
// JoinRequestSubmitted – key = joinRequestId. ideaCount = 0 khi hasNoIdea (rule 17)
{ "joinRequestId": "uuid", "teamId": "uuid", "studentId": "uuid", "leaderStudentId": "uuid", "expiresAt": "...",
  "ideaCount": 2, "hasNoIdea": false }
// JoinRequestAccepted / JoinRequestDeclined
{ "joinRequestId": "uuid", "teamId": "uuid", "studentId": "uuid" }
```

Với nhóm EXE201 kế thừa, "Accepted" chỉ tạo thay đổi chờ duyệt (rule 7); `MemberJoined` chỉ phát khi yêu cầu đổi nhóm được duyệt.

### exe.change-request (key = changeRequestId)

```jsonc
// ChangeRequestSubmitted
{ "changeRequestId": "uuid", "teamId": "uuid", "roundId": "uuid", "changedMemberCount": 2, "changedPercent": 33.33,
  "includesProjectChange": true }
// ChangeRequestApproved / ChangeRequestRejected
{ "changeRequestId": "uuid", "teamId": "uuid", "memberStudentIds": ["uuid"], "lecturerId": "uuid|null",
  "reviewNote": "..." }
```

### exe.assignment (key = teamId)

```jsonc
// TopicInterestOpened – giảng viên quan tâm đề tài (rule 19); Notification báo nhóm, Matching tăng số quan tâm
{ "interestId": "uuid", "teamId": "uuid", "projectId": "uuid", "lecturerId": "uuid", "lecturerName": "...",
  "wasPreviousInstructor": false }
// TopicInterestClosed – reason: WITHDRAWN | OTHER_LECTURER_ACCEPTED | LECTURER_ASSIGNED | THIS_LECTURER_ACCEPTED | TEAM_DISBANDED
{ "interestId": "uuid", "teamId": "uuid", "lecturerId": "uuid", "reason": "OTHER_LECTURER_ACCEPTED" }
// LecturerRequested
{ "preferenceId": "uuid", "teamId": "uuid", "teamName": "Nhóm A", "lecturerId": "uuid" }
// LecturerAccepted / LecturerDeclined
{ "preferenceId": "uuid", "teamId": "uuid", "lecturerId": "uuid", "responseNote": "..." }
// LecturerAssigned – Collaboration thêm giảng viên hướng dẫn vào workspace (rule 21)
{ "assignmentId": "uuid", "teamId": "uuid", "lecturerId": "uuid", "lecturerName": "...", "memberStudentIds": ["uuid"],
  "previousLecturerId": "uuid|null" }
// MentorAssigned – một mentor mỗi nhóm (rule 20); replacedMentorId != null khi thay mentor cũ
{ "assignmentId": "uuid", "teamId": "uuid", "mentorId": "uuid", "mentorName": "...", "mentorLecturerId": "uuid|null",
  "replacedMentorId": "uuid|null", "memberStudentIds": ["uuid"] }
// MentorUnassigned
{ "assignmentId": "uuid", "teamId": "uuid", "mentorId": "uuid", "reason": "..." }
```

Người nhận trong `exe.collab` và `exe.reminder` là `{ "personType": "STUDENT | LECTURER | MENTOR", "personId": "uuid" }`
(academic students.id / lecturers.id / mentors.id).

### exe.collab (key = workspaceId)

```jsonc
// AdvisorQuestionAsked – câu hỏi cho cố vấn (rule 22); gửi giảng viên hướng dẫn và mentor
{ "workspaceId": "uuid", "teamId": "uuid", "messageId": "uuid", "askedBy": { "personType": "STUDENT", "personId": "uuid" },
  "preview": "120 ký tự đầu", "recipients": [ { "personType": "LECTURER", "personId": "uuid" } ] }
// AdvisorQuestionAnswered
{ "workspaceId": "uuid", "teamId": "uuid", "messageId": "uuid", "answeredBy": { "personType": "MENTOR", "personId": "uuid" },
  "recipients": [ { "personType": "STUDENT", "personId": "uuid" } ] }
// UserMentioned – nhắc tên (@) trong tin nhắn
{ "workspaceId": "uuid", "channelKind": "TEAM", "messageId": "uuid", "mentioned": [ { "personType": "STUDENT", "personId": "uuid" } ] }
// TaskAssigned
{ "workspaceId": "uuid", "taskId": "uuid", "title": "...", "dueAt": "...|null", "assigneeStudentIds": ["uuid"] }
// DeliverableCreated – giảng viên hướng dẫn tạo bài nộp (rule 23)
{ "workspaceId": "uuid", "taskId": "uuid", "title": "Báo cáo tuần 3", "dueAt": "...", "memberStudentIds": ["uuid"] }
// DeliverableSubmitted
{ "workspaceId": "uuid", "taskId": "uuid", "submissionId": "uuid", "fileVersionId": "uuid", "lecturerId": "uuid" }
// DeliverableReviewed – reviewStatus: ACCEPTED | NEEDS_REVISION
{ "workspaceId": "uuid", "taskId": "uuid", "submissionId": "uuid", "reviewStatus": "NEEDS_REVISION", "comment": "...",
  "memberStudentIds": ["uuid"] }
// FileUploaded – chỉ dùng để báo cố vấn khi có phiên bản mới của file đã ghim / đã nộp (không báo mọi file)
{ "workspaceId": "uuid", "fileId": "uuid", "version": 3, "name": "bao-cao.pdf", "uploadedByStudentId": "uuid" }
// MeetingProposed / MeetingScheduled / MeetingCancelled (rule 25)
{ "workspaceId": "uuid", "meetingId": "uuid", "title": "...", "isMentorSession": true,
  "slots": [ { "slotId": "uuid", "startsAt": "...", "durationMinutes": 60 } ],
  "confirmedSlotId": "uuid|null", "cancelReason": "...|null",
  "invitees": [ { "personType": "STUDENT", "personId": "uuid" } ] }
```

### exe.reminder (Job, key = workspaceId)

```jsonc
// MeetingReminder – offsetMinutes: 1440 | 60, mỗi mốc gửi một lần
{ "workspaceId": "uuid", "meetingId": "uuid", "title": "...", "startsAt": "...", "location": "...", "offsetMinutes": 60,
  "invitees": [ { "personType": "MENTOR", "personId": "uuid" } ] }
// TaskDueSoon – hạn trong 24 giờ
{ "workspaceId": "uuid", "taskId": "uuid", "title": "...", "dueAt": "...", "assigneeStudentIds": ["uuid"] }
// DeliverableOverdue – gửi nhóm và giảng viên hướng dẫn
{ "workspaceId": "uuid", "taskId": "uuid", "title": "...", "dueAt": "...", "memberStudentIds": ["uuid"], "lecturerId": "uuid" }
```
