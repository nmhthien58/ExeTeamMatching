-- BE-60 – collab_db (Collaboration Service): workspace nhóm gồm tin nhắn, việc / bài nộp, file, lịch họp.
-- Quy tắc: PROJECT_CONTEXT §3 rule 21–26, §5.8. Người dùng được định danh bằng (person_type, person_id):
-- person_id = academic students.id / lecturers.id / mentors.id (cùng giá trị claim student_id / lecturer_id / mentor_id trong JWT).
\connect collab_db

CREATE SCHEMA IF NOT EXISTS collab AUTHORIZATION collab_svc;
SET ROLE collab_svc;
SET search_path = collab, public;

CREATE OR REPLACE FUNCTION collab.set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END $$;

-- ---------- Workspace và thành viên (rule 21) ----------
-- Một workspace cho mỗi nhóm, tạo khi consume TeamCreated. previous_workspace_id = workspace EXE101 cùng đề tài (rule 26).
CREATE TABLE collab.workspaces (
    id                    uuid         NOT NULL DEFAULT gen_random_uuid(),
    team_id               uuid         NOT NULL,              -- team.teams.id
    project_id            uuid,                               -- team.projects.id
    semester_id           uuid         NOT NULL,
    course_code           varchar(6)   NOT NULL,
    team_name             varchar(100) NOT NULL,              -- snapshot
    previous_workspace_id uuid,
    created_at            timestamptz  NOT NULL DEFAULT now(),
    archived_at           timestamptz,
    CONSTRAINT pk_workspaces PRIMARY KEY (id),
    CONSTRAINT uq_workspaces_team UNIQUE (team_id),
    CONSTRAINT fk_workspaces_previous FOREIGN KEY (previous_workspace_id)
        REFERENCES collab.workspaces (id) ON DELETE SET NULL,
    CONSTRAINT ck_workspaces_course CHECK (course_code IN ('EXE101', 'EXE201')),
    CONSTRAINT ck_workspaces_previous_self CHECK (previous_workspace_id IS NULL OR previous_workspace_id <> id)
);
CREATE INDEX idx_workspaces_project ON collab.workspaces (project_id);
CREATE INDEX idx_workspaces_previous ON collab.workspaces (previous_workspace_id);

-- Read model quyền truy cập, dựng từ Kafka (MemberJoined, MemberLeft, LeaderChanged, LecturerAssigned, MentorAssigned…).
-- Collaboration không gọi Team để kiểm tra quyền. Dòng không bị xóa: rời nhóm thì ghi left_at.
CREATE TABLE collab.workspace_members (
    workspace_id uuid         NOT NULL,
    person_type  varchar(10)  NOT NULL,
    person_id    uuid         NOT NULL,
    display_name varchar(150) NOT NULL,
    role         varchar(10)  NOT NULL,
    joined_at    timestamptz  NOT NULL DEFAULT now(),
    left_at      timestamptz,
    CONSTRAINT pk_workspace_members PRIMARY KEY (workspace_id, person_type, person_id),
    CONSTRAINT fk_workspace_members_workspace FOREIGN KEY (workspace_id)
        REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT ck_workspace_members_person_type CHECK (person_type IN ('STUDENT', 'LECTURER', 'MENTOR')),
    CONSTRAINT ck_workspace_members_role CHECK (role IN ('LEADER', 'MEMBER', 'INSTRUCTOR', 'MENTOR')),
    CONSTRAINT ck_workspace_members_role_type CHECK (
        (role IN ('LEADER', 'MEMBER') AND person_type = 'STUDENT')
        OR (role = 'INSTRUCTOR' AND person_type = 'LECTURER')
        OR (role = 'MENTOR' AND person_type IN ('MENTOR', 'LECTURER'))),
    CONSTRAINT ck_workspace_members_left CHECK (left_at IS NULL OR left_at >= joined_at)
);
CREATE INDEX idx_workspace_members_person ON collab.workspace_members (person_type, person_id) WHERE left_at IS NULL;
-- Rule 20: mỗi nhóm một mentor; mỗi nhóm một giảng viên hướng dẫn chính thức.
CREATE UNIQUE INDEX uq_workspace_members_one_mentor ON collab.workspace_members (workspace_id)
    WHERE role = 'MENTOR' AND left_at IS NULL;
