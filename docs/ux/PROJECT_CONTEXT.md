# PROJECT CONTEXT – EXE Team Matching System

> Context file for AI coding assistants (Claude Code, Cursor, Copilot…). Read this before generating code.
> Course: PRN232 – Final Assignment, FPT University, semester FA26.
> Rename to `CLAUDE.md` (Claude Code) or `AGENTS.md` / `.cursorrules` (Cursor) if your tool expects that name.

---

## 1. What the system does

FPT University manages team formation for the courses **EXE101** and **EXE201** entirely in a shared Google Sheets file ("Group List"). This system replaces that file.

- **EXE101**: students form new teams. There is **no change request** in EXE101.
- **EXE201**: teams are inherited from EXE101 (baseline imported by the EXE Department); changing members of an inherited team requires a **change request** approved by the EXE Department.
- **Both courses**: the team chooses a preferred lecturer, the lecturer confirms or declines, and the EXE Department makes the final assignment.
- Scale reference (FA26 data): ~220 teams, ~1,250 students per semester.

Problems being solved: unvalidated manual input, approvals written as free-text notes, no change history, students without a team grouped by hand after the deadline, personal data exposed in a public sheet.

## 2. Actors (roles)

| Role | Code | What they do |
|---|---|---|
| Student | `Student` | Completes profile, creates/joins teams, accepts invitations. A **Leader** (one per team) invites members, registers the idea/domain, submits change requests, chooses preferred lecturer. |
| Lecturer | `Lecturer` | Confirms mentoring a team, views assigned teams. |
| EXE Department | `Admin` | Imports the student list of the semester, opens/closes registration rounds, approves/rejects change requests, assigns lecturers (final decision). EXE Department and Academic Affairs are treated as **one** role. |

Clients: **Student** and **Lecturer** use the **Mobile App (Flutter)**; **EXE Department** uses the **Web App (React)**. Both clients call the same API Gateway.

## 3. Business rules (MUST be enforced in code)

1. A team has **4 to 6 members** and exactly **one Leader** who is a member.
2. A team must have **at least 2 different narrow specializations** (e.g. Software Engineering + Graphic Design). Enforced as a **hard block when the team is finalized**.
3. Members do **not** need to be in the same class.
4. A student belongs to **at most one team** per semester and course (EXE101 or EXE201). Every registration round and every team carries its `Course`.
5. Only students **in the imported semester list** can log in (email whitelist).
6. Team creation, invitations and member changes are allowed **only while a registration round is open**; the system locks automatically at the deadline.
7. **Change requests exist only in EXE201.** An inherited EXE201 team starts with the members of its EXE101 baseline. Any difference from the baseline (member added or removed) requires a **change request with a reason**, approved by Admin. **No limit** on the number/percentage of changed members. The system computes the count and percentage of changed members.
   - In an inherited team, invitations, join requests, leaving and removing members do **not** change membership immediately: once agreed by both sides they become **pending changes**, are bundled into the change request, and take effect only when Admin approves it. If the request is rejected, the team stays as it was.
   - EXE201 students without an EXE101 team (and all EXE101 students) form teams freely; membership changes immediately on acceptance.
8. Invitations / join requests **expire after 72 hours** (configurable – assumption).
9. Lecturer assignment (**EXE101 and EXE201**): (a) the Leader sends a mentoring request to a preferred lecturer; (b) the lecturer **accepts or declines** (a declined team may request another lecturer; a lecturer at full capacity cannot accept); (c) Admin makes the **final assignment**. The team's preference and the lecturer's acceptance are advisory; Admin decides and may assign a different lecturer.
10. Every team change is written to an **audit log** (who, when, what).
11. Student ID format: 2 letters + 6 digits (e.g. `SE184118`). Phone numbers stored as **string**. Phone visible only to teammates, assigned lecturer and Admin.
12. Specialization and domain are chosen from **catalogs**, never free text.
13. **Finalization.** Leaders do not finalize. When a round closes, the system finalizes every team that satisfies rules 1–2 (`Active` → `Finalized`). Teams that do not satisfy them become `NeedsReview`. Admin can also finalize or reopen a team manually.
14. **After the deadline.** Students without a team are first used to fill `NeedsReview` teams (missing members or missing second specialization), then the rest are grouped into new teams. Anything still invalid after that stays `NeedsReview` for Admin to fix by hand (add/move members) before finalizing.
15. Team status: `Draft` (created, fewer than 4 members) → `Active` → `Finalized`, or `NeedsReview` after the deadline.

