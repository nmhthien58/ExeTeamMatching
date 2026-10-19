"""Phần viết tay của tài liệu thiết kế DB (build.py ghép với cấu trúc đọc từ database thật).

Bảng, cột, ràng buộc, index, trigger KHÔNG viết ở đây: chúng được sinh từ deploy/db/*.sql.
Ở đây chỉ có phần giải thích: tổng quan, quy ước, quy tắc nghiệp vụ → cách ép trong DB, tham chiếu chéo service.
"""

from datetime import date

C = '<span class=mono>{}</span>'.format


def cover(today: date) -> str:
    return f"""<div class=cover>
<div class=bar></div>
<div class=small>PRN232 – Final Assignment · FPT University · FA26</div>
<h1>EXE Team Matching System<br>Thiết kế Database</h1>
<div class=sub>Bản 4 · PostgreSQL 16 · 1 instance · mỗi service 1 database riêng</div>
<div class=small>Sinh tự động từ {C('deploy/db/*.sql')} bằng {C('docs/db-design/build.py')} ngày {today:%d/%m/%Y}.<br>
Nguồn nghiệp vụ: {C('docs/ux/PROJECT_CONTEXT.md')} (quy tắc 1–27, mục 5, 6, 9), sơ đồ C1 {C('docs/c4-architecture/c1.mmd')}.</div>
</div>"""


SERVICES = {
    "identity": ("Identity Service", "Identity DB"),
    "academic": ("Academic Service", "Academic DB"),
    "team": ("Team Service", "Team DB"),
    "notification": ("Notification Service", "Notification DB"),
    "collab": ("Collaboration Service", "Collab DB"),
}


def overview(totals: list[tuple[str, str, list[str]]]) -> str:
    rows = "".join(
        f"<tr><td>{SERVICES[s][0]}</td><td>{SERVICES[s][1]}</td><td class=mono>{db}</td><td class=mono>{s}</td>"
        f"<td class=mono>{s}_svc</td><td>{len(names)}: {', '.join(names)}</td></tr>"
        for db, s, names in totals)
    count = sum(len(n) for _, _, n in totals)
    return f"""<h2 class=break>1. Tổng quan và quyền sở hữu</h2>
<p>Theo C1, mỗi microservice sở hữu dữ liệu của mình và <b>không service nào đọc database của service khác</b>. Mỗi "DB" trong C1
là một database riêng trên cùng một instance PostgreSQL (container {C('postgres')} trong Docker Compose), nên khi demo có thể mở
từng database để xem. Mỗi service kết nối bằng một login role chỉ có quyền CONNECT vào database của mình; không có khóa ngoại
chéo database. Script {C('deploy/db/00_init.sh')} tạo role và database; các file {C('01–06_*.sql')} tạo bảng.</p>
<p>Trong mỗi database, bảng nằm trong schema cùng tên service (ví dụ {C('team_db')} → schema {C('team')}), nên tài liệu ghi
tên bảng dạng {C('schema.bảng')}. Tổng cộng <b>{count} bảng</b> nghiệp vụ.</p>
<table><thead><tr><th>Service</th><th>"DB" trong C1</th><th>Database</th><th>Schema</th><th>Login role</th><th>Bảng</th></tr></thead>
<tbody>{rows}
<tr><td>Job Service (Hangfire)</td><td>Job DB</td><td class=mono>job_db</td><td class=mono>hangfire</td><td class=mono>job_svc</td>
<td>Bảng do Hangfire.PostgreSql tự tạo (job, state, set, hash, lock…). Không có dữ liệu nghiệp vụ (mục 9).</td></tr>
<tr><td>Matching Service</td><td>— (chỉ Redis)</td><td>—</td><td>—</td><td>—</td><td>Không có SQL. Read model trong Redis (mục 9).</td></tr>
<tr><td>API Gateway</td><td>—</td><td>—</td><td>—</td><td>—</td><td>Không lưu dữ liệu.</td></tr>
</tbody></table>"""