CREATE UNIQUE INDEX uq_workspace_members_one_instructor ON collab.workspace_members (workspace_id)
    WHERE role = 'INSTRUCTOR' AND left_at IS NULL;

-- ---------- Tin nhắn (rule 22) ----------
CREATE TABLE collab.channels (
    id           uuid        NOT NULL DEFAULT gen_random_uuid(),
    workspace_id uuid        NOT NULL,
    kind         varchar(10) NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_channels PRIMARY KEY (id),
    CONSTRAINT uq_channels_workspace_kind UNIQUE (workspace_id, kind),
    CONSTRAINT fk_channels_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT ck_channels_kind CHECK (kind IN ('TEAM', 'ADVISORS'))     -- Nhóm / Cố vấn
);

CREATE TABLE collab.messages (
    id                  uuid          NOT NULL DEFAULT gen_random_uuid(),
    channel_id          uuid          NOT NULL,
    parent_message_id   uuid,                                  -- trả lời trong luồng
    author_person_type  varchar(10)   NOT NULL,
    author_person_id    uuid          NOT NULL,
    author_name         varchar(150)  NOT NULL,                -- snapshot
    body                varchar(2000) NOT NULL,
    is_advisor_question boolean       NOT NULL DEFAULT false,
    created_at          timestamptz   NOT NULL DEFAULT now(),
    edited_at           timestamptz,
    deleted_at          timestamptz,                           -- xóa mềm, hiển thị "Tin nhắn đã bị xóa"
    CONSTRAINT pk_messages PRIMARY KEY (id),
    CONSTRAINT fk_messages_channel FOREIGN KEY (channel_id) REFERENCES collab.channels (id) ON DELETE CASCADE,
    CONSTRAINT fk_messages_parent FOREIGN KEY (parent_message_id) REFERENCES collab.messages (id) ON DELETE CASCADE,
    CONSTRAINT ck_messages_author_type CHECK (author_person_type IN ('STUDENT', 'LECTURER', 'MENTOR')),
    CONSTRAINT ck_messages_body CHECK (char_length(btrim(body)) >= 1),
    CONSTRAINT ck_messages_question_root CHECK (NOT is_advisor_question OR parent_message_id IS NULL)
);
-- Phân trang cursor (created_at, id) mới nhất trước.
CREATE INDEX idx_messages_channel_cursor ON collab.messages (channel_id, created_at DESC, id DESC) WHERE parent_message_id IS NULL;
CREATE INDEX idx_messages_thread ON collab.messages (parent_message_id, created_at) WHERE parent_message_id IS NOT NULL;

-- Câu hỏi cho cố vấn: OPEN tới khi giảng viên hoặc mentor trả lời trong luồng.
CREATE TABLE collab.advisor_questions (
    message_id               uuid        NOT NULL,
    workspace_id             uuid        NOT NULL,
    status                   varchar(10) NOT NULL DEFAULT 'OPEN',
    answered_by_person_type  varchar(10),
    answered_by_person_id    uuid,
    answered_at              timestamptz,
    CONSTRAINT pk_advisor_questions PRIMARY KEY (message_id),
    CONSTRAINT fk_advisor_questions_message FOREIGN KEY (message_id) REFERENCES collab.messages (id) ON DELETE CASCADE,
    CONSTRAINT fk_advisor_questions_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT ck_advisor_questions_status CHECK (status IN ('OPEN', 'ANSWERED')),
    CONSTRAINT ck_advisor_questions_answered CHECK (
        (status = 'ANSWERED') = (answered_at IS NOT NULL AND answered_by_person_id IS NOT NULL)),
    CONSTRAINT ck_advisor_questions_answerer CHECK (answered_by_person_type IS NULL OR answered_by_person_type IN ('LECTURER', 'MENTOR'))
);
CREATE INDEX idx_advisor_questions_open ON collab.advisor_questions (workspace_id) WHERE status = 'OPEN';

CREATE TABLE collab.message_reads (
    channel_id           uuid        NOT NULL,
    person_type          varchar(10) NOT NULL,
    person_id            uuid        NOT NULL,
    last_read_message_id uuid,
    last_read_at         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_message_reads PRIMARY KEY (channel_id, person_type, person_id),
    CONSTRAINT fk_message_reads_channel FOREIGN KEY (channel_id) REFERENCES collab.channels (id) ON DELETE CASCADE,
    CONSTRAINT fk_message_reads_message FOREIGN KEY (last_read_message_id) REFERENCES collab.messages (id) ON DELETE SET NULL
);