## 4. Architecture overview

Microservices, ASP.NET Core 8. Clients only talk to the **API Gateway**. Each service owns its database; **no service reads another service's database**.

### C0 – System Context
- Users (Student, Lecturer, EXE Department) → **EXE Team Matching System** over HTTPS. Students and lecturers use the mobile app, the EXE Department uses the web app.
- System → **Google Identity** (OAuth 2.0 / OpenID Connect) to authenticate users.
- System → **Email Service** (SMTP) to send notifications.
- No integration with FAP (FPT Academic Portal has no API) and no Google Sheets sync.

### C1 – Containers and communication

```
Student, Lecturer --HTTPS--> Mobile App (Flutter) --REST/HTTPS--> API Gateway (YARP)
EXE Department    --HTTPS--> Web App (React)      --REST/HTTPS--> API Gateway (YARP)
API Gateway --REST/HTTP--> Identity Service | Team Service | Academic Service | Notification Service
Job Service      --REST/HTTP (internal, service token)--> Team Service
Team Service     --REST/HTTP--> Academic Service
Team Service     --gRPC-------> Matching Service
Job Service      --gRPC-------> Matching Service
Team, Academic, Job  --Publish--> Kafka
Kafka --Consume--> Notification Service, Matching Service
Academic Service, Matching Service --> Redis (cache)
Identity -> Identity DB | Team -> Team DB | Academic -> Academic DB | Job -> Job DB | Notification -> Notification DB  (PostgreSQL)
Identity Service     --OAuth 2.0--> Google Identity
Notification Service --SMTP------> Email Service
```

Diagram sources: `c0.mmd`, `c1.mmd` (Mermaid flowchart). **The C1 diagram still needs three additions to match this file:** the Mobile App (Flutter) container, the edge API Gateway → Notification Service, and the edge Job Service → Team Service.

## 5. Services

### 5.1 API Gateway – YARP
- Routes requests, validates JWT, CORS, aggregates Swagger of downstream services.
- Exposes Identity, Team, Academic and the read API of Notification (`/api/notifications`). Job and Matching are **internal only**.

### 5.2 Identity Service – REST API
- Google login: receives Google ID token, checks the email is in the allowed list, then **issues the system's own JWT** (access token ~15 min) + refresh token (~7 days). Refresh, logout/revoke.
- Roles: `Student`, `Lecturer`, `Admin`. Leader is checked as a policy at the Services layer, not a role.
- DB (`Identity DB`): `UserAccount` (email, role, status), `Role`, `RefreshToken` (token, expiry, revoked).
- No Redis (JWT is validated by signature; caching login data risks keeping locked accounts active).

### 5.3 Academic Service – REST API
- Semesters and registration rounds (course: EXE101 / EXE201; type: team formation / change request; open time, deadline, status). Change-request rounds exist only for EXE201.
- **Import student list** of the semester from Excel (ClosedXML) – name, student ID, email, specialization, class.
- Catalogs: class codes, specializations, research domains (D01–D18).
- Student and lecturer profiles. A lecturer profile has a **capacity** (maximum number of teams) and an accepting-new-teams flag.
- Answers Team Service (HTTP): "is the round open?", "does this student exist / is in this semester?".
- Redis: caches catalogs and active round status; invalidate on change.
- Publishes: `StudentImported`, `StudentProfileUpdated`, `RoundOpened`, `RoundClosed`.
- DB (`Academic DB`): `Semester`, `RegistrationRound`, `Student`, `Lecturer`, `ClassCode`, `Specialization`, `Domain`.