CONVENTIONS = f"""<h2>2. Quy ước chung</h2><ul>
<li><b>Đặt tên:</b> snake_case, bảng số nhiều; tiền tố {C('pk_ / fk_ / uq_ / ck_ / ex_ / idx_ / trg_')}. Tên ràng buộc ổn định để
service map lỗi PostgreSQL ({C('SqlState')} 23505 / 23514 / 23503 + {C('ConstraintName')}) sang ProblemDetails với mã lỗi trong
{C('docs/contracts/README.md')}.</li>
<li><b>Khóa chính:</b> {C('uuid')} ({C('gen_random_uuid()')}) cho dữ liệu nghiệp vụ vì ID đi qua Kafka và giữa các service;
{C('integer IDENTITY')} cho danh mục nhỏ; {C('bigint IDENTITY')} cho audit log.</li>
<li><b>Thời gian:</b> luôn {C('timestamptz')}, lưu UTC. Bảng có thay đổi có {C('created_at')}, {C('updated_at')} (trigger
{C('trg_*_updated_at')} tự cập nhật).</li>
<li><b>Trạng thái / enum:</b> {C('varchar')} + {C("CHECK (... IN (...))")}, không dùng kiểu ENUM của PostgreSQL.</li>
<li><b>Email:</b> {C('citext')} để UNIQUE không phân biệt hoa thường. <b>SĐT:</b> {C('varchar(15)')}, giữ số 0 đầu, chỉ có từ file import.</li>
<li><b>Tham chiếu chéo service:</b> chỉ lưu ID (không FK) + snapshot vài trường cần hiển thị / kiểm tra (MSSV, tên, chuyên ngành,
mã domain). Cột như vậy có ghi chú {C('academic.students.id')}… trong bảng cột.</li>
<li><b>Lịch sử:</b> dùng cột {C('status')} / {C('ended_at')} / {C('left_at')} thay vì xóa cứng dữ liệu có ý nghĩa lịch sử.</li>
<li><b>Ràng buộc hai lớp:</b> Services (FluentValidation, Matching {C('ValidateComposition')}) kiểm tra trước để trả lỗi thân
thiện; CHECK / UNIQUE / trigger trong DB là lưới an toàn cuối, chống race condition khi nhiều request song song.</li>
<li><b>Optimistic concurrency:</b> cột hệ thống {C('xmin')} làm row version cho {C('teams')}, {C('invitations')},
{C('join_requests')}, {C('change_requests')}, {C('slot_bookings')}.</li>
<li><b>EF Core:</b> không dùng migration để tạo bảng. Entity và DbContext map đúng bảng trong script
({C('UseSnakeCaseNamingConvention')}, {C('HasDefaultSchema')}); muốn đổi bảng thì sửa script, chạy lại {C('build.py')} và mở PR
cho Thiên duyệt.</li></ul>"""


