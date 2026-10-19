# PROJECT CONTEXT – EXE Team Matching System

> Context file for AI coding assistants (Claude Code, Cursor, Copilot…). Read this before generating code.
> Course: PRN232 – Final Assignment, FPT University, semester FA26.
> Rename to `CLAUDE.md` (Claude Code) or `AGENTS.md` / `.cursorrules` (Cursor) if your tool expects that name.
> **Updated 2026-10-19 after lecturer feedback** (section 13; second pass the same day: one mentor per team with per-mentor capacity, mentoring window from week 6, projects tracked across EXE101 → EXE201): web app for every role, Mentor actor, team workspace (messages, tasks, files, meetings), topic recommendation for lecturers, simplified profile with 5 skill ratings, ideas in join requests, Discover locked after team formation. Figma still shows the earlier palette and screens; code follows this file and `DESIGN.md`.

---

## 1. What the system does

FPT University manages team formation for the courses **EXE101** and **EXE201** entirely in a shared Google Sheets file ("Group List"). This system replaces that file and, once teams are formed, gives each team a **workspace** to work with its lecturer and mentor for the rest of the semester.

- **EXE101**: students form new teams. There is **no change request** in EXE101.
- **EXE201**: teams are inherited from EXE101 (baseline imported by the EXE Department); changing members of an inherited team requires a **change request** approved by the EXE Department.
- **Both courses**: lecturers get topic recommendations and can show interest; the team chooses a preferred lecturer, the lecturer confirms or declines, and the EXE Department makes the final assignment. The EXE Department attaches **one mentor** (external expert or university lecturer) to each team, usually from week 6.
- **Across courses**: a team's **project** (đề tài) is the stable thing that continues from EXE101 to EXE201, even when members change (rule 26).
- **During the semester**: the team, its lecturer and its mentor message each other, track tasks and deliverables, share project files and schedule meetings in the team workspace.
- Scale reference (FA26 data): ~220 teams, ~1,250 students per semester.

Problems being solved: unvalidated manual input, approvals written as free-text notes, no change history, students without a team grouped by hand after the deadline, personal data exposed in a public sheet, and – after teams are formed – communication, files and deadlines scattered across Zalo, email and personal drives.

### 1.1 Semester phases

The client UI follows the phase of the student's course, not a fixed menu.

| Phase | When | What students see |
|---|---|---|
| **Team formation** | A team-formation round is open | Discover (teams, students looking for teams), invitations, join requests, team creation |
| **Semester running** | After the team-formation round closes (its deadline is set by Admin when the round opens) | Discover is hidden (rule 18); the team workspace takes its place. EXE201 change-request rounds still work through *My team* |
| **Mentoring window** | From the mentoring start week of the semester (default week 6, set by Admin) | Mentor appears in the workspace; meetings with the mentor are counted (rule 25) |

## 2. Actors (roles)

| Role | Code | What they do |
|---|---|---|
| Student | `Student` | Completes a short profile (5 skill ratings + description), creates/joins teams, accepts invitations, works in the team workspace. A **Leader** (one per team) invites members, reviews join requests and the ideas attached to them, registers the idea/domain, submits change requests, chooses the preferred lecturer among interested lecturers. |
| Lecturer (instructor) | `Lecturer` | The team's **instructor** (giảng viên hướng dẫn). Receives topic recommendations and marks interest, accepts/declines mentoring requests, and – once officially assigned – messages the team, creates deliverables, reviews files and schedules meetings. A lecturer can also be the mentor of other teams (rule 20). The lecturer of the FAP class is **not** modelled: students follow that lecturer on FAP. |
| Mentor | `Mentor` | A separate actor: an external expert or a university lecturer who works with a team for a short window – usually **from week 6, for 2–3 meetings**. **Created and assigned only by the EXE Department**; one mentor per team. Sees the same workspace screens as the lecturer: reads project files, comments on tasks, messages the team, schedules meetings. Cannot change membership, approve requests or create deliverables. |
| EXE Department | `Admin` | Imports the student list of the semester, opens/closes registration rounds, approves/rejects change requests, assigns lecturers (final decision), creates mentors and assigns them to teams. EXE Department and Academic Affairs are treated as **one** role. |

Clients (both call the same API Gateway):
- **Web App (React)** – one app with four role shells: Admin (W screens), Student, Lecturer and Mentor. The Student / Lecturer / Mentor shells use the **same screen IDs as mobile** with a desktop layout (`DESIGN.md` §8.2).
- **Mobile App (Flutter)** – Student, Lecturer and Mentor.

## 3. Business rules (MUST be enforced in code)