### 5.4 Team Service – REST API (core business)
- CRUD teams, members, leader. Each team has a `Course` and a status (`Draft`, `Active`, `NeedsReview`, `Finalized`).
- **Finalization** (rule 13): internal endpoint `POST /internal/teams/finalize-round` called by Job Service after the round closes; validates rules 1–2 via Matching `ValidateComposition`, sets `Finalized` or `NeedsReview`. Admin endpoints to finalize / reopen one team.
- **EXE101 baseline import** (EXE201 only, Admin): `POST /api/teams/baseline-import` takes an Excel file (team name, project, leader, member student IDs), creates the inherited EXE201 teams and stores the baseline in `TeamBaselineMember`. This is the reference for every change request.
- Invitations and join requests (send/accept/decline/expire).
- Recruitment posts (team looking for members) and seeking posts (student looking for team).
- Change requests (EXE201 only) + Admin approval. Pending changes of an inherited team are stored as `ChangeRequestItem` (add / remove a student) and applied to `TeamMember` only on approval (rule 7).
- Lecturer flow for both courses (rule 9): `LecturerPreference` holds the team's request and its status (`Pending`, `Accepted`, `Declined`); `LecturerAssignment` holds Admin's final decision.
- Search / filter / sort / paginate teams (e.g. `GET /api/teams?domain=D04&page=1&pageSize=20&sort=name`).
- Writes events through a **Transactional Outbox** (event row saved in the same transaction, background dispatcher publishes to Kafka).
- DB (`Team DB`): `Team`, `TeamMember`, `TeamDomain`, `Invitation`, `JoinRequest`, `RecruitmentPost`, `SeekingPost`, `TeamBaselineMember`, `ChangeRequest`, `ChangeRequestItem`, `LecturerPreference`, `LecturerAssignment`, `AuditLog`, `OutboxMessage`.

### 5.5 Matching Service – gRPC only ("matchmaker")
Only computes suggestions; never creates teams or writes business data.
- `SuggestMembers` – students without a team who fill missing specializations of a team.
- `SuggestTeams` – recruiting teams that need the student's specialization.
- `ValidateComposition` – checks 4–6 members and ≥ 2 specializations.
- `PlanAutoAssignment` – after the deadline, first proposes unmatched students to fill `NeedsReview` teams, then splits the remaining students into new valid teams (rule 14).
- Keeps a **read model in Redis** (student specialization, skills, has-team flag, team gaps), updated by consuming Kafka events (`StudentImported`, `StudentProfileUpdated`, `TeamCreated`, `MemberJoined`, `MemberLeft`). No SQL database.