_RULES = [
    ("1. Nhóm 4–6 người, đúng 1 leader là thành viên",
     f"{C('team_members.role')} LEADER / MEMBER; partial unique {C('uq_team_members_one_leader')} (≤ 1 leader); constraint trigger "
     f"deferred {C('trg_team_members_composition')} → {C('ck_team_exactly_one_leader')} (= 1 leader lúc COMMIT, đổi leader trong một "
     f"transaction được); {C('teams.member_count')} do {C('trg_team_members_count')} duy trì + {C('ck_teams_member_count')} "
     f"(0–6) chặn người thứ 7 ngay; khi FINALIZED trigger ép 4–6 ({C('ck_team_finalized_size')})."),
    ("2. ≥ 2 chuyên ngành khi chốt",
     f"Snapshot {C('team_members.specialization_id')}; trigger {C('trg_teams_composition')} → {C('ck_team_finalized_specializations')} "
     f"đếm DISTINCT khi status = FINALIZED (kể cả khi thay thành viên sau đó)."),
    ("3. Không cần cùng lớp", f"Không ràng buộc; lớp chỉ nằm ở {C('academic.semester_enrollments')}."),
    ("4. 1 sinh viên tối đa 1 nhóm mỗi học kỳ và môn",
     f"{C('uq_team_members_semester_student')}; FK composite ({C('team_id, semester_id')}) → {C('teams(id, semester_id)')}."),
    ("5. Chỉ tài khoản tạo sẵn từ danh sách được đăng nhập",
     f"{C('identity.user_accounts')} (status PENDING → ACTIVE, {C('allowed_until')}) + {C('user_account_roles')}, tạo từ "
     f"StudentImported / LecturerRegistered / MentorRegistered (idempotent qua {C('processed_events')}). Login không gọi Academic."),
    ("6. Chỉ thao tác khi đợt đăng ký mở",
     f"{C('academic.registration_rounds')} (status, opens_at, deadline_at, {C('ex_registration_rounds_overlap')} chống chồng đợt). "
     f"Team hỏi Academic qua HTTP (Redis cache) trước khi ghi; không ép bằng DB vì nằm ở database khác."),
    ("7. Đổi nhóm chỉ ở EXE201, có lý do, Admin duyệt",
     f"FK composite ({C('team_id, course_code')}) + {C('ck_change_requests_course')}; {C('ck_change_requests_reason')}; "
     f"{C('ck_change_requests_review')}, {C('ck_change_requests_reject_note')}; {C('uq_change_requests_pending')} (1 yêu cầu chờ / nhóm). "
     f"Thay đổi đã đồng ý nằm chờ trong {C('change_request_items')} ({C('uq_change_request_items_pending')}); mốc EXE101 ở "
     f"{C('team_baseline_members')}; {C('ck_change_requests_counts')} lưu số và % thay đổi."),
    ("8. Lời mời / yêu cầu hết hạn sau 72 giờ",
     f"{C('expires_at')} + {C('ck_invitations_expiry')}, {C('ck_join_requests_expiry')}; partial index {C('idx_invitations_expiry')}, "
     f"{C('idx_join_requests_expiry')} cho job quét; số giờ ở {C('rule_settings.invitation_expiry_hours')}."),
    ("9. Ghép giảng viên hướng dẫn tự động",
     f"{C('lecturer_preferences')} (khóa chính team + rank 1–3, {C('uq_lecturer_preferences_lecturer')}); "
     f"{C('lecturer_assignments')} với {C('reason')}, {C('preference_rank')}, {C('source')} AUTO / MANUAL, "
     f"{C('uq_lecturer_assignments_active')} (1 giảng viên hiệu lực / nhóm); {C('advisor_matching_runs')} lưu kế hoạch xem trước "
     f"(Ghép sớm); số nhóm tối đa {C('lecturers.max_teams_exe101 / _exe201')} (NULL = không giới hạn)."),
    ("10. Audit log mọi thay đổi nhóm",
     f"{C('team.audit_logs')} ghi cùng transaction; {C('trg_audit_logs_append_only')} chặn UPDATE / DELETE."),
    ("11. MSSV 2 chữ + 6 số; SĐT dạng chuỗi",
     f"{C('ck_students_student_code')}, {C('ck_team_members_student_code')}; {C('ck_students_phone')}. Quyền xem SĐT xử lý ở API."),
    ("12. Chuyên ngành, domain từ danh mục",
     f"Trong academic: FK tới {C('specializations')}, {C('domains')} ({C('ck_domains_code')} D01–D18). Trong team: lưu ID + mã có CHECK "
     f"định dạng ({C('ck_team_domains_code')})."),
    ("13–15. Chốt nhóm, NeedsReview, trạng thái",
     f"{C('ck_teams_status')} DRAFT / ACTIVE / NEEDS_REVIEW / FINALIZED / DISBANDED; {C('ck_teams_finalized_at')}; "
     f"{C('teams.is_auto_assigned')}, {C('team_members.join_source')} AUTO_ASSIGN / ADMIN cho ghép tự động và Admin sửa tay."),
    ("16. Hồ sơ = 5 kỹ năng + mô tả",
     f"{C('academic.student_skills')} (khóa chính student + skill, {C('ck_student_skills_skill')}, {C('ck_student_skills_level')} 1–5); "
     f"{C('students.description')} ≤ 500; {C('profile_completed_at')}."),
    ("17. Ý tưởng khi xin vào nhóm",
     f"{C('join_requests.has_no_idea')} + {C('join_request_ideas')} ({C('ck_join_request_ideas_position')} 1–3); constraint trigger "
     f"{C('trg_join_requests_ideas')} / {C('trg_join_request_ideas_check')} ép \"1–3 ý tưởng hoặc chưa có ý tưởng\" lúc COMMIT."),
    ("18. Khóa Khám phá sau khi lập nhóm",
     f"Giai đoạn tính từ {C('registration_rounds')} (đợt TEAM_FORMATION của môn đã CLOSED). Team kiểm tra ở Services."),
    ("19. Gợi ý đề tài, quan tâm của cố vấn",
     f"{C('team.topic_interests')} (advisor_kind LECTURER / MENTOR, status OPEN / WITHDRAWN / CLOSED, {C('uq_topic_interests_open')}); "
     f"giới hạn ở {C('rule_settings.interest_extra_slots')}, {C('max_interests_unlimited')}; domain ở {C('lecturer_domains')}, "
     f"{C('mentor_domains')}."),
    ("20. Mentor",
     f"{C('academic.mentors')} ({C('max_teams')}, {C('preferred_teams')} ≤ max, {C('is_internal')} + {C('uq_mentors_lecturer_id')}); "
     f"{C('team.mentor_assignments')} ({C('uq_mentor_assignments_active')}: một mentor / nhóm); {C('semesters.mentoring_start_week')}, "
     f"{C('expected_mentor_sessions')}; Identity: một tài khoản nhiều role ({C('user_account_roles')})."),
    ("21. Quyền workspace",
     f"{C('collab.workspace_members')} dựng từ Kafka (role LEADER / MEMBER / INSTRUCTOR / MENTOR, {C('left_at')}); "
     f"{C('uq_workspace_members_one_instructor')}, {C('uq_workspace_members_one_mentor')}; {C('workspaces.previous_workspace_id')} cho "
     f"kho EXE101."),
    ("22. Tin nhắn",
     f"{C('channels')} ({C('uq_channels_workspace_kind')}: TEAM, ADVISORS); {C('ck_messages_body')} ≤ 2000; "
     f"{C('advisor_questions')} OPEN / ANSWERED; {C('message_reads')}."),
    ("23. Việc và bài nộp",
     f"{C('task_items')} kind TASK / DELIVERABLE, {C('ck_task_items_deliverable_creator')} (chỉ giảng viên tạo bài nộp), "
     f"{C('deliverable_submissions')} ACCEPTED / NEEDS_REVISION ({C('ck_deliverable_submissions_revision_note')})."),
    ("24. File",
     f"{C('project_files')} ({C('ck_project_files_type')}, {C('uq_project_files_name')}), {C('file_versions')} "
     f"({C('ck_file_versions_size')} ≤ 25 MB); nội dung ở S3, DB chỉ giữ {C('object_key')}."),
    ("25. Họp",
     f"Lịch tuần EXE201: {C('meeting_schedules')}, {C('schedule_windows')}, {C('schedule_slots')}, {C('slot_bookings')} "
     f"({C('uq_slot_bookings_slot_active')}: một nhóm / slot, ai đặt trước được trước; {C('uq_slot_bookings_workspace_active')}); "
     f"{C('meetings.week_no')} + {C('uq_meetings_weekly')}. Họp đề xuất: {C('meeting_slots')}, {C('meeting_responses')}."),
    ("26. Đề tài xuyên kỳ",
     f"{C('team.projects')}, {C('project_versions')} (PIVOT phải có lý do và yêu cầu đổi nhóm: {C('ck_project_versions_pivot')}); "
     f"{C('teams.project_id')} + {C('uq_teams_project_semester')}; {C('change_request_items')} action PROJECT_CHANGE "
     f"({C('uq_change_request_items_project_pending')})."),
    ("27. Checkpoint EXE101 (không chấm điểm)",
     f"{C('collab.checkpoint_reviews')} ({C('ck_checkpoint_reviews_no')} 1–4, {C('uq_checkpoint_reviews_workspace')}); tuần mục tiêu "
     f"ở {C('semesters.checkpoint_target_weeks')}."),
]