-- ---------- File dự án (rule 24) ----------
-- Thư mục is_archive = "EXE101 (kỳ trước)": chỉ đọc. Nội dung file nằm ở MinIO bucket exe-files.
CREATE TABLE collab.folders (
    id           uuid         NOT NULL DEFAULT gen_random_uuid(),
    workspace_id uuid         NOT NULL,
    parent_id    uuid,
    name         varchar(100) NOT NULL,
    is_archive   boolean      NOT NULL DEFAULT false,
    created_at   timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_folders PRIMARY KEY (id),
    CONSTRAINT fk_folders_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT fk_folders_parent FOREIGN KEY (parent_id) REFERENCES collab.folders (id) ON DELETE CASCADE,
    CONSTRAINT ck_folders_name CHECK (char_length(btrim(name)) >= 1)
);
CREATE UNIQUE INDEX uq_folders_name ON collab.folders
    (workspace_id, COALESCE(parent_id, '00000000-0000-0000-0000-000000000000'::uuid), lower(name));

CREATE TABLE collab.project_files (
    id                 uuid         NOT NULL DEFAULT gen_random_uuid(),
    workspace_id       uuid         NOT NULL,
    folder_id          uuid,                                   -- NULL = thư mục gốc
    name               varchar(255) NOT NULL,
    current_version    integer      NOT NULL DEFAULT 1,
    created_by_type    varchar(10)  NOT NULL,
    created_by_id      uuid         NOT NULL,
    created_at         timestamptz  NOT NULL DEFAULT now(),
    updated_at         timestamptz  NOT NULL DEFAULT now(),
    deleted_at         timestamptz,                            -- xóa mềm (người tải lên, trưởng nhóm)
    CONSTRAINT pk_project_files PRIMARY KEY (id),
    CONSTRAINT fk_project_files_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT fk_project_files_folder FOREIGN KEY (folder_id) REFERENCES collab.folders (id) ON DELETE CASCADE,
    CONSTRAINT ck_project_files_type CHECK (name ~* '\.(pdf|docx|pptx|xlsx|png|jpe?g|zip)$'),
    CONSTRAINT ck_project_files_creator CHECK (created_by_type = 'STUDENT'),
    CONSTRAINT ck_project_files_version CHECK (current_version >= 1)
);
-- Trùng tên trong cùng thư mục → phiên bản mới của cùng file, không tạo file thứ hai.
CREATE UNIQUE INDEX uq_project_files_name ON collab.project_files
    (workspace_id, COALESCE(folder_id, '00000000-0000-0000-0000-000000000000'::uuid), lower(name)) WHERE deleted_at IS NULL;

CREATE TABLE collab.file_versions (
    id               uuid         NOT NULL DEFAULT gen_random_uuid(),
    file_id          uuid         NOT NULL,
    version          integer      NOT NULL,
    object_key       varchar(500) NOT NULL,                   -- workspace/{id}/{fileId}/{version}
    size_bytes       bigint       NOT NULL,
    content_type     varchar(150) NOT NULL,
    uploaded_by_id   uuid         NOT NULL,                   -- students.id
    uploaded_by_name varchar(150) NOT NULL,
    created_at       timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_file_versions PRIMARY KEY (id),
    CONSTRAINT uq_file_versions_file_version UNIQUE (file_id, version),
    CONSTRAINT uq_file_versions_object_key UNIQUE (object_key),
    CONSTRAINT fk_file_versions_file FOREIGN KEY (file_id) REFERENCES collab.project_files (id) ON DELETE CASCADE,
    CONSTRAINT ck_file_versions_size CHECK (size_bytes BETWEEN 1 AND 26214400),   -- 25 MB (rule 24)
    CONSTRAINT ck_file_versions_version CHECK (version >= 1)
);