1. A team has **4 to 6 members** and exactly **one Leader** who is a member.
2. A team must have **at least 2 different narrow specializations** (e.g. Software Engineering + Graphic Design). Enforced as a **hard block when the team is finalized**.
3. Members do **not** need to be in the same class.
4. A student belongs to **at most one team** per semester and course (EXE101 or EXE201). Every registration round and every team carries its `Course`.
5. Only accounts **pre-created from the imported lists** can log in: students and lecturers from the semester import, mentors created by Admin (email allow-list).
6. Team creation, invitations and member changes are allowed **only while a registration round is open**; the system locks automatically at the deadline.
7. **Change requests exist only in EXE201.** An inherited EXE201 team starts with the members of its EXE101 baseline. Any difference from the baseline (member added or removed) requires a **change request with a reason**, approved by Admin. **No limit** on the number/percentage of changed members. The system computes the count and percentage of changed members.
   - In an inherited team, invitations, join requests, leaving and removing members do **not** change membership immediately: once agreed by both sides they become **pending changes**, are bundled into the change request, and take effect only when Admin approves it. If the request is rejected, the team stays as it was.
   - EXE201 students without an EXE101 team (and all EXE101 students) form teams freely; membership changes immediately on acceptance.
8. Invitations / join requests **expire after 72 hours** (configurable – assumption).
9. Lecturer assignment (**EXE101 and EXE201**): (a) the Leader sends a mentoring request to a preferred lecturer – usually one who showed interest (rule 19); (b) the lecturer **accepts or declines** (a declined team may request another lecturer; a lecturer at full capacity cannot accept); (c) Admin makes the **final assignment**. The team's preference and the lecturer's acceptance are advisory; Admin decides and may assign a different lecturer.
10. Every team change is written to an **audit log** (who, when, what).
11. Student ID format: 2 letters + 6 digits (e.g. `SE184118`). Phone numbers (when present in the import) are stored as **string**. Contact data (email, phone) comes **only from the imported list** and is visible only to teammates, the assigned lecturer, assigned mentors and Admin.
12. Specialization and domain are chosen from **catalogs**, never free text.
13. **Finalization.** Leaders do not finalize. When a round closes, the system finalizes every team that satisfies rules 1–2 (`Active` → `Finalized`). Teams that do not satisfy them become `NeedsReview`. Admin can also finalize or reopen a team manually.
14. **After the deadline.** Students without a team are first used to fill `NeedsReview` teams (missing members or missing second specialization), then the rest are grouped into new teams. Anything still invalid after that stays `NeedsReview` for Admin to fix by hand (add/move members) before finalizing.
15. Team status: `Draft` (created, fewer than 4 members) → `Active` → `Finalized`, or `NeedsReview` after the deadline.
16. **Profile = skills + description only.** Five fixed skill categories, each self-rated with an integer **1–5**: `Technology`, `UiUxDesign`, `Management`, `Marketing`, `CommunicationPlanning`. Plus a free description (≤ 500 characters). The profile has **no contact fields** (no phone, no Zalo): contact data comes from the import (rule 11) and day-to-day contact happens in workspace messages. First-time onboarding is one screen: confirm the imported data (read-only) + skills + description.
17. **Ideas in a join request.** When asking to join a team, the student either adds **1–3 ideas** (title ≤ 80, description ≤ 300 characters each) or ticks **"I have no idea yet"** – one of the two is required. Ideas are visible only to the team's members while reviewing the request and are kept with the request.
18. **Discover is locked after team formation.** "The semester has started" means the team-formation round of the course has closed (confirmed 2026-10-19; Admin sets the deadline when opening the round). Team lists, seeking posts, join requests and team creation are available only while a team-formation round is open for the student's course. After it closes, clients hide the Discover entry point and the API returns `409 round-closed`; deep links show a reminder dialog instead of the screen. Replacements in an EXE201 change-request round go through *My team* (suggestions, search by student ID), not Discover.
19. **Topic recommendation and lecturer interest.**
    - Matching recommends to each lecturer the teams whose domains match the lecturer's expertise domains and that have no accepted or assigned lecturer yet.
    - A lecturer may mark **interest** in a team's topic. Interest is **non-binding** and does **not** use capacity; only accepting a mentoring request does.
    - Several lecturers may be interested in the same topic. Each lecturer sees **how many** other lecturers are interested (not who). The team sees **who** is interested and chooses whom to send the mentoring request to. There is no first-come-first-served race.
    - When a lecturer of a team is accepted or officially assigned, the other open interests in that team **close automatically** and those lecturers are notified ("the team chose another lecturer").
    - A lecturer can hold at most **remaining capacity + 2** open interests (configurable) to avoid hoarding topics.
    - Admin sees all interests when assigning (W19).
20. **Mentors.**
    - Admin creates mentor profiles: name, organization, email, expertise domains, internal (university lecturer) or external, and **maximum number of teams set by Admin per mentor** (it differs between mentors).
    - The mentor states a **preferred number of teams** (≤ the maximum) in their profile. When assigning, the system warns above the preferred number and blocks above the maximum.
    - **A team has at most one mentor.** Assigning a new mentor replaces the old one (with confirmation); the old mentor loses workspace access immediately, history stays.
    - Mentoring has a **window**: the semester's mentoring start week (default week 6) and an expected number of sessions (default 3, configurable). Before the window the team sees "Mentor sẽ được gán từ tuần 6"; Admin sees how many teams still have no mentor.
    - A university lecturer can be both the instructor of some teams and the mentor of others. They keep **one account with two roles** (`Lecturer`, `Mentor`); their mentor teams appear inside the lecturer shell with a "Mentor" label, so there is no role switching. External mentors use the Mentor shell.