RULES = ("<h2>3. Quy tắc nghiệp vụ → cách ép trong DB</h2>"
         "<p>Số quy tắc theo PROJECT_CONTEXT §3. Quy tắc nào cần dữ liệu của service khác thì kiểm tra ở tầng Services.</p>"
         "<table><thead><tr><th style='width:27%'>Quy tắc</th><th>Cách thực thi</th></tr></thead><tbody>"
         + "".join(f"<tr><td>{r}</td><td>{how}</td></tr>" for r, how in _RULES)
         + "</tbody></table>")


SCHEMA_INTRO = {
    "identity": "<p>Tài khoản được tạo sẵn từ sự kiện của Academic (quy tắc 5). Một tài khoản có thể giữ nhiều vai trò, ví dụ giảng viên "
                "vừa là mentor (quy tắc 20); {} trỏ tới id sinh viên / giảng viên / mentor bên Academic và được phát vào claim "
                "{} của JWT. Refresh token chỉ lưu hash SHA-256, có rotation.</p>".format(
                    C("user_account_roles.external_ref_id"), C("student_id / lecturer_id / mentor_id")),
    "academic": "<p>Nguồn của học kỳ, đợt đăng ký, danh sách sinh viên và giảng viên, mentor, danh mục. Publish sự kiện qua "
                "{} (topic {}, {}).</p>".format(C("outbox_messages"), C("exe.student"), C("exe.registration")),
    "team": "<p>Nghiệp vụ lõi: đề tài, nhóm, lời mời, yêu cầu tham gia, bài đăng, đổi nhóm, ghép cố vấn. Các trigger ép quy tắc 1, 2, 17 "
            "và audit log append-only. Publish qua {} (topic {}, {}, {}, {}).</p>".format(
                C("outbox_messages"), C("exe.team"), C("exe.invitation"), C("exe.change-request"), C("exe.assignment")),
    "notification": "<p>Read model người nhận ({}) dựng từ {}; thông báo trong ứng dụng và log email. Idempotent theo eventId.</p>".format(
        C("contacts"), C("exe.student")),
    "collab": "<p>Workspace của nhóm (thêm 19/10/2026): tin nhắn, file, việc và bài nộp, họp, checkpoint. {} là read model dựng từ "
              "{} và {}; file nhị phân nằm ở S3 (bucket {}). Publish qua {} (topic {}).</p>".format(
                  C("workspace_members"), C("exe.team"), C("exe.assignment"), C("exe-files"), C("outbox_messages"), C("exe.collab")),
}