CREATE TABLE collab.file_comments (
    id                 uuid          NOT NULL DEFAULT gen_random_uuid(),
    file_id            uuid          NOT NULL,
    file_version_id    uuid,
    author_person_type varchar(10)   NOT NULL,
    author_person_id   uuid          NOT NULL,
    author_name        varchar(150)  NOT NULL,
    body               varchar(2000) NOT NULL,
    created_at         timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_file_comments PRIMARY KEY (id),
    CONSTRAINT fk_file_comments_file FOREIGN KEY (file_id) REFERENCES collab.project_files (id) ON DELETE CASCADE,
    CONSTRAINT fk_file_comments_version FOREIGN KEY (file_version_id) REFERENCES collab.file_versions (id) ON DELETE SET NULL,
    CONSTRAINT ck_file_comments_author_type CHECK (author_person_type IN ('STUDENT', 'LECTURER', 'MENTOR'))
);
CREATE INDEX idx_file_comments_file ON collab.file_comments (file_id, created_at);

CREATE TABLE collab.message_attachments (
    message_id uuid NOT NULL,
    file_id    uuid NOT NULL,
    CONSTRAINT pk_message_attachments PRIMARY KEY (message_id, file_id),
    CONSTRAINT fk_message_attachments_message FOREIGN KEY (message_id) REFERENCES collab.messages (id) ON DELETE CASCADE,
    CONSTRAINT fk_message_attachments_file FOREIGN KEY (file_id) REFERENCES collab.project_files (id) ON DELETE CASCADE
);

-- ---------- Lịch họp (rule 25) ----------
CREATE TABLE collab.meetings (
    id                   uuid          NOT NULL DEFAULT gen_random_uuid(),
    workspace_id         uuid          NOT NULL,
    title                varchar(200)  NOT NULL,
    agenda               varchar(2000),
    location             varchar(500),                         -- phòng hoặc link họp nhập tay
    organizer_type       varchar(10)   NOT NULL,
    organizer_id         uuid          NOT NULL,
    organizer_name       varchar(150)  NOT NULL,
    organizer_role       varchar(10)   NOT NULL,
    is_mentor_session    boolean       NOT NULL DEFAULT false,  -- họp do mentor tổ chức = buổi mentor
    status               varchar(10)   NOT NULL DEFAULT 'PROPOSED',
    confirmed_slot_id    uuid,
    cancel_reason        varchar(500),
    created_at           timestamptz   NOT NULL DEFAULT now(),
    updated_at           timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_meetings PRIMARY KEY (id),
    CONSTRAINT fk_meetings_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT ck_meetings_organizer_role CHECK (organizer_role IN ('LEADER', 'INSTRUCTOR', 'MENTOR')),
    CONSTRAINT ck_meetings_mentor_session CHECK (is_mentor_session = (organizer_role = 'MENTOR')),
    CONSTRAINT ck_meetings_status CHECK (status IN ('PROPOSED', 'SCHEDULED', 'CANCELLED', 'DONE')),
    CONSTRAINT ck_meetings_confirmed CHECK ((status IN ('SCHEDULED', 'DONE')) = (confirmed_slot_id IS NOT NULL)),
    CONSTRAINT ck_meetings_cancel CHECK ((status = 'CANCELLED') = (cancel_reason IS NOT NULL))
);
CREATE INDEX idx_meetings_workspace_status ON collab.meetings (workspace_id, status);

CREATE TABLE collab.meeting_slots (
    id               uuid        NOT NULL DEFAULT gen_random_uuid(),
    meeting_id       uuid        NOT NULL,
    starts_at        timestamptz NOT NULL,
    duration_minutes smallint    NOT NULL DEFAULT 60,
    CONSTRAINT pk_meeting_slots PRIMARY KEY (id),
    CONSTRAINT uq_meeting_slots_meeting_start UNIQUE (meeting_id, starts_at),
    CONSTRAINT fk_meeting_slots_meeting FOREIGN KEY (meeting_id) REFERENCES collab.meetings (id) ON DELETE CASCADE,
    CONSTRAINT ck_meeting_slots_duration CHECK (duration_minutes BETWEEN 15 AND 240)
);
CREATE INDEX idx_meeting_slots_starts ON collab.meeting_slots (starts_at);
ALTER TABLE collab.meetings ADD CONSTRAINT fk_meetings_confirmed_slot
    FOREIGN KEY (confirmed_slot_id) REFERENCES collab.meeting_slots (id) ON DELETE RESTRICT;

