-- BE-01 – team_db (Team Service). Thiết kế: docs/EXE_Database_Design.pdf mục 6.
-- Chỗ khác PDF (theo PROJECT_CONTEXT): xem deploy/db/README.md.
\connect team_db

CREATE SCHEMA IF NOT EXISTS team AUTHORIZATION team_svc;
SET ROLE team_svc;
SET search_path = team, public;

CREATE OR REPLACE FUNCTION team.set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END $$;

-- ---------- Đề tài xuyên kỳ (rule 26) ----------
-- Đề tài tạo khi nhóm EXE101 đăng ký ý tưởng; nhóm EXE201 kế thừa trỏ cùng project_id.
CREATE TABLE team.projects (
    id                 uuid          NOT NULL DEFAULT gen_random_uuid(),
    name               varchar(200)  NOT NULL,
    description        varchar(4000),
    origin_semester_id uuid          NOT NULL,                -- academic.semesters.id
    origin_course_code varchar(6)    NOT NULL,
    current_version    smallint      NOT NULL DEFAULT 1,
    status             varchar(10)   NOT NULL DEFAULT 'ACTIVE',
    created_at         timestamptz   NOT NULL DEFAULT now(),
    updated_at         timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_projects PRIMARY KEY (id),
    CONSTRAINT ck_projects_course CHECK (origin_course_code IN ('EXE101', 'EXE201')),
    CONSTRAINT ck_projects_status CHECK (status IN ('ACTIVE', 'CLOSED')),
    CONSTRAINT ck_projects_name CHECK (char_length(btrim(name)) >= 2),
    CONSTRAINT ck_projects_version CHECK (current_version >= 1)
);
CREATE INDEX idx_projects_name ON team.projects (lower(name));