21. **Workspace access.** Every team has one workspace, created with the team. Members get access while they are members; the instructor when **officially assigned** (not on acceptance alone); the mentor while assigned; Admin read-only. An EXE201 workspace also shows the EXE101 workspace of the same project as a read-only archive (rule 26). Membership changes are synchronized from Team events.
22. **Messages.** Each workspace has two channels: **Team** (members only) and **Advisors** (members + instructor + mentor). Messages ≤ 2,000 characters; attachments are links to workspace files. A member can mark a message in Advisors as **a question for advisors**; it stays in the lecturer's/mentor's *Needs reply* list until an advisor replies in its thread. Unread counts per channel.
23. **Tasks and deliverables.** Tasks have a title, assignees (members), due date and status `ToDo` → `Doing` → `Done`. Members create and edit team tasks. The instructor creates **deliverables** (a task with a required file and due date) for the team; the team submits a file version; the lecturer marks it `Accepted` or `NeedsRevision` with a comment. The mentor can comment but not create deliverables.
24. **Files.** Folders and files per workspace; max **25 MB** per file; allowed types pdf, docx, pptx, xlsx, png, jpg, zip. Uploading a file with an existing name in the same folder creates a **new version**; earlier versions stay downloadable. Members upload and delete (soft delete, uploader or leader); the instructor and the mentor read and comment only.
25. **Meetings.** The leader, the instructor or the mentor proposes a meeting with **1–3 time slots** (or one fixed slot), place (room or online link) and agenda. Invitees answer each slot (`Yes` / `No`); the organizer confirms one slot → the meeting is scheduled. Reminders 24 h and 1 h before. After the meeting the organizer records minutes; action items can be turned into tasks in one step. Meetings organized by the mentor are **mentoring sessions**: the workspace shows "Buổi mentor 2/3" against the expected number (rule 20).
26. **Projects across courses (đề tài).** The system tracks work by **project**, not only by team:
    - A `Project` is created when an EXE101 team registers its idea (name, description, domains). It keeps a history of versions (`ProjectVersion`: what changed, why, who, when).
    - A team belongs to one course; its `ProjectId` links it to the project. The **EXE101 baseline import** for EXE201 links each inherited team to the project of its EXE101 team (matched by team code; unmatched rows are listed for Admin to link by hand). EXE201 teams without an EXE101 team create a new project.
    - **Changing the project in EXE201** (pivot or new idea) is an item of the change request (`ProjectChange`, with reason) approved by Admin together with member changes; on approval a new `ProjectVersion` is written. Small edits (description typo) do not need approval.
    - The EXE201 team sees "Tiếp tục từ EXE101 · SP26" with the project timeline; its workspace shows the EXE101 files, finished tasks and minutes read-only.
    - For EXE201 topic recommendations, the **instructor of the same project in EXE101** is shown first to the team ("Đã hướng dẫn ở EXE101"), and the project shows up first in that lecturer's L07.
    - Admin manages projects across semesters (W29): lineage EXE101 team → EXE201 team, percentage of members kept, project changes, instructor and mentor per semester.

## 4. Architecture overview

Microservices, ASP.NET Core 8. Clients only talk to the **API Gateway**. Each service owns its database; **no service reads another service's database**.

### C0 – System Context
- Users (Student, Lecturer, Mentor, EXE Department) → **EXE Team Matching System** over HTTPS, from the web app or the mobile app.
- System → **Google Identity** (OAuth 2.0 / OpenID Connect) to authenticate users.
- System → **Email Service** (SMTP) to send notifications.
- System → **Object storage** (MinIO, S3-compatible, runs in Docker) for workspace files.
- No integration with FAP (FPT Academic Portal has no API), no Google Sheets sync, no calendar integration (meeting links are entered by hand).

### C1 – Containers and communication

```
Student, Lecturer, Mentor --HTTPS--> Mobile App (Flutter) --REST + WebSocket--> API Gateway (YARP)
Student, Lecturer, Mentor, EXE Department --HTTPS--> Web App (React) --REST + WebSocket--> API Gateway (YARP)
API Gateway --REST/HTTP--> Identity | Team | Academic | Notification | Collaboration
API Gateway --WebSocket (SignalR)--> Collaboration Service (/hubs/collab)
Job Service      --REST/HTTP (internal, service token)--> Team Service, Collaboration Service
Team Service     --REST/HTTP--> Academic Service
Team Service     --gRPC-------> Matching Service
Job Service      --gRPC-------> Matching Service
Team, Academic, Job, Collaboration  --Publish--> Kafka
Kafka --Consume--> Notification, Matching, Identity, Collaboration
Academic Service, Matching Service --> Redis (cache)
Collaboration Service --S3 API--> MinIO (files)
Identity -> Identity DB | Team -> Team DB | Academic -> Academic DB | Job -> Job DB | Notification -> Notification DB | Collaboration -> Collab DB  (PostgreSQL)
Identity Service     --OAuth 2.0--> Google Identity
Notification Service --SMTP------> Email Service
```