CREATE TABLE collab.meeting_responses (
    slot_id     uuid        NOT NULL,
    person_type varchar(10) NOT NULL,
    person_id   uuid        NOT NULL,
    answer      varchar(3)  NOT NULL,
    answered_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_meeting_responses PRIMARY KEY (slot_id, person_type, person_id),
    CONSTRAINT fk_meeting_responses_slot FOREIGN KEY (slot_id) REFERENCES collab.meeting_slots (id) ON DELETE CASCADE,
    CONSTRAINT ck_meeting_responses_answer CHECK (answer IN ('YES', 'NO'))
);

CREATE TABLE collab.meeting_minutes (
    meeting_id       uuid          NOT NULL,
    content          varchar(8000) NOT NULL,
    recorded_by_id   uuid          NOT NULL,
    recorded_by_name varchar(150)  NOT NULL,
    recorded_at      timestamptz   NOT NULL DEFAULT now(),
    updated_at       timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_meeting_minutes PRIMARY KEY (meeting_id),
    CONSTRAINT fk_meeting_minutes_meeting FOREIGN KEY (meeting_id) REFERENCES collab.meetings (id) ON DELETE CASCADE
);

-- Job Service gọi /internal/meetings/upcoming; mỗi mốc nhắc (1440, 60 phút) chỉ gửi một lần.
CREATE TABLE collab.meeting_reminders (
    meeting_id     uuid        NOT NULL,
    offset_minutes smallint    NOT NULL,
    sent_at        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_meeting_reminders PRIMARY KEY (meeting_id, offset_minutes),
    CONSTRAINT fk_meeting_reminders_meeting FOREIGN KEY (meeting_id) REFERENCES collab.meetings (id) ON DELETE CASCADE
);

-- ---------- Lịch họp hằng tuần EXE201 (rule 25) ----------
-- Giảng viên hướng dẫn đăng các khung giờ, hệ thống cắt thành slot (mặc định 15 phút); mỗi nhóm đặt một slot cố định
-- cho cả kỳ, ai đặt trước được trước. Từ slot đã đặt, Collaboration sinh một cuộc họp mỗi tuần (meetings.schedule_booking_id).
CREATE TABLE collab.meeting_schedules (
    id               uuid         NOT NULL DEFAULT gen_random_uuid(),
    lecturer_id      uuid         NOT NULL,                     -- academic.lecturers.id
    lecturer_name    varchar(150) NOT NULL,                     -- snapshot
    semester_id      uuid         NOT NULL,
    course_code      varchar(6)   NOT NULL DEFAULT 'EXE201',
    slot_minutes     smallint     NOT NULL DEFAULT 15,
    location         varchar(500) NOT NULL,                     -- link họp online
    booking_deadline timestamptz  NOT NULL,
    status           varchar(10)  NOT NULL DEFAULT 'DRAFT',
    published_at     timestamptz,
    closed_at        timestamptz,
    created_at       timestamptz  NOT NULL DEFAULT now(),
    updated_at       timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_meeting_schedules PRIMARY KEY (id),
    CONSTRAINT uq_meeting_schedules_lecturer UNIQUE (lecturer_id, semester_id, course_code),
    CONSTRAINT ck_meeting_schedules_course CHECK (course_code = 'EXE201'),
    CONSTRAINT ck_meeting_schedules_slot CHECK (slot_minutes BETWEEN 5 AND 60),
    CONSTRAINT ck_meeting_schedules_status CHECK (status IN ('DRAFT', 'PUBLISHED', 'CLOSED')),
    CONSTRAINT ck_meeting_schedules_published CHECK ((status = 'DRAFT') = (published_at IS NULL)),
    CONSTRAINT ck_meeting_schedules_closed CHECK ((status = 'CLOSED') = (closed_at IS NOT NULL))
);

-- Khung giờ trong tuần, ví dụ thứ Ba 09:00–11:00 (day_of_week: 1 = thứ Hai … 7 = Chủ nhật, giờ địa phương).
CREATE TABLE collab.schedule_windows (
    id          uuid     NOT NULL DEFAULT gen_random_uuid(),
    schedule_id uuid     NOT NULL,
    day_of_week smallint NOT NULL,
    start_time  time     NOT NULL,
    end_time    time     NOT NULL,
    CONSTRAINT pk_schedule_windows PRIMARY KEY (id),
    CONSTRAINT fk_schedule_windows_schedule FOREIGN KEY (schedule_id) REFERENCES collab.meeting_schedules (id) ON DELETE CASCADE,
    CONSTRAINT ck_schedule_windows_day CHECK (day_of_week BETWEEN 1 AND 7),
    CONSTRAINT ck_schedule_windows_time CHECK (end_time > start_time)
);
CREATE INDEX idx_schedule_windows_schedule ON collab.schedule_windows (schedule_id);

