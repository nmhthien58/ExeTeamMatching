# Sự kiện Kafka (BE-02)

Nguồn: PROJECT_CONTEXT §6, thiết kế DB mục 8. Mọi thay đổi tên topic, tên sự kiện hoặc payload phải qua PR do Thiên duyệt.

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
| `exe.student` | StudentImported, StudentProfileUpdated, LecturerRegistered | Academic (outbox) | Identity (tạo sẵn tài khoản), Notification (contacts), Matching (read model) |
| `exe.registration` | RoundOpened, RoundClosed | Academic (outbox) | Notification, Job (lên lịch đóng đợt) |
| `exe.registration` | DeadlineApproaching | Job (publish trực tiếp) | Notification |
| `exe.team` | TeamCreated, MemberJoined, MemberLeft, LeaderChanged, TeamFinalized, TeamNeedsReview | Team (outbox) | Matching, Notification |
| `exe.invitation` | InvitationSent, InvitationAccepted, InvitationDeclined, InvitationExpired, JoinRequestSubmitted, JoinRequestAccepted, JoinRequestDeclined | Team (outbox) | Notification |
| `exe.change-request` | ChangeRequestSubmitted, ChangeRequestApproved, ChangeRequestRejected | Team (outbox) | Notification |
| `exe.assignment` | LecturerRequested, LecturerAccepted, LecturerDeclined, LecturerAssigned | Team (outbox) | Notification |

`InvitationExpired` do Team phát: Job gọi endpoint nội bộ `POST /internal/invitations/expire-overdue` của Team (Job không đụng database của Team).

## Payload

Kiểu: `uuid` là chuỗi uuid, thời gian là chuỗi UTC ISO 8601.

### exe.student

```jsonc
// StudentImported – mỗi sinh viên một message, key = studentId
{ "studentId": "uuid", "studentCode": "SE184118", "fullName": "Nguyễn Văn A", "email": "a@gmail.com",
  "specializationId": 1, "skills": ["Flutter"], "semesterId": "uuid", "semesterEndDate": "2026-12-26",
  "courseCode": "EXE101", "classCode": "EXE101SE01" }

// StudentProfileUpdated – key = studentId
{ "studentId": "uuid", "fullName": "Nguyễn Văn A", "email": "a@gmail.com", "specializationId": 1,
  "skills": ["Flutter", "UI"], "isSeekingTeam": true }

// LecturerRegistered – Admin thêm giảng viên, key = lecturerId
{ "lecturerId": "uuid", "fullName": "Trần Thị B", "email": "b@fe.edu.vn", "isActive": true }
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
// TeamCreated
{ "teamId": "uuid", "semesterId": "uuid", "courseCode": "EXE101", "name": "Nhóm A", "leaderStudentId": "uuid",
  "isAutoAssigned": false, "isInherited": false }
// MemberJoined
{ "teamId": "uuid", "semesterId": "uuid", "studentId": "uuid", "specializationId": 1,
  "joinSource": "INVITATION", "memberCount": 5 }
// MemberLeft
{ "teamId": "uuid", "semesterId": "uuid", "studentId": "uuid", "reason": "LEAVE", "memberCount": 4 }   // reason: LEAVE | REMOVED | CHANGE_REQUEST | ADMIN
// LeaderChanged
{ "teamId": "uuid", "oldLeaderStudentId": "uuid", "newLeaderStudentId": "uuid" }
// TeamFinalized
{ "teamId": "uuid", "semesterId": "uuid", "memberStudentIds": ["uuid"], "finalizedBy": "SYSTEM" }       // SYSTEM | ADMIN
// TeamNeedsReview
{ "teamId": "uuid", "semesterId": "uuid", "violations": ["TOO_FEW_MEMBERS"] }
```

### exe.invitation

```jsonc
// InvitationSent – key = invitationId
{ "invitationId": "uuid", "teamId": "uuid", "teamName": "Nhóm A", "inviteeStudentId": "uuid",
  "invitedByStudentId": "uuid", "expiresAt": "..." }
// InvitationAccepted / InvitationDeclined / InvitationExpired
{ "invitationId": "uuid", "teamId": "uuid", "inviteeStudentId": "uuid", "leaderStudentId": "uuid" }
// JoinRequestSubmitted – key = joinRequestId
{ "joinRequestId": "uuid", "teamId": "uuid", "studentId": "uuid", "leaderStudentId": "uuid", "expiresAt": "..." }
// JoinRequestAccepted / JoinRequestDeclined
{ "joinRequestId": "uuid", "teamId": "uuid", "studentId": "uuid" }
```

Với nhóm EXE201 kế thừa, "Accepted" chỉ tạo thay đổi chờ duyệt (rule 7); `MemberJoined` chỉ phát khi yêu cầu đổi nhóm được duyệt.

### exe.change-request (key = changeRequestId)

```jsonc
// ChangeRequestSubmitted
{ "changeRequestId": "uuid", "teamId": "uuid", "roundId": "uuid", "changedMemberCount": 2, "changedPercent": 33.33 }
// ChangeRequestApproved / ChangeRequestRejected
{ "changeRequestId": "uuid", "teamId": "uuid", "memberStudentIds": ["uuid"], "lecturerId": "uuid|null",
  "reviewNote": "..." }
```

### exe.assignment (key = teamId)

```jsonc
// LecturerRequested
{ "preferenceId": "uuid", "teamId": "uuid", "teamName": "Nhóm A", "lecturerId": "uuid" }
// LecturerAccepted / LecturerDeclined
{ "preferenceId": "uuid", "teamId": "uuid", "lecturerId": "uuid", "responseNote": "..." }
// LecturerAssigned
{ "assignmentId": "uuid", "teamId": "uuid", "lecturerId": "uuid", "memberStudentIds": ["uuid"] }
```