```proto
syntax = "proto3";
package exe.matching.v1;

service MatchingService {
  rpc SuggestMembers (SuggestMembersRequest) returns (SuggestMembersResponse);
  rpc SuggestTeams (SuggestTeamsRequest) returns (SuggestTeamsResponse);
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
- DB (`Job DB`): Hangfire storage only (jobs, schedules, state, retries). No business data.
- Hangfire Dashboard at `/hangfire` for the demo.

### 5.7 Notification Service – REST API + Worker (BackgroundService)
- Consumes Kafka events, sends email via SMTP (MailKit), stores in-app notifications.
- Idempotent: stores processed event IDs to avoid duplicate emails.
- Small read API for the in-app notification list, exposed through the Gateway and scoped to the caller's JWT: `GET /api/notifications?page=&pageSize=&unreadOnly=`, `GET /api/notifications/unread-count`, `PATCH /api/notifications/{id}/read`, `PATCH /api/notifications/read-all`. The mobile app polls (pull to refresh); no realtime push.
- DB (`Notification DB`): `Notification` (recipient, content, type, read flag, time), `EmailLog`, `ProcessedEvent`.

## 6. Kafka topics and events

| Topic | Events | Producer | Consumers |
|---|---|---|---|
| `exe.team` | TeamCreated, MemberJoined, MemberLeft, LeaderChanged, TeamFinalized, TeamNeedsReview | Team | Notification, Matching |
| `exe.invitation` | InvitationSent, InvitationAccepted, InvitationDeclined, InvitationExpired, JoinRequestSubmitted, JoinRequestAccepted, JoinRequestDeclined | Team, Job | Notification |
| `exe.change-request` | ChangeRequestSubmitted, ChangeRequestApproved, ChangeRequestRejected | Team | Notification |
| `exe.registration` | RoundOpened, DeadlineApproaching, RoundClosed | Academic, Job | Notification (Job listens to RoundClosed internally via schedule) |
| `exe.student` | StudentImported, StudentProfileUpdated | Academic | Matching |
| `exe.assignment` | LecturerRequested, LecturerAccepted, LecturerDeclined, LecturerAssigned | Team | Notification |

Event envelope: `eventId` (GUID, for idempotency), `eventType`, `occurredAt` (UTC), `correlationId`, `payload`.
Consumers retry, then send to a dead-letter topic `<topic>.dlq`.

## 7. Tech stack

- C# 12, **ASP.NET Core 8**, **EF Core 8** (code-first, migrations, seed data) with **PostgreSQL** (Npgsql).
- gRPC (`Grpc.AspNetCore`), Apache Kafka (KRaft, `Confluent.Kafka`), Redis 7 (`StackExchange.Redis`), Hangfire (PostgreSQL storage), YARP.
- Auth: Google OAuth 2.0 / OpenID Connect + JWT Bearer.
- FluentValidation, Serilog (structured logs with CorrelationId through Gateway, REST, gRPC, Kafka), global exception middleware returning **ProblemDetails (RFC 7807)**, gRPC interceptor mapping exceptions to status codes.
- Swagger/OpenAPI per REST service, aggregated at the Gateway.
- Docker + Docker Compose.
- Clients: **Flutter** mobile app (Material 3) for Student and Lecturer; **React** web app for the EXE Department.

## 8. Code conventions

Every REST service uses the layered structure required by the assignment: **API – Services – Repository**.

```
src/
  BuildingBlocks/        # shared: JWT, logging, exception middleware, Kafka, Outbox, event contracts
  ApiGateway/
  Services/
    Identity/      Identity.Api | Identity.Services | Identity.Repositories | Identity.Domain
    Academic/      Academic.Api | Academic.Services | Academic.Repositories | Academic.Domain
    Team/          Team.Api     | Team.Services     | Team.Repositories     | Team.Domain
    Matching/      Matching.Grpc | Matching.Services
    Notification/  Notification.Worker
    Jobs/          Jobs.Host (Hangfire)
  Protos/                # shared *.proto
deploy/
  docker-compose.yml
  docker-compose.override.yml