CREATE TABLE collab.schedule_slots (
    id          uuid     NOT NULL DEFAULT gen_random_uuid(),
    schedule_id uuid     NOT NULL,
    window_id   uuid     NOT NULL,
    day_of_week smallint NOT NULL,
    start_time  time     NOT NULL,
    end_time    time     NOT NULL,
    CONSTRAINT pk_schedule_slots PRIMARY KEY (id),
    CONSTRAINT uq_schedule_slots_time UNIQUE (schedule_id, day_of_week, start_time),
    CONSTRAINT fk_schedule_slots_schedule FOREIGN KEY (schedule_id) REFERENCES collab.meeting_schedules (id) ON DELETE CASCADE,
    CONSTRAINT fk_schedule_slots_window FOREIGN KEY (window_id) REFERENCES collab.schedule_windows (id) ON DELETE CASCADE,
    CONSTRAINT ck_schedule_slots_day CHECK (day_of_week BETWEEN 1 AND 7),
    CONSTRAINT ck_schedule_slots_time CHECK (end_time > start_time)
);

-- Một slot một nhóm, một nhóm một slot đang hiệu lực. Đổi slot = hủy dòng cũ (cancelled_at) rồi tạo dòng mới.
CREATE TABLE collab.slot_bookings (
    id            uuid        NOT NULL DEFAULT gen_random_uuid(),
    schedule_id   uuid        NOT NULL,
    slot_id       uuid        NOT NULL,
    workspace_id  uuid        NOT NULL,
    source        varchar(10) NOT NULL,                         -- LEADER tự đặt, INSTRUCTOR đổi giúp, AUTO xếp lúc hết hạn
    booked_by_id  uuid,                                         -- sinh viên / giảng viên; NULL khi AUTO
    booked_at     timestamptz NOT NULL DEFAULT now(),
    cancelled_at  timestamptz,
    CONSTRAINT pk_slot_bookings PRIMARY KEY (id),
    CONSTRAINT fk_slot_bookings_schedule FOREIGN KEY (schedule_id) REFERENCES collab.meeting_schedules (id) ON DELETE CASCADE,
    CONSTRAINT fk_slot_bookings_slot FOREIGN KEY (slot_id) REFERENCES collab.schedule_slots (id) ON DELETE CASCADE,
    CONSTRAINT fk_slot_bookings_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT ck_slot_bookings_source CHECK (source IN ('LEADER', 'INSTRUCTOR', 'AUTO')),
    CONSTRAINT ck_slot_bookings_by CHECK ((source = 'AUTO') = (booked_by_id IS NULL))
);
CREATE UNIQUE INDEX uq_slot_bookings_slot_active ON collab.slot_bookings (slot_id) WHERE cancelled_at IS NULL;
CREATE UNIQUE INDEX uq_slot_bookings_workspace_active ON collab.slot_bookings (schedule_id, workspace_id) WHERE cancelled_at IS NULL;

-- Họp hằng tuần là một dòng meetings bình thường (organizer_role INSTRUCTOR, có confirmed slot) gắn với booking và số tuần.
ALTER TABLE collab.meetings
    ADD COLUMN schedule_booking_id uuid,
    ADD COLUMN week_no smallint,
    ADD CONSTRAINT fk_meetings_schedule_booking FOREIGN KEY (schedule_booking_id)
        REFERENCES collab.slot_bookings (id) ON DELETE SET NULL,
    ADD CONSTRAINT ck_meetings_weekly CHECK (
        (schedule_booking_id IS NULL AND week_no IS NULL)
        OR (week_no BETWEEN 1 AND 20 AND organizer_role = 'INSTRUCTOR'));
CREATE UNIQUE INDEX uq_meetings_weekly ON collab.meetings (schedule_booking_id, week_no) WHERE schedule_booking_id IS NOT NULL;

