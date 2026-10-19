-- BE-01 – identity_db (Identity Service). Thiết kế: docs/EXE_Database_Design.pdf mục 4.
\connect identity_db

CREATE EXTENSION IF NOT EXISTS citext;
CREATE SCHEMA IF NOT EXISTS identity AUTHORIZATION identity_svc;
SET ROLE identity_svc;
SET search_path = identity, public;

CREATE OR REPLACE FUNCTION identity.set_updated_at() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END $$;

-- Vai trò: Student, Lecturer, Mentor, Admin. Leader là policy ở tầng Services, không phải role.
CREATE TABLE identity.roles (
    id   smallint    NOT NULL,
    code varchar(20) NOT NULL,
    name varchar(50) NOT NULL,
    CONSTRAINT pk_roles PRIMARY KEY (id),
    CONSTRAINT uq_roles_code UNIQUE (code),
    CONSTRAINT ck_roles_code CHECK (code IN ('Student', 'Lecturer', 'Admin', 'Mentor'))
);

-- Tài khoản đăng nhập kiêm email whitelist (rule 5). Student / Lecturer / Mentor được tạo sẵn (PENDING) từ sự kiện
-- StudentImported / LecturerRegistered / MentorRegistered, chuyển ACTIVE ở lần đăng nhập Google đầu tiên.
-- Vai trò nằm ở user_account_roles: một email có thể vừa là Lecturer vừa là Mentor (rule 20).
CREATE TABLE identity.user_accounts (
    id              uuid         NOT NULL DEFAULT gen_random_uuid(),
    email           citext       NOT NULL,
    google_subject  varchar(255),
    full_name       varchar(150) NOT NULL,
    status          varchar(20)  NOT NULL DEFAULT 'PENDING',
    allowed_until   timestamptz,                -- hết hạn whitelist = end_date học kỳ
    last_login_at   timestamptz,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    updated_at      timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_user_accounts PRIMARY KEY (id),
    CONSTRAINT uq_user_accounts_email UNIQUE (email),
    CONSTRAINT uq_user_accounts_google_subject UNIQUE (google_subject),
    CONSTRAINT ck_user_accounts_email CHECK (email ~ '^[^@\s]+@[^@\s]+\.[^@\s]+$'),
    CONSTRAINT ck_user_accounts_status CHECK (status IN ('PENDING', 'ACTIVE', 'LOCKED'))
);
CREATE TRIGGER trg_user_accounts_updated_at BEFORE UPDATE ON identity.user_accounts
    FOR EACH ROW EXECUTE FUNCTION identity.set_updated_at();

-- Vai trò của tài khoản. external_ref_id = academic students.id / lecturers.id / mentors.id, đưa vào JWT
-- (claim roles là danh sách; student_id / lecturer_id / mentor_id). Admin không có external_ref_id.
CREATE TABLE identity.user_account_roles (
    user_account_id uuid        NOT NULL,
    role_id         smallint    NOT NULL,
    external_ref_id uuid,
    created_at      timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_user_account_roles PRIMARY KEY (user_account_id, role_id),
    CONSTRAINT fk_user_account_roles_account FOREIGN KEY (user_account_id)
        REFERENCES identity.user_accounts (id) ON DELETE CASCADE,
    CONSTRAINT fk_user_account_roles_role FOREIGN KEY (role_id) REFERENCES identity.roles (id) ON DELETE RESTRICT,
    -- 1 Student, 2 Lecturer, 4 Mentor cần external_ref_id; 3 Admin thì không.
    CONSTRAINT ck_user_account_roles_external_ref CHECK ((role_id IN (1, 2, 4)) = (external_ref_id IS NOT NULL))
);
CREATE UNIQUE INDEX uq_user_account_roles_external_ref ON identity.user_account_roles (role_id, external_ref_id)
    WHERE external_ref_id IS NOT NULL;
CREATE INDEX idx_user_account_roles_role_id ON identity.user_account_roles (role_id);

-- Refresh token ~7 ngày, chỉ lưu SHA-256. Rotation: token cũ bị revoke và trỏ sang token mới.
CREATE TABLE identity.refresh_tokens (
    id                   uuid        NOT NULL DEFAULT gen_random_uuid(),
    user_account_id      uuid        NOT NULL,
    token_hash           char(64)    NOT NULL,
    expires_at           timestamptz NOT NULL,
    revoked_at           timestamptz,
    replaced_by_token_id uuid,
    created_by_ip        inet,
    created_at           timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_refresh_tokens PRIMARY KEY (id),
    CONSTRAINT uq_refresh_tokens_token_hash UNIQUE (token_hash),
    CONSTRAINT fk_refresh_tokens_user_account FOREIGN KEY (user_account_id)
        REFERENCES identity.user_accounts (id) ON DELETE CASCADE,
    CONSTRAINT fk_refresh_tokens_replaced_by FOREIGN KEY (replaced_by_token_id)
        REFERENCES identity.refresh_tokens (id) ON DELETE SET NULL,
    CONSTRAINT ck_refresh_tokens_expiry CHECK (expires_at > created_at)
);
CREATE INDEX idx_refresh_tokens_user_account_id ON identity.refresh_tokens (user_account_id) WHERE revoked_at IS NULL;
CREATE INDEX idx_refresh_tokens_replaced_by ON identity.refresh_tokens (replaced_by_token_id);

-- Idempotency khi consume StudentImported / LecturerRegistered / MentorRegistered.
CREATE TABLE identity.processed_events (
    event_id     uuid         NOT NULL,
    event_type   varchar(100) NOT NULL,
    processed_at timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_processed_events PRIMARY KEY (event_id)
);

RESET ROLE;