Diagram source: `docs/c4-architecture/c1.mmd` (Mermaid flowchart, rendered to `c1.png`). Re-rendered on 2026-10-19 with Collaboration Service, MinIO, Mentor and the web app for every role.

## 5. Services

### 5.1 API Gateway – YARP
- Routes requests, validates JWT, CORS, aggregates Swagger of downstream services.
- Exposes Identity, Team, Academic, Collaboration and the read API of Notification (`/api/notifications`). Job and Matching are **internal only**.
- Proxies the SignalR hub `/hubs/collab` (WebSocket, falls back to long polling); the JWT is passed as `access_token` query parameter on the hub route only.

### 5.2 Identity Service – REST API
- Google login: receives Google ID token, looks up the email in its own `UserAccount` table, then **issues the system's own JWT** (access token ~15 min) + refresh token (~7 days). Refresh, logout/revoke. An email with no active account is rejected (mobile screen A04).
- **Login allow-list (rule 5).** Identity never calls Academic at login. It consumes `StudentImported`, `LecturerRegistered` and `MentorRegistered` (topic `exe.student`) and pre-creates a `UserAccount` (role `Student` / `Lecturer` / `Mentor`, status `PENDING`, becomes `ACTIVE` at the first Google login) for each email; the event is processed idempotently (same email → update, not duplicate). The first Admin account is seeded.
- Roles: `Student`, `Lecturer`, `Mentor`, `Admin`. An account can hold `Lecturer` and `Mentor` together (JWT claim `roles` is a list). Leader is checked as a policy at the Services layer, not a role.
- DB (`Identity DB`): `UserAccount` (email, role, status, semester), `Role`, `RefreshToken` (token, expiry, revoked), `ProcessedEvent` (idempotency).
- No Redis (JWT is validated by signature; caching login data risks keeping locked accounts active).

### 5.3 Academic Service – REST API
- Semesters and registration rounds (course: EXE101 / EXE201; type: team formation / change request; open time, deadline, status). Change-request rounds exist only for EXE201. Exposes the **current phase** of each course (section 1.1) for the clients.
- **Import student list** of the semester from Excel (ClosedXML) – name, student ID, email, specialization, class, phone (optional).
- Catalogs: class codes, specializations, research domains (D01–D18).
- Student profile: 5 skill ratings + description (rule 16). Lecturer profile: **capacity**, accepting-new-teams flag, **expertise domains** (used by topic recommendation). **Mentor profiles** created by Admin: max teams (Admin), preferred teams (mentor), internal / external, domains (rule 20). Semester settings include the **mentoring start week** and expected mentoring sessions.
- Answers Team Service (HTTP): "is the round open?", "does this student exist / is in this semester?".
- Redis: caches catalogs and active round status; invalidate on change.
- Publishes: `StudentImported`, `StudentProfileUpdated`, `LecturerRegistered`, `LecturerProfileUpdated`, `MentorRegistered`, `RoundOpened`, `RoundClosed`.
- DB (`Academic DB`): `Semester`, `RegistrationRound`, `Student`, `StudentSkill`, `Lecturer`, `LecturerDomain`, `Mentor`, `MentorDomain`, `ClassCode`, `Specialization`, `Domain`.

### 5.4 Team Service – REST API (core business)
- CRUD teams, members, leader. Each team has a `Course` and a status (`Draft`, `Active`, `NeedsReview`, `Finalized`).
- **Finalization** (rule 13): internal endpoint `POST /internal/teams/finalize-round` called by Job Service after the round closes; validates rules 1–2 via Matching `ValidateComposition`, sets `Finalized` or `NeedsReview`. Admin endpoints to finalize / reopen one team.
- **EXE101 baseline import** (EXE201 only, Admin): `POST /api/teams/baseline-import` takes an Excel file (team name, project, leader, member student IDs), creates the inherited EXE201 teams and stores the baseline in `TeamBaselineMember`. This is the reference for every change request.
- Invitations and join requests (send/accept/decline/expire). Join requests carry ideas or a "no idea" flag (rule 17).
- Recruitment posts (team looking for members) and seeking posts (student looking for team). Discover endpoints check the phase (rule 18).
- **Projects** (rule 26): `Project`, `ProjectVersion`; baseline import links inherited teams to their EXE101 project; project lineage and history APIs for students, lecturers and Admin.
- Change requests (EXE201 only) + Admin approval. Pending changes of an inherited team are stored as `ChangeRequestItem` (add / remove a student, or change the project) and applied only on approval (rules 7, 26).
- Lecturer flow for both courses (rules 9, 19): `TopicInterest` holds lecturers' interest in a team (`Open`, `Closed`); `LecturerPreference` holds the team's request and its status (`Pending`, `Accepted`, `Declined`); `LecturerAssignment` holds Admin's final decision.
- Mentor assignment (rule 20): `MentorAssignment` (team – unique while active, mentor, assigned by, time, ended at); checks the mentor's max and preferred number of teams.
- Search / filter / sort / paginate teams (e.g. `GET /api/teams?domain=D04&page=1&pageSize=20&sort=name`).
- Writes events through a **Transactional Outbox** (event row saved in the same transaction, background dispatcher publishes to Kafka).
- DB (`Team DB`): `Project`, `ProjectVersion`, `Team` (with `ProjectId`, `PreviousTeamId`), `TeamMember`, `TeamDomain`, `Invitation`, `JoinRequest`, `JoinRequestIdea`, `RecruitmentPost`, `SeekingPost`, `TeamBaselineMember`, `ChangeRequest`, `ChangeRequestItem`, `TopicInterest`, `LecturerPreference`, `LecturerAssignment`, `MentorAssignment`, `AuditLog`, `OutboxMessage`.