-- Lịch sử đề tài. PIVOT (đổi đề tài ở EXE201) đi qua yêu cầu đổi nhóm và cần lý do.
CREATE TABLE team.project_versions (
    project_id            uuid          NOT NULL,
    version               smallint      NOT NULL,
    name                  varchar(200)  NOT NULL,
    description           varchar(4000),
    change_type           varchar(10)   NOT NULL,
    reason                varchar(1000),
    semester_id           uuid          NOT NULL,
    course_code           varchar(6)    NOT NULL,
    change_request_id     uuid,
    changed_by_account_id uuid,                               -- NULL = hệ thống (import)
    created_at            timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_project_versions PRIMARY KEY (project_id, version),
    CONSTRAINT fk_project_versions_project FOREIGN KEY (project_id) REFERENCES team.projects (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_versions_type CHECK (change_type IN ('CREATED', 'EDITED', 'PIVOT')),
    CONSTRAINT ck_project_versions_pivot CHECK (change_type <> 'PIVOT' OR (reason IS NOT NULL AND change_request_id IS NOT NULL)),
    CONSTRAINT ck_project_versions_course CHECK (course_code IN ('EXE101', 'EXE201'))
);

-- ---------- Nhóm và thành viên (rule 1, 2, 4, 13, 15) ----------
-- status: DRAFT (< 4 người) -> ACTIVE -> FINALIZED, hoặc NEEDS_REVIEW sau hạn chót; DISBANDED khi giải tán.
CREATE TABLE team.teams (
    id               uuid          NOT NULL DEFAULT gen_random_uuid(),
    semester_id      uuid          NOT NULL,                 -- academic.semesters.id
    course_code      varchar(6)    NOT NULL,
    name             varchar(100)  NOT NULL,
    project_id       uuid,                                   -- đề tài (rule 26); NULL khi nhóm chưa đăng ký ý tưởng
    status           varchar(12)   NOT NULL DEFAULT 'DRAFT',
    member_count     smallint      NOT NULL DEFAULT 0,       -- trigger duy trì
    is_inherited     boolean       NOT NULL DEFAULT false,   -- nhóm EXE201 kế thừa từ mốc EXE101 (import)
    source_team_id   uuid,                                   -- nhóm EXE101 gốc (cùng đề tài) nếu có trong hệ thống
    is_auto_assigned boolean       NOT NULL DEFAULT false,
    finalized_at     timestamptz,
    created_at       timestamptz   NOT NULL DEFAULT now(),
    updated_at       timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_teams PRIMARY KEY (id),
    CONSTRAINT uq_teams_id_course UNIQUE (id, course_code),
    CONSTRAINT uq_teams_id_semester UNIQUE (id, semester_id),
    CONSTRAINT fk_teams_source_team FOREIGN KEY (source_team_id) REFERENCES team.teams (id) ON DELETE RESTRICT,
    CONSTRAINT fk_teams_project FOREIGN KEY (project_id) REFERENCES team.projects (id) ON DELETE RESTRICT,
    CONSTRAINT ck_teams_course CHECK (course_code IN ('EXE101', 'EXE201')),
    CONSTRAINT ck_teams_status CHECK (status IN ('DRAFT', 'ACTIVE', 'NEEDS_REVIEW', 'FINALIZED', 'DISBANDED')),
    CONSTRAINT ck_teams_member_count CHECK (member_count BETWEEN 0 AND 6),
    CONSTRAINT ck_teams_name CHECK (char_length(btrim(name)) >= 2),
    CONSTRAINT ck_teams_finalized_at CHECK (status <> 'FINALIZED' OR finalized_at IS NOT NULL),
    CONSTRAINT ck_teams_inherited CHECK ((NOT is_inherited AND source_team_id IS NULL) OR course_code = 'EXE201')
);
CREATE UNIQUE INDEX uq_teams_semester_name ON team.teams (semester_id, course_code, lower(name)) WHERE status <> 'DISBANDED';
CREATE UNIQUE INDEX uq_teams_source_team_id ON team.teams (source_team_id) WHERE source_team_id IS NOT NULL;
CREATE INDEX idx_teams_semester_status_name ON team.teams (semester_id, status, name);
-- Một đề tài chỉ có một nhóm đang hoạt động trong mỗi học kỳ.
CREATE UNIQUE INDEX uq_teams_project_semester ON team.teams (project_id, semester_id)
    WHERE project_id IS NOT NULL AND status <> 'DISBANDED';
CREATE INDEX idx_teams_semester_member_count ON team.teams (semester_id, member_count) WHERE status IN ('DRAFT', 'ACTIVE');

-- Thành viên hiện tại. UNIQUE (semester_id, student_id) = rule 4. Snapshot MSSV, tên, chuyên ngành.
CREATE TABLE team.team_members (
    id                uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id           uuid         NOT NULL,
    semester_id       uuid         NOT NULL,
    student_id        uuid         NOT NULL,                 -- academic.students.id
    student_code      varchar(8)   NOT NULL,
    full_name         varchar(150) NOT NULL,
    specialization_id integer      NOT NULL,
    role              varchar(10)  NOT NULL DEFAULT 'MEMBER',
    join_source       varchar(20)  NOT NULL,
    joined_at         timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_team_members PRIMARY KEY (id),
    CONSTRAINT uq_team_members_semester_student UNIQUE (semester_id, student_id),
    CONSTRAINT fk_team_members_team FOREIGN KEY (team_id, semester_id)
        REFERENCES team.teams (id, semester_id) ON DELETE CASCADE,
    CONSTRAINT ck_team_members_role CHECK (role IN ('LEADER', 'MEMBER')),
    CONSTRAINT ck_team_members_join_source CHECK (join_source IN
        ('CREATOR', 'INVITATION', 'JOIN_REQUEST', 'CHANGE_REQUEST', 'AUTO_ASSIGN', 'INHERITED', 'ADMIN')),
    CONSTRAINT ck_team_members_student_code CHECK (student_code ~ '^[A-Z]{2}[0-9]{6}$')
);
CREATE INDEX idx_team_members_team_id ON team.team_members (team_id);
CREATE UNIQUE INDEX uq_team_members_one_leader ON team.team_members (team_id) WHERE role = 'LEADER';

-- Mốc EXE101 của nhóm EXE201 kế thừa (import Excel, BE baseline-import). Tham chiếu cho yêu cầu đổi nhóm.
CREATE TABLE team.team_baseline_members (
    team_id           uuid         NOT NULL,
    student_id        uuid         NOT NULL,
    student_code      varchar(8)   NOT NULL,
    full_name         varchar(150) NOT NULL,
    specialization_id integer      NOT NULL,
    role              varchar(10)  NOT NULL,
    imported_at       timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_team_baseline_members PRIMARY KEY (team_id, student_id),
    CONSTRAINT fk_team_baseline_members_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_team_baseline_members_role CHECK (role IN ('LEADER', 'MEMBER')),
    CONSTRAINT ck_team_baseline_members_code CHECK (student_code ~ '^[A-Z]{2}[0-9]{6}$')
);
CREATE UNIQUE INDEX uq_team_baseline_members_leader ON team.team_baseline_members (team_id) WHERE role = 'LEADER';

-- Domain của nhóm (từ danh mục). domain_code snapshot để lọc ?domain=D04.
CREATE TABLE team.team_domains (
    team_id     uuid       NOT NULL,
    domain_id   integer    NOT NULL,                          -- academic.domains.id
    domain_code varchar(3) NOT NULL,
    is_primary  boolean    NOT NULL DEFAULT false,
    CONSTRAINT pk_team_domains PRIMARY KEY (team_id, domain_id),
    CONSTRAINT fk_team_domains_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_team_domains_code CHECK (domain_code ~ '^D(0[1-9]|1[0-8])$')
);
CREATE INDEX idx_team_domains_domain_code ON team.team_domains (domain_code);
CREATE UNIQUE INDEX uq_team_domains_primary ON team.team_domains (team_id) WHERE is_primary;

-- ---------- Lời mời, yêu cầu tham gia (rule 8: hết hạn 72 giờ) ----------
CREATE TABLE team.invitations (
    id                    uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id               uuid         NOT NULL,
    invitee_student_id    uuid         NOT NULL,
    invited_by_student_id uuid         NOT NULL,             -- leader
    message               varchar(500),
    status                varchar(10)  NOT NULL DEFAULT 'PENDING',
    created_at            timestamptz  NOT NULL DEFAULT now(),
    expires_at            timestamptz  NOT NULL DEFAULT now() + interval '72 hours',
    resolved_at           timestamptz,
    CONSTRAINT pk_invitations PRIMARY KEY (id),
    CONSTRAINT fk_invitations_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_invitations_status CHECK (status IN ('PENDING', 'ACCEPTED', 'DECLINED', 'EXPIRED', 'CANCELLED')),
    CONSTRAINT ck_invitations_resolved CHECK ((status = 'PENDING') = (resolved_at IS NULL)),
    CONSTRAINT ck_invitations_expiry CHECK (expires_at > created_at),
    CONSTRAINT ck_invitations_self CHECK (invitee_student_id <> invited_by_student_id)
);
CREATE UNIQUE INDEX uq_invitations_pending ON team.invitations (team_id, invitee_student_id) WHERE status = 'PENDING';
CREATE INDEX idx_invitations_invitee ON team.invitations (invitee_student_id, status);
CREATE INDEX idx_invitations_expiry ON team.invitations (expires_at) WHERE status = 'PENDING';

CREATE TABLE team.join_requests (
    id                     uuid        NOT NULL DEFAULT gen_random_uuid(),
    team_id                uuid        NOT NULL,
    student_id             uuid        NOT NULL,
    message                varchar(500),
    has_no_idea            boolean     NOT NULL DEFAULT false, -- tích "Chưa có ý tưởng" (rule 17)
    status                 varchar(10) NOT NULL DEFAULT 'PENDING',
    resolved_by_student_id uuid,                              -- leader duyệt
    created_at             timestamptz NOT NULL DEFAULT now(),
    expires_at             timestamptz NOT NULL DEFAULT now() + interval '72 hours',
    resolved_at            timestamptz,
    CONSTRAINT pk_join_requests PRIMARY KEY (id),
    CONSTRAINT fk_join_requests_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_join_requests_status CHECK (status IN ('PENDING', 'ACCEPTED', 'DECLINED', 'EXPIRED', 'CANCELLED')),
    CONSTRAINT ck_join_requests_resolved CHECK ((status = 'PENDING') = (resolved_at IS NULL)),
    CONSTRAINT ck_join_requests_expiry CHECK (expires_at > created_at)
);
CREATE UNIQUE INDEX uq_join_requests_pending ON team.join_requests (team_id, student_id) WHERE status = 'PENDING';
CREATE INDEX idx_join_requests_student ON team.join_requests (student_id, status);
CREATE INDEX idx_join_requests_expiry ON team.join_requests (expires_at) WHERE status = 'PENDING';

-- Ý tưởng kèm yêu cầu tham gia (rule 17): 1–3 ý tưởng hoặc has_no_idea = true, kiểm tra lúc COMMIT.
CREATE TABLE team.join_request_ideas (
    join_request_id uuid         NOT NULL,
    position        smallint     NOT NULL,
    title           varchar(80)  NOT NULL,
    description     varchar(300),
    CONSTRAINT pk_join_request_ideas PRIMARY KEY (join_request_id, position),
    CONSTRAINT fk_join_request_ideas_request FOREIGN KEY (join_request_id)
        REFERENCES team.join_requests (id) ON DELETE CASCADE,
    CONSTRAINT ck_join_request_ideas_position CHECK (position BETWEEN 1 AND 3),
    CONSTRAINT ck_join_request_ideas_title CHECK (char_length(btrim(title)) >= 1)
);

-- ---------- Bài tuyển thành viên, bài tìm nhóm ----------
CREATE TABLE team.recruitment_posts (
    id          uuid          NOT NULL DEFAULT gen_random_uuid(),
    team_id     uuid          NOT NULL,
    title       varchar(150)  NOT NULL,
    description varchar(2000),
    status      varchar(10)   NOT NULL DEFAULT 'OPEN',
    created_at  timestamptz   NOT NULL DEFAULT now(),
    updated_at  timestamptz   NOT NULL DEFAULT now(),
    closed_at   timestamptz,
    CONSTRAINT pk_recruitment_posts PRIMARY KEY (id),
    CONSTRAINT fk_recruitment_posts_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_recruitment_posts_status CHECK (status IN ('OPEN', 'CLOSED')),
    CONSTRAINT ck_recruitment_posts_closed CHECK ((status = 'CLOSED') = (closed_at IS NOT NULL))
);
CREATE UNIQUE INDEX uq_recruitment_posts_open ON team.recruitment_posts (team_id) WHERE status = 'OPEN';

CREATE TABLE team.recruitment_post_specializations (
    recruitment_post_id uuid    NOT NULL,
    specialization_id   integer NOT NULL,
    CONSTRAINT pk_recruitment_post_specializations PRIMARY KEY (recruitment_post_id, specialization_id),
    CONSTRAINT fk_rps_post FOREIGN KEY (recruitment_post_id) REFERENCES team.recruitment_posts (id) ON DELETE CASCADE
);
CREATE INDEX idx_rps_specialization_id ON team.recruitment_post_specializations (specialization_id);

CREATE TABLE team.seeking_posts (
    id                uuid          NOT NULL DEFAULT gen_random_uuid(),
    semester_id       uuid          NOT NULL,
    student_id        uuid          NOT NULL,
    specialization_id integer       NOT NULL,
    title             varchar(150)  NOT NULL,
    description       varchar(2000),
    status            varchar(10)   NOT NULL DEFAULT 'OPEN',
    created_at        timestamptz   NOT NULL DEFAULT now(),
    updated_at        timestamptz   NOT NULL DEFAULT now(),
    closed_at         timestamptz,
    CONSTRAINT pk_seeking_posts PRIMARY KEY (id),
    CONSTRAINT ck_seeking_posts_status CHECK (status IN ('OPEN', 'CLOSED')),
    CONSTRAINT ck_seeking_posts_closed CHECK ((status = 'CLOSED') = (closed_at IS NOT NULL))
);
CREATE UNIQUE INDEX uq_seeking_posts_open ON team.seeking_posts (semester_id, student_id) WHERE status = 'OPEN';
CREATE INDEX idx_seeking_posts_semester_spec ON team.seeking_posts (semester_id, specialization_id) WHERE status = 'OPEN';

CREATE TABLE team.seeking_post_domains (
    seeking_post_id uuid       NOT NULL,
    domain_id       integer    NOT NULL,
    domain_code     varchar(3) NOT NULL,
    CONSTRAINT pk_seeking_post_domains PRIMARY KEY (seeking_post_id, domain_id),
    CONSTRAINT fk_spd_post FOREIGN KEY (seeking_post_id) REFERENCES team.seeking_posts (id) ON DELETE CASCADE,
    CONSTRAINT ck_spd_code CHECK (domain_code ~ '^D(0[1-9]|1[0-8])$')
);

-- ---------- Yêu cầu đổi nhóm, chỉ EXE201 (rule 7) ----------
CREATE TABLE team.change_requests (
    id                      uuid          NOT NULL DEFAULT gen_random_uuid(),
    team_id                 uuid          NOT NULL,
    course_code             varchar(6)    NOT NULL DEFAULT 'EXE201',
    round_id                uuid          NOT NULL,           -- academic.registration_rounds.id (đợt đổi nhóm)
    requested_by_student_id uuid          NOT NULL,
    reason                  varchar(1000) NOT NULL,
    changed_member_count    smallint      NOT NULL,           -- hệ thống tự tính so với mốc EXE101
    changed_percent         numeric(5, 2) NOT NULL,
    status                  varchar(10)   NOT NULL DEFAULT 'PENDING',
    reviewed_by_account_id  uuid,                             -- Admin (identity)
    reviewed_at             timestamptz,
    review_note             varchar(1000),
    created_at              timestamptz   NOT NULL DEFAULT now(),
    updated_at              timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_change_requests PRIMARY KEY (id),
    CONSTRAINT fk_change_requests_team FOREIGN KEY (team_id, course_code)
        REFERENCES team.teams (id, course_code) ON DELETE RESTRICT,
    CONSTRAINT ck_change_requests_course CHECK (course_code = 'EXE201'),
    CONSTRAINT ck_change_requests_reason CHECK (char_length(btrim(reason)) >= 10),
    CONSTRAINT ck_change_requests_counts CHECK (changed_member_count >= 0 AND changed_percent BETWEEN 0 AND 100),
    CONSTRAINT ck_change_requests_status CHECK (status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')),
    CONSTRAINT ck_change_requests_review CHECK (
        (status IN ('APPROVED', 'REJECTED')) = (reviewed_by_account_id IS NOT NULL AND reviewed_at IS NOT NULL)),
    CONSTRAINT ck_change_requests_reject_note CHECK (status <> 'REJECTED' OR review_note IS NOT NULL)
);
CREATE UNIQUE INDEX uq_change_requests_pending ON team.change_requests (team_id) WHERE status = 'PENDING';
CREATE INDEX idx_change_requests_status_created ON team.change_requests (status, created_at DESC);
CREATE INDEX idx_change_requests_round ON team.change_requests (round_id, status);

-- Thay đổi chờ duyệt của nhóm kế thừa. change_request_id NULL = đã được hai bên đồng ý nhưng chưa đưa vào yêu cầu.
CREATE TABLE team.change_request_items (
    id                uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id           uuid         NOT NULL,
    change_request_id uuid,
    action            varchar(14)  NOT NULL,
    source            varchar(20)  NOT NULL,
    student_id        uuid,                                   -- NULL khi PROJECT_CHANGE
    student_code      varchar(8),
    full_name         varchar(150),
    specialization_id integer,
    new_project_name  varchar(200),                           -- PROJECT_CHANGE (rule 26)
    new_project_description varchar(4000),
    created_at        timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_change_request_items PRIMARY KEY (id),
    CONSTRAINT fk_change_request_items_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT fk_change_request_items_request FOREIGN KEY (change_request_id)
        REFERENCES team.change_requests (id) ON DELETE CASCADE,
    CONSTRAINT ck_change_request_items_action CHECK (action IN ('ADD', 'REMOVE', 'SET_LEADER', 'PROJECT_CHANGE')),
    CONSTRAINT ck_change_request_items_source CHECK (source IN
        ('INVITATION', 'JOIN_REQUEST', 'LEAVE', 'REMOVE', 'LEADER_TRANSFER', 'PROJECT')),
    CONSTRAINT ck_change_request_items_spec CHECK (action <> 'ADD' OR specialization_id IS NOT NULL),
    CONSTRAINT ck_change_request_items_code CHECK (student_code IS NULL OR student_code ~ '^[A-Z]{2}[0-9]{6}$'),
    CONSTRAINT ck_change_request_items_target CHECK (
        (action = 'PROJECT_CHANGE' AND source = 'PROJECT' AND student_id IS NULL AND new_project_name IS NOT NULL)
        OR (action <> 'PROJECT_CHANGE' AND source <> 'PROJECT' AND student_id IS NOT NULL AND student_code IS NOT NULL
            AND full_name IS NOT NULL AND new_project_name IS NULL))
);
CREATE UNIQUE INDEX uq_change_request_items_pending ON team.change_request_items (team_id, student_id, action)
    WHERE change_request_id IS NULL;
CREATE UNIQUE INDEX uq_change_request_items_request ON team.change_request_items (change_request_id, student_id, action)
    WHERE change_request_id IS NOT NULL;
CREATE INDEX idx_change_request_items_student_id ON team.change_request_items (student_id);
-- Mỗi nhóm tối đa một thay đổi đề tài đang chờ.
CREATE UNIQUE INDEX uq_change_request_items_project_pending ON team.change_request_items (team_id)
    WHERE action = 'PROJECT_CHANGE' AND change_request_id IS NULL;

-- ---------- Quan tâm của cố vấn (rule 19) ----------
-- Giảng viên hoặc mentor bày tỏ muốn nhận nhóm. Không giữ chỗ; là một đầu vào của ghép tự động (rule 9, 20).
-- Giới hạn số quan tâm kiểm tra ở Services. CLOSED khi nhóm đã được ghép cố vấn cùng loại, WITHDRAWN khi người đó tự rút.
CREATE TABLE team.topic_interests (
    id            uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id       uuid         NOT NULL,
    advisor_kind  varchar(10)  NOT NULL,                      -- LECTURER | MENTOR
    advisor_id    uuid         NOT NULL,                      -- academic.lecturers.id hoặc academic.mentors.id
    advisor_name  varchar(150) NOT NULL,                      -- snapshot
    status        varchar(10)  NOT NULL DEFAULT 'OPEN',
    closed_reason varchar(30),
    created_at    timestamptz  NOT NULL DEFAULT now(),
    closed_at     timestamptz,
    CONSTRAINT pk_topic_interests PRIMARY KEY (id),
    CONSTRAINT fk_topic_interests_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_topic_interests_kind CHECK (advisor_kind IN ('LECTURER', 'MENTOR')),
    CONSTRAINT ck_topic_interests_status CHECK (status IN ('OPEN', 'WITHDRAWN', 'CLOSED')),
    CONSTRAINT ck_topic_interests_closed CHECK ((status = 'OPEN') = (closed_at IS NULL)),
    CONSTRAINT ck_topic_interests_reason CHECK (
        (status = 'CLOSED') = (closed_reason IS NOT NULL)
        AND (closed_reason IS NULL OR closed_reason IN ('THIS_ADVISOR_MATCHED', 'OTHER_ADVISOR_MATCHED', 'TEAM_DISBANDED')))
);
CREATE UNIQUE INDEX uq_topic_interests_open ON team.topic_interests (team_id, advisor_kind, advisor_id) WHERE status = 'OPEN';
CREATE INDEX idx_topic_interests_advisor ON team.topic_interests (advisor_kind, advisor_id, status);

-- ---------- Nguyện vọng giảng viên của nhóm (rule 9) ----------
-- Tối đa 3 (rule_settings.max_lecturer_preferences, kiểm tra ở Services), có thứ tự. Không có trạng thái:
-- trưởng nhóm sửa được tới khi nhóm được ghép giảng viên, sau đó khóa (Services kiểm tra). Không còn bước chấp nhận / từ chối.
CREATE TABLE team.lecturer_preferences (
    team_id               uuid         NOT NULL,
    rank                  smallint     NOT NULL,
    lecturer_id           uuid         NOT NULL,              -- academic.lecturers.id
    lecturer_name         varchar(150) NOT NULL,              -- snapshot
    updated_by_student_id uuid         NOT NULL,
    updated_at            timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_lecturer_preferences PRIMARY KEY (team_id, rank),
    CONSTRAINT uq_lecturer_preferences_lecturer UNIQUE (team_id, lecturer_id),
    CONSTRAINT fk_lecturer_preferences_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_lecturer_preferences_rank CHECK (rank BETWEEN 1 AND 10)
);
CREATE INDEX idx_lecturer_preferences_lecturer ON team.lecturer_preferences (lecturer_id);

-- ---------- Lần chạy ghép cố vấn (rule 9, 20) ----------
-- Mỗi lần ghép (tự động lúc hết hạn / tuần mentor, hoặc Admin bấm "Ghép sớm") là một dòng.
-- PREVIEW: kết quả xem trước nằm trong plan; APPLIED: đã ghi vào *_assignments; DISCARDED: Admin bỏ.
CREATE TABLE team.advisor_matching_runs (
    id                      uuid        NOT NULL DEFAULT gen_random_uuid(),
    advisor_kind            varchar(10) NOT NULL,
    semester_id             uuid        NOT NULL,
    course_code             varchar(6)  NOT NULL,
    trigger                 varchar(10) NOT NULL,
    status                  varchar(10) NOT NULL DEFAULT 'PREVIEW',
    requested_by_account_id uuid,                              -- Admin khi Ghép sớm; NULL khi Job chạy
    plan                    jsonb       NOT NULL DEFAULT '[]', -- [{teamId, advisorId, reason, preferenceRank}]
    matched_count           integer     NOT NULL DEFAULT 0,
    unmatched_count         integer     NOT NULL DEFAULT 0,
    created_at              timestamptz NOT NULL DEFAULT now(),
    applied_at              timestamptz,
    CONSTRAINT pk_advisor_matching_runs PRIMARY KEY (id),
    CONSTRAINT ck_advisor_matching_runs_kind CHECK (advisor_kind IN ('LECTURER', 'MENTOR')),
    CONSTRAINT ck_advisor_matching_runs_course CHECK (course_code IN ('EXE101', 'EXE201')),
    CONSTRAINT ck_advisor_matching_runs_trigger CHECK (trigger IN ('SCHEDULED', 'EARLY')),
    CONSTRAINT ck_advisor_matching_runs_status CHECK (status IN ('PREVIEW', 'APPLIED', 'DISCARDED')),
    CONSTRAINT ck_advisor_matching_runs_applied CHECK ((status = 'APPLIED') = (applied_at IS NOT NULL)),
    CONSTRAINT ck_advisor_matching_runs_requester CHECK (trigger = 'SCHEDULED' OR requested_by_account_id IS NOT NULL)
);
CREATE INDEX idx_advisor_matching_runs_semester ON team.advisor_matching_runs (semester_id, course_code, advisor_kind);

-- ---------- Giảng viên hướng dẫn của nhóm (rule 9) ----------
-- Một giảng viên hướng dẫn đang hiệu lực mỗi nhóm. reason là nhãn hiện cho nhóm, giảng viên và Admin.
CREATE TABLE team.lecturer_assignments (
    id                     uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id                uuid         NOT NULL,
    lecturer_id            uuid         NOT NULL,
    lecturer_name          varchar(150) NOT NULL,             -- snapshot
    status                 varchar(10)  NOT NULL DEFAULT 'ASSIGNED',
    reason                 varchar(20)  NOT NULL,
    preference_rank        smallint,                          -- với MUTUAL / PREFERENCE: nguyện vọng thứ mấy
    source                 varchar(10)  NOT NULL DEFAULT 'AUTO',
    matching_run_id        uuid,
    assigned_by_account_id uuid,                              -- Admin khi MANUAL
    assigned_at            timestamptz  NOT NULL DEFAULT now(),
    revoked_at             timestamptz,
    note                   varchar(500),                      -- lý do khi Admin đổi tay
    CONSTRAINT pk_lecturer_assignments PRIMARY KEY (id),
    CONSTRAINT fk_lecturer_assignments_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE RESTRICT,
    CONSTRAINT fk_lecturer_assignments_run FOREIGN KEY (matching_run_id) REFERENCES team.advisor_matching_runs (id) ON DELETE SET NULL,
    CONSTRAINT ck_lecturer_assignments_status CHECK (status IN ('ASSIGNED', 'REVOKED')),
    CONSTRAINT ck_lecturer_assignments_revoked CHECK ((status = 'REVOKED') = (revoked_at IS NOT NULL)),
    CONSTRAINT ck_lecturer_assignments_reason CHECK (reason IN
        ('MUTUAL', 'PREFERENCE', 'LECTURER_INTEREST', 'PREVIOUS_INSTRUCTOR', 'DOMAIN', 'MANUAL')),
    CONSTRAINT ck_lecturer_assignments_rank CHECK (
        (reason IN ('MUTUAL', 'PREFERENCE')) = (preference_rank IS NOT NULL)),
    CONSTRAINT ck_lecturer_assignments_source CHECK (
        (source = 'AUTO' AND reason <> 'MANUAL')
        OR (source = 'MANUAL' AND reason = 'MANUAL' AND assigned_by_account_id IS NOT NULL AND note IS NOT NULL))
);
CREATE UNIQUE INDEX uq_lecturer_assignments_active ON team.lecturer_assignments (team_id) WHERE status = 'ASSIGNED';
CREATE INDEX idx_lecturer_assignments_lecturer ON team.lecturer_assignments (lecturer_id, status);

-- ---------- Mentor của nhóm (rule 20) ----------
-- Một mentor đang hiệu lực mỗi nhóm; ghép tự động ở tuần bắt đầu mentor theo quan tâm của mentor.
CREATE TABLE team.mentor_assignments (
    id                     uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id                uuid         NOT NULL,
    mentor_id              uuid         NOT NULL,             -- academic.mentors.id
    mentor_name            varchar(150) NOT NULL,             -- snapshot
    status                 varchar(10)  NOT NULL DEFAULT 'ASSIGNED',
    reason                 varchar(20)  NOT NULL,
    source                 varchar(10)  NOT NULL DEFAULT 'AUTO',
    matching_run_id        uuid,
    assigned_by_account_id uuid,                              -- Admin khi MANUAL
    assigned_at            timestamptz  NOT NULL DEFAULT now(),
    ended_at               timestamptz,
    end_reason             varchar(500),
    note                   varchar(500),                      -- lý do khi Admin đổi tay
    CONSTRAINT pk_mentor_assignments PRIMARY KEY (id),
    CONSTRAINT fk_mentor_assignments_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE RESTRICT,
    CONSTRAINT fk_mentor_assignments_run FOREIGN KEY (matching_run_id) REFERENCES team.advisor_matching_runs (id) ON DELETE SET NULL,
    CONSTRAINT ck_mentor_assignments_status CHECK (status IN ('ASSIGNED', 'ENDED')),
    CONSTRAINT ck_mentor_assignments_ended CHECK ((status = 'ENDED') = (ended_at IS NOT NULL)),
    CONSTRAINT ck_mentor_assignments_reason CHECK (reason IN ('MENTOR_INTEREST', 'DOMAIN', 'MANUAL')),
    CONSTRAINT ck_mentor_assignments_source CHECK (
        (source = 'AUTO' AND reason <> 'MANUAL')
        OR (source = 'MANUAL' AND reason = 'MANUAL' AND assigned_by_account_id IS NOT NULL AND note IS NOT NULL))
);
CREATE UNIQUE INDEX uq_mentor_assignments_active ON team.mentor_assignments (team_id) WHERE status = 'ASSIGNED';
CREATE INDEX idx_mentor_assignments_mentor ON team.mentor_assignments (mentor_id, status);

-- ---------- Nhật ký (rule 10), Outbox ----------
CREATE TABLE team.audit_logs (
    id               bigint        GENERATED ALWAYS AS IDENTITY,
    team_id          uuid,
    entity_type      varchar(50)   NOT NULL,
    entity_id        uuid          NOT NULL,
    action           varchar(50)   NOT NULL,
    actor_account_id uuid,                                    -- NULL = hệ thống / job
    actor_role       varchar(20)   NOT NULL,
    old_values       jsonb,
    new_values       jsonb,
    reason           varchar(1000),
    correlation_id   varchar(64),
    occurred_at      timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_audit_logs PRIMARY KEY (id),
    CONSTRAINT ck_audit_logs_actor_role CHECK (actor_role IN ('Student', 'Lecturer', 'Mentor', 'Admin', 'System'))
);
CREATE INDEX idx_audit_logs_team_time ON team.audit_logs (team_id, occurred_at DESC);
CREATE INDEX idx_audit_logs_entity ON team.audit_logs (entity_type, entity_id);
CREATE INDEX idx_audit_logs_actor ON team.audit_logs (actor_account_id);

CREATE TABLE team.outbox_messages (
    id             uuid         NOT NULL,                     -- = eventId của envelope
    topic          varchar(100) NOT NULL,
    event_type     varchar(100) NOT NULL,
    aggregate_type varchar(50)  NOT NULL,
    aggregate_id   uuid         NOT NULL,                     -- Kafka message key
    payload        jsonb        NOT NULL,
    correlation_id varchar(64),
    occurred_at    timestamptz  NOT NULL DEFAULT now(),
    processed_at   timestamptz,
    attempts       integer      NOT NULL DEFAULT 0,
    last_error     text,
    CONSTRAINT pk_outbox_messages PRIMARY KEY (id),
    CONSTRAINT ck_outbox_messages_attempts CHECK (attempts >= 0)
);
CREATE INDEX idx_outbox_messages_pending ON team.outbox_messages (occurred_at) WHERE processed_at IS NULL;

-- ---------- Trigger ----------
CREATE TRIGGER trg_teams_updated_at BEFORE UPDATE ON team.teams FOR EACH ROW EXECUTE FUNCTION team.set_updated_at();
CREATE TRIGGER trg_projects_updated_at BEFORE UPDATE ON team.projects FOR EACH ROW EXECUTE FUNCTION team.set_updated_at();
CREATE TRIGGER trg_recruitment_posts_updated_at BEFORE UPDATE ON team.recruitment_posts FOR EACH ROW EXECUTE FUNCTION team.set_updated_at();
CREATE TRIGGER trg_seeking_posts_updated_at BEFORE UPDATE ON team.seeking_posts FOR EACH ROW EXECUTE FUNCTION team.set_updated_at();
CREATE TRIGGER trg_change_requests_updated_at BEFORE UPDATE ON team.change_requests FOR EACH ROW EXECUTE FUNCTION team.set_updated_at();

-- member_count luôn khớp số dòng team_members; CHECK ≤ 6 chặn người thứ 7 ngay khi INSERT.
CREATE OR REPLACE FUNCTION team.tg_team_members_count() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE team.teams SET member_count = member_count + 1 WHERE id = NEW.team_id;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE team.teams SET member_count = member_count - 1 WHERE id = OLD.team_id;
    END IF;
    RETURN NULL;
END $$;
CREATE TRIGGER trg_team_members_count AFTER INSERT OR DELETE ON team.team_members
    FOR EACH ROW EXECUTE FUNCTION team.tg_team_members_count();

-- Không cho chuyển một dòng thành viên sang nhóm / sinh viên khác: xóa rồi thêm mới.
CREATE OR REPLACE FUNCTION team.tg_team_members_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.team_id <> OLD.team_id OR NEW.semester_id <> OLD.semester_id OR NEW.student_id <> OLD.student_id THEN
        RAISE EXCEPTION 'team_members: team_id, semester_id, student_id không được sửa'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_team_members_immutable';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_team_members_immutable BEFORE UPDATE ON team.team_members
    FOR EACH ROW EXECUTE FUNCTION team.tg_team_members_immutable();

-- Kiểm tra cơ cấu nhóm lúc COMMIT (deferred): đúng 1 leader khi có thành viên (rule 1);
-- khi FINALIZED: 4–6 người và ≥ 2 chuyên ngành (rule 1, 2 – hard block).
CREATE OR REPLACE FUNCTION team.check_team_composition(p_team_id uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_status  varchar(12);
    v_members integer;
    v_leaders integer;
    v_specs   integer;
BEGIN
    SELECT status INTO v_status FROM team.teams WHERE id = p_team_id;
    IF NOT FOUND OR v_status = 'DISBANDED' THEN
        RETURN;
    END IF;
    SELECT count(*), count(*) FILTER (WHERE role = 'LEADER'), count(DISTINCT specialization_id)
      INTO v_members, v_leaders, v_specs
      FROM team.team_members WHERE team_id = p_team_id;
    IF v_members > 0 AND v_leaders <> 1 THEN
        RAISE EXCEPTION 'Nhóm % phải có đúng 1 trưởng nhóm (đang có %)', p_team_id, v_leaders
            USING ERRCODE = '23514', CONSTRAINT = 'ck_team_exactly_one_leader';
    END IF;
    IF v_status = 'FINALIZED' THEN
        IF v_members NOT BETWEEN 4 AND 6 THEN
            RAISE EXCEPTION 'Nhóm % đã chốt phải có 4–6 thành viên (đang có %)', p_team_id, v_members
                USING ERRCODE = '23514', CONSTRAINT = 'ck_team_finalized_size';
        END IF;
        IF v_specs < 2 THEN
            RAISE EXCEPTION 'Nhóm % đã chốt phải có ít nhất 2 chuyên ngành (đang có %)', p_team_id, v_specs
                USING ERRCODE = '23514', CONSTRAINT = 'ck_team_finalized_specializations';
        END IF;
    END IF;
END $$;

CREATE OR REPLACE FUNCTION team.tg_team_members_composition() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM team.check_team_composition(CASE WHEN TG_OP = 'DELETE' THEN OLD.team_id ELSE NEW.team_id END);
    RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER trg_team_members_composition AFTER INSERT OR UPDATE OR DELETE ON team.team_members
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION team.tg_team_members_composition();

CREATE OR REPLACE FUNCTION team.tg_teams_composition() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM team.check_team_composition(NEW.id);
    RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER trg_teams_composition AFTER INSERT OR UPDATE OF status ON team.teams
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION team.tg_teams_composition();

-- Rule 17: yêu cầu tham gia có 1–3 ý tưởng hoặc has_no_idea = true (không có cả hai). Kiểm tra lúc COMMIT
-- vì ý tưởng được thêm sau dòng join_requests trong cùng transaction.
CREATE OR REPLACE FUNCTION team.check_join_request_ideas(p_request_id uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_no_idea boolean;
    v_ideas   integer;
BEGIN
    SELECT has_no_idea INTO v_no_idea FROM team.join_requests WHERE id = p_request_id;
    IF NOT FOUND THEN
        RETURN;
    END IF;
    SELECT count(*) INTO v_ideas FROM team.join_request_ideas WHERE join_request_id = p_request_id;
    IF v_no_idea = (v_ideas > 0) THEN
        RAISE EXCEPTION 'Yêu cầu % cần 1–3 ý tưởng hoặc chọn "Chưa có ý tưởng" (đang có % ý tưởng)', p_request_id, v_ideas
            USING ERRCODE = '23514', CONSTRAINT = 'ck_join_requests_ideas';
    END IF;
END $$;

CREATE OR REPLACE FUNCTION team.tg_join_requests_ideas() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_TABLE_NAME = 'join_requests' THEN
        PERFORM team.check_join_request_ideas(NEW.id);
    ELSE
        PERFORM team.check_join_request_ideas(CASE WHEN TG_OP = 'DELETE' THEN OLD.join_request_id ELSE NEW.join_request_id END);
    END IF;
    RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER trg_join_requests_ideas AFTER INSERT OR UPDATE OF has_no_idea ON team.join_requests
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION team.tg_join_requests_ideas();
CREATE CONSTRAINT TRIGGER trg_join_request_ideas_check AFTER INSERT OR DELETE ON team.join_request_ideas
    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION team.tg_join_requests_ideas();

-- audit_logs chỉ được thêm, không sửa / xóa.
CREATE OR REPLACE FUNCTION team.tg_audit_logs_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'audit_logs chỉ được thêm, không được sửa hoặc xóa'
        USING ERRCODE = '42501', CONSTRAINT = 'ck_audit_logs_append_only';
END $$;
CREATE TRIGGER trg_audit_logs_append_only BEFORE UPDATE OR DELETE ON team.audit_logs
    FOR EACH ROW EXECUTE FUNCTION team.tg_audit_logs_append_only();

RESET ROLE;