-- ---------- Checkpoint EXE101 (rule 27) ----------
-- Giảng viên đánh dấu nhóm đã trình bày Checkpoint 1–4 trên lớp. Không lưu điểm (điểm ở FAP).
-- Bỏ đánh dấu (trong ngày) = xóa dòng; Services kiểm tra giới hạn trong ngày.
CREATE TABLE collab.checkpoint_reviews (
    id                      uuid         NOT NULL DEFAULT gen_random_uuid(),
    workspace_id            uuid         NOT NULL,
    checkpoint_no           smallint     NOT NULL,
    presented_on            date         NOT NULL,
    comment                 varchar(500),
    reviewed_by_lecturer_id uuid         NOT NULL,
    reviewed_by_name        varchar(150) NOT NULL,              -- snapshot
    created_at              timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_checkpoint_reviews PRIMARY KEY (id),
    CONSTRAINT uq_checkpoint_reviews_workspace UNIQUE (workspace_id, checkpoint_no),
    CONSTRAINT fk_checkpoint_reviews_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT ck_checkpoint_reviews_no CHECK (checkpoint_no BETWEEN 1 AND 4)
);

-- ---------- Việc và bài nộp (rule 23) ----------
-- kind = DELIVERABLE: bài nộp do giảng viên hướng dẫn tạo, cần nộp file.
CREATE TABLE collab.task_items (
    id                uuid          NOT NULL DEFAULT gen_random_uuid(),
    workspace_id      uuid          NOT NULL,
    kind              varchar(12)   NOT NULL DEFAULT 'TASK',
    title             varchar(200)  NOT NULL,
    description       varchar(4000),
    status            varchar(5)    NOT NULL DEFAULT 'TODO',
    due_at            timestamptz,
    position          integer       NOT NULL DEFAULT 0,        -- thứ tự trong cột
    created_by_type   varchar(10)   NOT NULL,
    created_by_id     uuid          NOT NULL,
    source_meeting_id uuid,                                    -- tạo từ biên bản họp
    completed_at      timestamptz,
    created_at        timestamptz   NOT NULL DEFAULT now(),
    updated_at        timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_task_items PRIMARY KEY (id),
    CONSTRAINT fk_task_items_workspace FOREIGN KEY (workspace_id) REFERENCES collab.workspaces (id) ON DELETE CASCADE,
    CONSTRAINT fk_task_items_meeting FOREIGN KEY (source_meeting_id) REFERENCES collab.meetings (id) ON DELETE SET NULL,
    CONSTRAINT ck_task_items_kind CHECK (kind IN ('TASK', 'DELIVERABLE')),
    CONSTRAINT ck_task_items_status CHECK (status IN ('TODO', 'DOING', 'DONE')),
    CONSTRAINT ck_task_items_done CHECK ((status = 'DONE') = (completed_at IS NOT NULL)),
    CONSTRAINT ck_task_items_creator CHECK (created_by_type IN ('STUDENT', 'LECTURER', 'MENTOR')),
    -- Mentor không tạo việc trực tiếp (chỉ qua biên bản họp); chỉ giảng viên tạo bài nộp.
    CONSTRAINT ck_task_items_deliverable_creator CHECK (kind <> 'DELIVERABLE' OR created_by_type = 'LECTURER'),
    CONSTRAINT ck_task_items_deliverable_due CHECK (kind <> 'DELIVERABLE' OR due_at IS NOT NULL),
    CONSTRAINT ck_task_items_title CHECK (char_length(btrim(title)) >= 1)
);
CREATE INDEX idx_task_items_board ON collab.task_items (workspace_id, status, position);
CREATE INDEX idx_task_items_due ON collab.task_items (due_at) WHERE status <> 'DONE' AND due_at IS NOT NULL;

CREATE TABLE collab.task_assignees (
    task_id    uuid NOT NULL,
    student_id uuid NOT NULL,
    CONSTRAINT pk_task_assignees PRIMARY KEY (task_id, student_id),
    CONSTRAINT fk_task_assignees_task FOREIGN KEY (task_id) REFERENCES collab.task_items (id) ON DELETE CASCADE
);
CREATE INDEX idx_task_assignees_student ON collab.task_assignees (student_id);

CREATE TABLE collab.task_comments (
    id                 uuid          NOT NULL DEFAULT gen_random_uuid(),
    task_id            uuid          NOT NULL,
    author_person_type varchar(10)   NOT NULL,
    author_person_id   uuid          NOT NULL,
    author_name        varchar(150)  NOT NULL,
    body               varchar(2000) NOT NULL,
    created_at         timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_task_comments PRIMARY KEY (id),
    CONSTRAINT fk_task_comments_task FOREIGN KEY (task_id) REFERENCES collab.task_items (id) ON DELETE CASCADE,
    CONSTRAINT ck_task_comments_author_type CHECK (author_person_type IN ('STUDENT', 'LECTURER', 'MENTOR'))
);
CREATE INDEX idx_task_comments_task ON collab.task_comments (task_id, created_at);