```

- **Api**: controllers, DTOs, validation, mapping, Swagger. No business logic.
- **Services**: business rules, gRPC/HTTP calls, write Outbox.
- **Repositories**: DbContext, repositories, migrations, paged/filtered/sorted queries.
- Use built-in DI; configuration via `appsettings.{Environment}.json` + environment variables; never commit secrets.
- REST naming: plural nouns, kebab-case routes (`/api/teams/{id}/members`, `/api/change-requests`).
- All times stored in UTC.
- Each service exposes `/health`.

## 9. Deployment (Docker Compose)

| Service | Port (host) |
|---|---|
| web (React, EXE Department) | 3000 |
| gateway | 8080 |
| identity-api | 5001 |
| academic-api | 5002 |
| team-api | 5003 |
| matching-grpc | 5004 (HTTP/2) |
| notification (API + worker) | 5005 |
| jobs (`/hangfire`) | 5006 |
| postgres | 5432 (one instance, one database per service) |
| kafka | 9092 |
| kafka-ui | 8085 |
| redis | 6379 |
| mailpit (dev SMTP) | 1025 / 8025 |

The Flutter mobile app is not part of Compose; it points at the gateway URL. `depends_on` with `condition: service_healthy`. EF migrations auto-run in Development. Demo seed data comes from an **anonymized** Group List (no phone numbers, fake emails).

## 10. PRN232 requirement mapping

| Requirement | Where |
|---|---|
| REST API, CRUD, layered architecture | Identity, Academic, Team |
| JWT authentication | Identity issues, Gateway + services validate |
| Search, filter, sort, pagination | Team (teams), Academic (students) |
| Background job | Job Service (Hangfire), Outbox dispatcher, Kafka consumers |
| Message broker: ≥ 1 producer, ≥ 1 consumer | Kafka: Team/Academic/Job produce, Notification/Matching consume |
| Independent gRPC service called from REST API | Team Service → Matching Service |
| Docker deployment | Docker Compose |

## 11. End-to-end demo flows

1. **Create team & invite**: Leader logs in (Google → JWT) → `POST /api/teams` → Team asks Academic if round is open → Team calls Matching `SuggestMembers` (gRPC) → Leader invites → Outbox → Kafka `InvitationSent` → Notification emails the student → student accepts → `MemberJoined` → Matching updates Redis.
2. **Change request (EXE201 only)**: Admin imports the EXE101 baseline → inherited team exists → Leader invites a new student / marks a member as leaving (pending changes) → Leader submits the change request with a reason → `ChangeRequestSubmitted` → Admin approves → pending changes applied to members, AuditLog written, `ChangeRequestApproved` → email to members and lecturer. On rejection nothing changes and `ChangeRequestRejected` is sent.
3. **Deadline, finalization & auto-assign**: reminders at 72h/24h → round auto-closes → Job calls Team `finalize-round` (valid teams `Finalized`, others `NeedsReview`) → Job calls Matching `PlanAutoAssignment` → Job calls Team to fill incomplete teams and create new ones → `MemberJoined` / `TeamCreated` → emails → Admin reviews what is still `NeedsReview` and finalizes.
4. **Lecturer (EXE101 and EXE201)**: Leader requests a lecturer → `LecturerRequested` → lecturer accepts (`LecturerAccepted`) or declines (`LecturerDeclined`, team picks another) → Admin sets the final assignment → `LecturerAssigned` → notification to the team and the lecturer.
5. **In-app notifications**: any event above → Notification Service stores a `Notification` row and sends the email → mobile app reads `GET /api/notifications` through the Gateway.

## 12. Open decisions / assumptions

- **Email whitelist check at login**: Identity needs the imported list owned by Academic. Options: (a) Identity calls Academic over HTTP at login, or (b) Academic publishes `StudentImported` and Identity consumes it to pre-create accounts. Not yet decided; neither edge is drawn in C1.
- **Job → Team (internal HTTP)** for finalizing the round and creating auto-assigned teams exists in the design but is not drawn in C1. Same for **Mobile App** and **Gateway → Notification**.
- **Assumptions added on 2026-10-12 – confirm with the team/lecturer:**
  - The EXE101 baseline of EXE201 teams comes from an **Excel import** by Admin (section 5.4), not from data of a previous semester inside the system.
  - Member changes of an inherited EXE201 team are **pending until Admin approves** the change request (rule 7).
  - Teams are **finalized automatically at the deadline**; invalid teams become `NeedsReview` and are handled by auto-assign, then by Admin (rules 13–14).
  - In-app notifications are fetched by **polling** the Notification read API; no SignalR / push.
- The imported student file is assumed to come from FAP exports and to contain each student's Google account email.
- Lecturer emails are entered by Admin to grant the `Lecturer` role; the first Admin account is seeded.
- If the team is short on people: merge Academic into Team and Notification into Job (4 services).