JOB_AND_MATCHING = f"""<h2 class=break>9. Service không có schema nghiệp vụ</h2>
<h3>9.1 Matching Service — Redis read model</h3>
<p>Matching chỉ tính toán (gRPC), không có SQL. Read model trong Redis cập nhật từ Kafka; dựng lại được bằng cách replay
{C('exe.student')}, {C('exe.team')}, {C('exe.assignment')} từ đầu. Mỗi event lưu {C('matching:processed:{eventId}')} (TTL 7 ngày) để idempotent.</p>
<table><thead><tr><th>Key</th><th>Kiểu</th><th>Nội dung</th><th>Cập nhật bởi</th></tr></thead><tbody>
<tr><td class=mono>matching:{{semesterId}}:student:{{studentId}}</td><td>Hash</td><td>chuyên ngành, 5 kỹ năng, teamId (rỗng = chưa có nhóm)</td><td>StudentImported, StudentProfileUpdated, MemberJoined, MemberLeft</td></tr>
<tr><td class=mono>matching:{{semesterId}}:unmatched:{{specializationId}}</td><td>Set</td><td>sinh viên chưa có nhóm theo chuyên ngành</td><td>StudentImported, MemberJoined, MemberLeft</td></tr>
<tr><td class=mono>matching:{{semesterId}}:team:{{teamId}}</td><td>Hash</td><td>số thành viên, chuyên ngành, kỹ năng (max mỗi loại), domain, trạng thái, giảng viên / mentor đã ghép</td><td>TeamCreated, MemberJoined, MemberLeft, TeamFinalized, ProjectChanged, LecturerAssigned, MentorAssigned</td></tr>
<tr><td class=mono>matching:advisor:{{kind}}:{{id}}</td><td>Hash</td><td>domain chuyên môn, số nhóm tối đa / mong muốn, tải hiện tại</td><td>LecturerRegistered, LecturerProfileUpdated, MentorRegistered, LecturerAssigned, MentorAssigned, MentorUnassigned</td></tr>
<tr><td class=mono>matching:{{semesterId}}:interest:{{teamId}}</td><td>Set</td><td>cố vấn đang quan tâm nhóm</td><td>TopicInterestOpened, TopicInterestClosed</td></tr>
<tr><td class=mono>matching:{{semesterId}}:prefs:{{teamId}}</td><td>List</td><td>nguyện vọng giảng viên 1–3</td><td>LecturerPreferencesUpdated</td></tr>
</tbody></table>
<h3>9.2 Job Service — schema hangfire</h3>
<p>{C('UsePostgreSqlStorage(..., new PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true })')} với role
{C('job_svc')}; script {C('05_job_db.sql')} chỉ tạo schema. Hangfire tự tạo bảng; không có bảng nghiệp vụ. Job gọi endpoint nội bộ
của service sở hữu dữ liệu (service token) thay vì ghi DB.</p>"""