CREATE TABLE collab.deliverable_submissions (
    id                 uuid          NOT NULL DEFAULT gen_random_uuid(),
    task_id            uuid          NOT NULL,
    file_version_id    uuid          NOT NULL,
    submitted_by_id    uuid          NOT NULL,                 -- students.id
    submitted_at       timestamptz   NOT NULL DEFAULT now(),
    review_status      varchar(15)   NOT NULL DEFAULT 'PENDING',
    review_comment     varchar(2000),
    reviewed_by_id     uuid,                                   -- lecturers.id
    reviewed_at        timestamptz,
    CONSTRAINT pk_deliverable_submissions PRIMARY KEY (id),
    CONSTRAINT fk_deliverable_submissions_task FOREIGN KEY (task_id) REFERENCES collab.task_items (id) ON DELETE CASCADE,
    CONSTRAINT fk_deliverable_submissions_version FOREIGN KEY (file_version_id)
        REFERENCES collab.file_versions (id) ON DELETE RESTRICT,
    CONSTRAINT ck_deliverable_submissions_status CHECK (review_status IN ('PENDING', 'ACCEPTED', 'NEEDS_REVISION')),
    CONSTRAINT ck_deliverable_submissions_review CHECK (
        (review_status = 'PENDING') = (reviewed_at IS NULL AND reviewed_by_id IS NULL)),
    CONSTRAINT ck_deliverable_submissions_revision_note CHECK (review_status <> 'NEEDS_REVISION' OR review_comment IS NOT NULL)
);
CREATE INDEX idx_deliverable_submissions_task ON collab.deliverable_submissions (task_id, submitted_at DESC);
CREATE INDEX idx_deliverable_submissions_pending ON collab.deliverable_submissions (task_id) WHERE review_status = 'PENDING';

-- Bài nộp chỉ gắn với việc kind = DELIVERABLE.
CREATE OR REPLACE FUNCTION collab.tg_submission_is_deliverable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM collab.task_items WHERE id = NEW.task_id AND kind = 'DELIVERABLE') THEN
        RAISE EXCEPTION 'Chỉ nộp bài cho việc loại bài nộp'
            USING ERRCODE = '23514', CONSTRAINT = 'ck_deliverable_submissions_kind';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_deliverable_submissions_kind BEFORE INSERT OR UPDATE OF task_id ON collab.deliverable_submissions
    FOR EACH ROW EXECUTE FUNCTION collab.tg_submission_is_deliverable();

-- ---------- Outbox (topic exe.collab), idempotency khi consume ----------
CREATE TABLE collab.outbox_messages (
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
CREATE INDEX idx_outbox_messages_pending ON collab.outbox_messages (occurred_at) WHERE processed_at IS NULL;

CREATE TABLE collab.processed_events (
    event_id     uuid         NOT NULL,
    event_type   varchar(100) NOT NULL,
    processed_at timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_processed_events PRIMARY KEY (event_id)
);

-- ---------- updated_at triggers ----------
CREATE TRIGGER trg_project_files_updated_at BEFORE UPDATE ON collab.project_files FOR EACH ROW EXECUTE FUNCTION collab.set_updated_at();
CREATE TRIGGER trg_meetings_updated_at      BEFORE UPDATE ON collab.meetings      FOR EACH ROW EXECUTE FUNCTION collab.set_updated_at();
CREATE TRIGGER trg_meeting_minutes_updated_at BEFORE UPDATE ON collab.meeting_minutes FOR EACH ROW EXECUTE FUNCTION collab.set_updated_at();
CREATE TRIGGER trg_task_items_updated_at    BEFORE UPDATE ON collab.task_items    FOR EACH ROW EXECUTE FUNCTION collab.set_updated_at();
CREATE TRIGGER trg_meeting_schedules_updated_at BEFORE UPDATE ON collab.meeting_schedules FOR EACH ROW EXECUTE FUNCTION collab.set_updated_at();

RESET ROLE;