### 5.5 Matching Service – gRPC only ("matchmaker")
Only computes suggestions; never creates teams or writes business data.
- `SuggestMembers` – students without a team who fill missing specializations **and the weakest skill categories** of a team (team skill profile = max rating per category across members).
- `SuggestTeams` – recruiting teams that need the student's specialization or strongest skills.
- `SuggestTopicsForLecturer` – teams whose domains match the lecturer's expertise domains and that have no accepted/assigned lecturer, ranked by domain overlap, then by fewest interested lecturers (spreads interest across topics).
- `ValidateComposition` – checks 4–6 members and ≥ 2 specializations.
- `PlanAutoAssignment` – after the deadline, first proposes unmatched students to fill `NeedsReview` teams, then splits the remaining students into new valid teams (rule 14).
- Keeps a **read model in Redis** (student specialization, skill ratings, has-team flag, team gaps and domains, lecturer expertise, interest counts), updated by consuming Kafka events (`StudentImported`, `StudentProfileUpdated`, `LecturerRegistered`, `LecturerProfileUpdated`, `TeamCreated`, `MemberJoined`, `MemberLeft`, `TopicInterestOpened`, `TopicInterestClosed`, `LecturerAccepted`, `LecturerAssigned`). No SQL database.

```proto
syntax = "proto3";
package exe.matching.v1;

service MatchingService {
  rpc SuggestMembers (SuggestMembersRequest) returns (SuggestMembersResponse);
  rpc SuggestTeams (SuggestTeamsRequest) returns (SuggestTeamsResponse);
  rpc SuggestTopicsForLecturer (SuggestTopicsForLecturerRequest) returns (SuggestTopicsForLecturerResponse);
  rpc ValidateComposition (ValidateCompositionRequest) returns (ValidateCompositionResponse);
  rpc PlanAutoAssignment (PlanAutoAssignmentRequest) returns (PlanAutoAssignmentResponse);
}
```

### 5.6 Job Service – Hangfire
| Job | Trigger | Action |
|---|---|---|
| `DeadlineReminderJob` | Daily 08:00 while a round is open | 72h and 24h before deadline publish `DeadlineApproaching` (students without team, teams < 4) |
| `RoundAutoCloseJob` | Delayed job at deadline | Close round, publish `RoundClosed`, then call Team Service `finalize-round` (internal HTTP) |
| `AutoAssignUnmatchedJob` | After finalization of the round | gRPC `PlanAutoAssignment`, then call Team Service (internal HTTP, service token) to add students to incomplete teams and create new teams. Admin reviews and adjusts the result on the web app. |
| `InvitationExpiryJob` | Hourly | Mark invitations/join requests > 72h as expired, publish `InvitationExpired` |
| `MeetingReminderJob` | Every 15 min | Ask Collaboration (internal HTTP) for meetings starting in 24 h / 1 h, publish `MeetingReminder` once per meeting and offset |
| `TaskDueReminderJob` | Daily 08:00 | Ask Collaboration for tasks/deliverables due in 24 h or overdue, publish `TaskDueSoon` / `DeliverableOverdue` |
- DB (`Job DB`): Hangfire storage only (jobs, schedules, state, retries). No business data.
- Hangfire Dashboard at `/hangfire` for the demo.

### 5.7 Notification Service – REST API + Worker (BackgroundService)
- Consumes Kafka events, sends email via SMTP (MailKit), stores in-app notifications.
- Idempotent: stores processed event IDs to avoid duplicate emails.
- Small read API for the in-app notification list, exposed through the Gateway and scoped to the caller's JWT: `GET /api/notifications?page=&pageSize=&unreadOnly=`, `GET /api/notifications/unread-count`, `PATCH /api/notifications/{id}/read`, `PATCH /api/notifications/read-all`. Clients poll (pull to refresh); chat messages are **not** notifications – they arrive through the Collaboration hub.
- Notifies on collaboration events only when they need attention: advisor question, task assigned, deliverable created / reviewed / overdue, meeting proposed / scheduled / reminder / cancelled, mention. Plain messages do not create notifications or emails.
- DB (`Notification DB`): `Notification` (recipient, content, type, read flag, time), `EmailLog`, `ProcessedEvent`.