CROSS_SERVICE = f"""<h2 class=break>10. Tham chiếu chéo service và đồng bộ qua Kafka</h2>
<h3>10.1 ID tham chiếu sang service khác</h3>
<table><thead><tr><th>Cột</th><th>Trỏ tới (chủ sở hữu)</th><th>Kiểm tra khi ghi</th><th>Đồng bộ</th></tr></thead><tbody>
<tr><td class=mono>team.*.student_id, invitee_student_id, requested_by_student_id</td><td class=mono>academic.students.id</td><td>Team gọi Academic HTTP "SV có trong học kỳ?", lấy snapshot MSSV, tên, chuyên ngành</td><td>Snapshot lúc ghi; khi chốt nhóm lấy lại chuyên ngành</td></tr>
<tr><td class=mono>team.teams.semester_id, *.round_id</td><td class=mono>academic.semesters / registration_rounds</td><td>HTTP "đợt có đang mở?"</td><td>Không cần</td></tr>
<tr><td class=mono>team.*.specialization_id, domain_id (+ code)</td><td class=mono>academic.specializations / domains</td><td>HTTP lấy danh mục (Redis cache)</td><td>Danh mục chỉ vô hiệu, không xóa</td></tr>
<tr><td class=mono>team.lecturer_preferences / lecturer_assignments.lecturer_id, topic_interests.advisor_id, mentor_assignments.mentor_id</td><td class=mono>academic.lecturers / mentors</td><td>HTTP kiểm tra cố vấn đang hoạt động, số nhóm tối đa</td><td>Snapshot tên cố vấn</td></tr>
<tr><td class=mono>collab.workspaces.team_id, project_id; workspace_members.person_id</td><td class=mono>team.teams, team.projects; academic students / lecturers / mentors</td><td>—</td><td>Consume exe.team, exe.assignment</td></tr>
<tr><td class=mono>*_account_id, created_by, imported_by, updated_by</td><td class=mono>identity.user_accounts.id</td><td>Lấy từ claim sub trong JWT</td><td>Không cần</td></tr>
<tr><td class=mono>identity.user_account_roles.external_ref_id</td><td class=mono>academic students / lecturers / mentors</td><td>—</td><td>Consume StudentImported, LecturerRegistered, MentorRegistered</td></tr>
<tr><td class=mono>notification.contacts.id, notifications.recipient_id</td><td class=mono>academic students / lecturers / mentors</td><td>—</td><td>Consume exe.student</td></tr>
</tbody></table>
<p>JWT do Identity cấp mang {C('sub')} (user_account id), {C('roles')} (danh sách) và {C('student_id / lecturer_id / mentor_id')}
(= external_ref_id), nên Team, Collaboration và Notification làm việc trực tiếp bằng ID của Academic.</p>
<h3>10.2 Topic, producer và consumer</h3>
<table><thead><tr><th>Topic</th><th>Event</th><th>Producer</th><th>Consumer</th></tr></thead><tbody>
<tr><td class=mono>exe.student</td><td>StudentImported, StudentProfileUpdated, LecturerRegistered, LecturerProfileUpdated, MentorRegistered</td><td>Academic (outbox)</td><td>Identity, Notification, Matching</td></tr>
<tr><td class=mono>exe.registration</td><td>RoundOpened, DeadlineApproaching, RoundClosed</td><td>Academic (outbox), Job</td><td>Notification</td></tr>
<tr><td class=mono>exe.team</td><td>TeamCreated, MemberJoined, MemberLeft, LeaderChanged, TeamFinalized, TeamNeedsReview, ProjectCreated, ProjectChanged, TeamLinkedToProject</td><td>Team (outbox)</td><td>Notification, Matching, Collaboration</td></tr>
<tr><td class=mono>exe.invitation</td><td>InvitationSent / Accepted / Declined / Expired, JoinRequestSubmitted / Accepted / Declined</td><td>Team (outbox)</td><td>Notification</td></tr>
<tr><td class=mono>exe.change-request</td><td>ChangeRequestSubmitted / Approved / Rejected</td><td>Team (outbox)</td><td>Notification</td></tr>
<tr><td class=mono>exe.assignment</td><td>TopicInterestOpened / Closed, LecturerPreferencesUpdated, LecturerAssigned, MentorAssigned, MentorUnassigned, AdvisorMatchingCompleted</td><td>Team (outbox)</td><td>Notification, Matching, Collaboration</td></tr>
<tr><td class=mono>exe.collab</td><td>AdvisorQuestionAsked / Answered, UserMentioned, TaskAssigned, Deliverable*, FileUploaded, Meeting*, MeetingSlotsPublished, MeetingSlotBooked, CheckpointReviewed</td><td>Collaboration (outbox)</td><td>Notification</td></tr>
<tr><td class=mono>exe.reminder</td><td>MeetingReminder, TaskDueSoon, DeliverableOverdue</td><td>Job (publish trực tiếp)</td><td>Notification</td></tr>
</tbody></table>
<h3>10.3 Transactional Outbox và consumer idempotent</h3><ul>
<li>Academic, Team, Collaboration có {C('outbox_messages')}: thay đổi nghiệp vụ + audit log + dòng outbox ghi trong một transaction.
{C('id')} là eventId của envelope; {C('aggregate_id')} làm Kafka key để giữ thứ tự theo đối tượng.</li>
<li>Dispatcher nền mỗi 1–2 giây: {C('... WHERE processed_at IS NULL ORDER BY occurred_at LIMIT 100 FOR UPDATE SKIP LOCKED')} →
publish → {C('processed_at')}; lỗi thì tăng {C('attempts')}, ghi {C('last_error')}.</li>
<li>Identity, Notification, Collaboration lưu eventId trong {C('processed_events')} cùng transaction với thay đổi; Notification còn có
{C('uq_notifications_event_recipient')}, {C('uq_email_logs_event_to')}. Lỗi sau khi retry → {C('<topic>.dlq')}.</li>
<li>Job không có outbox: event của nó không gắn với thay đổi dữ liệu nào, publish trực tiếp là đủ.</li></ul>"""