### 5.8 Collaboration Service – REST API + SignalR hub (team workspace)
- One `Workspace` per team (rule 21), with `PreviousWorkspaceId` for the EXE101 workspace of the same project (read-only archive, rule 26). Keeps a local `WorkspaceMember` table (user, role in workspace: `Member`, `Leader`, `Lecturer`, `Mentor`, `Admin`) built from Kafka events: `TeamCreated`, `MemberJoined`, `MemberLeft`, `LeaderChanged`, `LecturerAssigned`, `MentorAssigned`, `MentorUnassigned`. Never calls Team to check access.
- **Messages** (rule 22): channels `Team` and `Advisors`, threads (reply to a message), advisor questions with `Open` / `Answered`, read markers. SignalR hub `/hubs/collab`: groups per channel; events `messageCreated`, `messageUpdated`, `readUpdated`, `typing`. REST for history (cursor pagination) and sending (the hub only pushes).
- **Tasks and deliverables** (rule 23): board per workspace, assignees, due dates, comments; deliverables with submissions (file version) and review status.
- **Files** (rule 24): folders, files, versions; binary content in MinIO bucket `exe-files` (key `workspace/{id}/{fileId}/{version}`); downloads through short-lived pre-signed URLs; comments.
- **Meetings** (rule 25): proposals with slots, responses, confirmation, cancellation, minutes, action items → tasks.
- Lecturer / mentor overview endpoints across their teams: needs-reply questions, overdue deliverables, upcoming meetings.
- Internal endpoints for Job Service: `GET /internal/meetings/upcoming`, `GET /internal/tasks/due`.
- Publishes through Outbox to `exe.collab` (section 6).
- DB (`Collab DB`): `Workspace`, `WorkspaceMember`, `Channel`, `Message`, `MessageRead`, `AdvisorQuestion`, `TaskItem`, `TaskAssignee`, `TaskComment`, `Deliverable`, `DeliverableSubmission`, `Folder`, `ProjectFile`, `FileVersion`, `FileComment`, `Meeting`, `MeetingSlot`, `MeetingResponse`, `MeetingMinutes`, `OutboxMessage`, `ProcessedEvent`.

## 6. Kafka topics and events

| Topic | Events | Producer | Consumers |
|---|---|---|---|
| `exe.team` | TeamCreated, MemberJoined, MemberLeft, LeaderChanged, TeamFinalized, TeamNeedsReview, ProjectCreated, ProjectChanged, TeamLinkedToProject | Team | Notification, Matching, Collaboration |
| `exe.invitation` | InvitationSent, InvitationAccepted, InvitationDeclined, InvitationExpired, JoinRequestSubmitted, JoinRequestAccepted, JoinRequestDeclined | Team, Job | Notification |
| `exe.change-request` | ChangeRequestSubmitted, ChangeRequestApproved, ChangeRequestRejected | Team | Notification |
| `exe.registration` | RoundOpened, DeadlineApproaching, RoundClosed | Academic, Job | Notification (Job listens to RoundClosed internally via schedule) |
| `exe.student` | StudentImported, StudentProfileUpdated, LecturerRegistered, LecturerProfileUpdated, MentorRegistered | Academic | Matching (read model), Identity (pre-created accounts), Notification (recipient contacts) |
| `exe.assignment` | TopicInterestOpened, TopicInterestClosed, LecturerRequested, LecturerAccepted, LecturerDeclined, LecturerAssigned, MentorAssigned, MentorUnassigned | Team | Notification, Matching, Collaboration |
| `exe.collab` | AdvisorQuestionAsked, AdvisorQuestionAnswered, UserMentioned, TaskAssigned, DeliverableCreated, DeliverableSubmitted, DeliverableReviewed, FileUploaded, MeetingProposed, MeetingScheduled, MeetingCancelled | Collaboration | Notification |
| `exe.reminder` | MeetingReminder, TaskDueSoon, DeliverableOverdue | Job | Notification |

Event envelope: `eventId` (GUID, for idempotency), `eventType`, `occurredAt` (UTC), `correlationId`, `payload`.
Consumers retry, then send to a dead-letter topic `<topic>.dlq`.

## 7. Tech stack

- C# 12, **ASP.NET Core 8**, **EF Core 8** (code-first, migrations, seed data) with **PostgreSQL** (Npgsql).
- gRPC (`Grpc.AspNetCore`), Apache Kafka (KRaft, `Confluent.Kafka`), Redis 7 (`StackExchange.Redis`), Hangfire (PostgreSQL storage), YARP.
- **SignalR** (ASP.NET Core) for workspace messages; **MinIO** (`Minio` .NET SDK or `AWSSDK.S3`) for files.
- Auth: Google OAuth 2.0 / OpenID Connect + JWT Bearer.
- FluentValidation, Serilog (structured logs with CorrelationId through Gateway, REST, gRPC, Kafka), global exception middleware returning **ProblemDetails (RFC 7807)**, gRPC interceptor mapping exceptions to status codes.
- Swagger/OpenAPI per REST service, aggregated at the Gateway.
- Docker + Docker Compose.
- Clients: **Flutter** mobile app (Material 3) for Student, Lecturer and Mentor; **React** web app for all four roles (`@microsoft/signalr` client on both).

## 8. Code conventions

Every REST service uses the layered structure required by the assignment: **API – Services – Repository**.

```
src/
  BuildingBlocks/        # shared: JWT, logging, exception middleware, Kafka, Outbox, event contracts
  ApiGateway/
  Services/
    Identity/       Identity.Api | Identity.Services | Identity.Repositories | Identity.Domain
    Academic/       Academic.Api | Academic.Services | Academic.Repositories | Academic.Domain
    Team/           Team.Api     | Team.Services     | Team.Repositories     | Team.Domain
    Collaboration/  Collab.Api   | Collab.Services   | Collab.Repositories   | Collab.Domain
                    (modules: Messages, Tasks, Files, Meetings – one folder each, like Team)
    Matching/       Matching.Grpc | Matching.Services
    Notification/   Notification.Worker
    Jobs/           Jobs.Host (Hangfire)
  Protos/                # shared *.proto
deploy/
  docker-compose.yml
  docker-compose.override.yml
```

- **Api**: controllers, DTOs, validation, mapping, Swagger, SignalR hubs. No business logic.
- **Services**: business rules, gRPC/HTTP calls, write Outbox.
- **Repositories**: DbContext, repositories, migrations, paged/filtered/sorted queries.
- Use built-in DI; configuration via `appsettings.{Environment}.json` + environment variables; never commit secrets.
- REST naming: plural nouns, kebab-case routes (`/api/teams/{id}/members`, `/api/change-requests`, `/api/workspaces/{id}/meetings`).
- All times stored in UTC.
- Each service exposes `/health`.

## 9. Deployment (Docker Compose)

| Service | Port (host) |
|---|---|
| web (React, all roles) | 3000 |
| gateway | 8080 |
| identity-api | 5001 |
| academic-api | 5002 |
| team-api | 5003 |
| matching-grpc | 5004 (HTTP/2) |
| notification (API + worker) | 5005 |
| jobs (`/hangfire`) | 5006 |
| collab-api (REST + SignalR) | 5007 |
| postgres | 5432 (one instance, one database per service: `identity_db`, `academic_db`, `team_db`, `notification_db`, `job_db`, `collab_db`; table design in `docs/EXE_Database_Design.pdf`, SQL scripts in `deploy/db/`) |
| kafka | 9092 |
| kafka-ui | 8085 |
| redis | 6379 |
| minio | 9000 (S3 API) / 9001 (console) |
| mailpit (dev SMTP) | 1025 / 8025 |

The Flutter mobile app is not part of Compose; it points at the gateway URL. `depends_on` with `condition: service_healthy`. EF migrations auto-run in Development. Demo seed data comes from an **anonymized** Group List (no phone numbers, fake emails).

> `collab_db`, its SQL script and the `collab-api` / `minio` containers are **not created yet** (tasks BE-60, BE-61). `EXE_Database_Design.pdf` and `deploy/db/` still describe the five original databases.

## 10. PRN232 requirement mapping

| Requirement | Where |
|---|---|
| REST API, CRUD, layered architecture | Identity, Academic, Team, Collaboration |
| JWT authentication | Identity issues, Gateway + services validate (also on the SignalR hub) |
| Search, filter, sort, pagination | Team (teams), Academic (students), Collaboration (messages – cursor, files) |
| Background job | Job Service (Hangfire), Outbox dispatchers, Kafka consumers |
| Message broker: ≥ 1 producer, ≥ 1 consumer | Kafka: Team/Academic/Job/Collaboration produce, Notification/Matching/Identity/Collaboration consume |
| Independent gRPC service called from REST API | Team Service → Matching Service |
| Docker deployment | Docker Compose |
| Extra (not required) | Realtime with SignalR, object storage with MinIO |

## 11. End-to-end demo flows

1. **Create team & invite**: Leader logs in (Google → JWT) → `POST /api/teams` → Team asks Academic if round is open → Team calls Matching `SuggestMembers` (gRPC) → Leader invites → Outbox → Kafka `InvitationSent` → Notification emails the student → student accepts → `MemberJoined` → Matching updates Redis, Collaboration adds the member to the workspace.
2. **Join request with ideas**: student sends a join request with two ideas → `JoinRequestSubmitted` → leader reviews the ideas on the request screen → accepts → `MemberJoined`.
3. **Change request (EXE201 only)**: Admin imports the EXE101 baseline → inherited team exists → Leader invites a new student / marks a member as leaving (pending changes) → Leader submits the change request with a reason → `ChangeRequestSubmitted` → Admin approves → pending changes applied to members, AuditLog written, `ChangeRequestApproved` → email to members and lecturer. On rejection nothing changes and `ChangeRequestRejected` is sent.
4. **Deadline, finalization & auto-assign**: reminders at 72h/24h → round auto-closes → Discover disappears from the clients → Job calls Team `finalize-round` (valid teams `Finalized`, others `NeedsReview`) → Job calls Matching `PlanAutoAssignment` → Job calls Team to fill incomplete teams and create new ones → `MemberJoined` / `TeamCreated` → emails → Admin reviews what is still `NeedsReview` and finalizes.
5. **Topic interest → lecturer**: lecturer A and lecturer B open *Recommended topics* (Matching `SuggestTopicsForLecturer`) and both mark interest in team T (`TopicInterestOpened` ×2; each sees "1 other lecturer interested") → team T sees both, sends the mentoring request to A → A accepts (`LecturerAccepted`) → B's interest closes (`TopicInterestClosed`, B is notified) → Admin assigns A (`LecturerAssigned`) → A joins the workspace.
6. **Workspace with mentor**: Admin creates mentor M and assigns M to team T (`MentorRegistered`, `MentorAssigned`) → M logs in with Google → M proposes a meeting with 2 slots → members answer → M confirms (`MeetingScheduled`) → `MeetingReminder` 1 h before → M records minutes and turns 2 action items into tasks (`TaskAssigned`).
7. **Deliverable**: lecturer creates deliverable "Báo cáo tuần 3" → team uploads a file version and submits (`DeliverableSubmitted`) → lecturer marks `NeedsRevision` with a comment → team uploads version 2 → `Accepted`.
8. **Project continues into EXE201**: Admin imports the EXE101 baseline → inherited team T2 is linked to project P of EXE101 team T1 (`TeamLinkedToProject`) → T2's workspace shows T1's files read-only → the EXE101 instructor sees P first in L07 → T2 includes a `ProjectChange` (pivot) in its change request → Admin approves → `ProjectChanged`, new project version.
9. **In-app notifications**: any event above → Notification Service stores a `Notification` row and sends the email → clients read `GET /api/notifications` through the Gateway.