CHANGES = f"""<h2 class=break>11. Thay đổi so với bản 19/10/2026</h2>
<p>Tóm tắt; chi tiết từng dòng ở {C('deploy/db/README.md')} mục "Khác với PDF thiết kế".</p>
<table><thead><tr><th style='width:22%'>Chỗ</th><th>Thay đổi</th><th style='width:18%'>Quy tắc</th></tr></thead><tbody>
<tr><td>Database mới</td><td>{C('collab_db')} (role {C('collab_svc')}): workspace, tin nhắn, file, việc, họp, lịch tuần, checkpoint</td><td>§5.8, 21–25, 27</td></tr>
<tr><td>Vai trò tài khoản</td><td>{C('user_accounts.role_id')} → bảng {C('user_account_roles')}; thêm role Mentor</td><td>5, 20</td></tr>
<tr><td>Hồ sơ sinh viên</td><td>{C('student_skills')} (5 kỹ năng 1–5), {C('description')} ≤ 500, {C('profile_completed_at')}, {C('is_seeking_team')}</td><td>16</td></tr>
<tr><td>Giảng viên, mentor</td><td>{C('lecturer_domains')}, {C('max_teams_exe101 / _exe201')}; {C('mentors')} ({C('max_teams')}, {C('preferred_teams')}, {C('is_internal')}), {C('mentor_domains')}</td><td>9, 19, 20</td></tr>
<tr><td>Học kỳ, cấu hình</td><td>{C('mentoring_start_week')}, {C('expected_mentor_sessions')}, {C('slot_booking_deadline_week')}, {C('meeting_slot_minutes')}, {C('checkpoint_target_weeks')}; bảng {C('rule_settings')}</td><td>9, 17, 19, 20, 24, 25, 27</td></tr>
<tr><td>Trạng thái nhóm</td><td>FORMING / FINALIZED / DISBANDED → DRAFT / ACTIVE / NEEDS_REVIEW / FINALIZED / DISBANDED</td><td>13–15</td></tr>
<tr><td>Đề tài xuyên kỳ</td><td>{C('projects')}, {C('project_versions')}, {C('teams.project_id')} (thay {C('idea_title')}); item PROJECT_CHANGE</td><td>26</td></tr>
<tr><td>Mốc EXE101, đổi nhóm</td><td>{C('team_baseline_members')}, {C('teams.is_inherited')}; item chờ không cần change request; {C('round_id')}, số và % thay đổi</td><td>7</td></tr>
<tr><td>Ý tưởng</td><td>{C('join_requests.has_no_idea')}, {C('join_request_ideas')}</td><td>17</td></tr>
<tr><td>Ghép cố vấn</td><td>{C('topic_interests')}; {C('lecturer_preferences')} không trạng thái; {C('lecturer_assignments')} ASSIGNED / REVOKED + reason, source; {C('mentor_assignments')}; {C('advisor_matching_runs')}</td><td>9, 19, 20</td></tr>
<tr><td>Thông báo</td><td>{C('contacts.email_enabled')}; người nhận có thể là mentor</td><td>A07</td></tr>
</tbody></table>"""