## 12. Open decisions / assumptions

- ~~Email whitelist check at login~~ – **decided 2026-10-19: option (b).** Academic publishes `StudentImported` / `LecturerRegistered` / `MentorRegistered`, Identity consumes them and pre-creates accounts (section 5.2). Login does not depend on Academic being up.
- ~~Job → Team, Mobile App and Gateway → Notification missing from C1~~ – drawn on 2026-10-19.
- **Decided 2026-10-19 (lecturer feedback):** collaboration features live in a separate **Collaboration Service** with its own database; mentors are created **only by Admin**; realtime only for messages (SignalR), everything else stays request/response + polling.
- **Assumptions added on 2026-10-12 – confirm with the team/lecturer:**
  - The EXE101 baseline of EXE201 teams comes from an **Excel import** by Admin (section 5.4), not from data of a previous semester inside the system.
  - Member changes of an inherited EXE201 team are **pending until Admin approves** the change request (rule 7).
  - Teams are **finalized automatically at the deadline**; invalid teams become `NeedsReview` and are handled by auto-assign, then by Admin (rules 13–14).
  - In-app notifications are fetched by **polling** the Notification read API.
- **Assumptions added on 2026-10-19 – confirm with the lecturer:**
  - A lecturer who is also a mentor has one account with roles `Lecturer` and `Mentor` (rule 20).
  - Limits: 25 MB per file, 3 ideas per join request, interest cap = remaining capacity + 2 – all configurable. (Mentor limits are decided: one mentor per team; max teams per mentor set by Admin.)
  - A project change in EXE201 goes through the change request and Admin approval (rule 26).
  - No virus scanning and no storage quota per team in this version.
  - Meeting links (Google Meet, Teams) are typed in by the organizer; no calendar API.
- The imported student file is assumed to come from FAP exports and to contain each student's Google account email (and phone, optional).
- Lecturer emails and expertise domains are entered by Admin on the web app (Academic Service), which publishes `LecturerRegistered` so Identity grants the `Lecturer` role; the first Admin account is seeded.
- If the team is short on people: merge Academic into Team and Notification into Job; keep Collaboration separate (it owns the realtime hub and file storage).

## 13. Lecturer feedback 2026-10-19 → where it is handled

| # | Feedback | Handled in |
|---|---|---|
| 1 | Web app also for students and lecturers | §2 clients; `DESIGN.md` §8.2; `user-flow.md` §1 |
| 2 | Communication between lecturer and students | Rule 22; §5.8 Messages |
| 3 | Files and tasks of the team and lecturer | Rules 23, 24; §5.8 |
| 4 | Brighter FPT colours (orange/blue), modern, less text | `DESIGN.md` §2, §9 |
| 5 | Ideas (1, 2, … or "no idea") in a join request | Rule 17 |
| 6 | No Discover after the semester starts | §1.1, rule 18 |
| 7 | Topic recommendation for lecturers; two lecturers like the same topic | Rule 19; §5.5 `SuggestTopicsForLecturer` |
| 8 | 5 skills with a 1–5 slider | Rule 16 |
| 9 | Profile = skills + description, no contact info, shorter onboarding | Rules 11, 16 |
| 10 | Mentor actor with lecturer-like screens (files, meetings, messages) | §2, rules 20–25 |
| 11 | Manage projects and teams from EXE101 to EXE201 | Rule 26; §5.4 Projects; demo flow 8 |
| – | Clarified 2026-10-19: semester start = team-formation round closed; 1 mentor per team; mentor capacity per mentor + preferred number; mentoring from week 6, 2–3 sessions; FAP lecturer not modelled | §1.1, §2, rules 18, 20, 25 |
