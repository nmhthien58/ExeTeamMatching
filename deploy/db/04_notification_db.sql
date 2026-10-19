-- BE-01 – notification_db (Notification Service). Thiết kế: docs/EXE_Database_Design.pdf mục 7.
\connect notification_db

CREATE EXTENSION IF NOT EXISTS citext;
CREATE SCHEMA IF NOT EXISTS notification AUTHORIZATION notification_svc;
SET ROLE notification_svc;
SET search_path = notification, public;

-- Danh bạ người nhận (email, tên) dựng từ topic exe.student (StudentImported, StudentProfileUpdated, LecturerRegistered).
CREATE TABLE notification.contacts (
    id            uuid         NOT NULL,                      -- academic students.id / lecturers.id
    person_type   varchar(10)  NOT NULL,
    email         citext       NOT NULL,
    full_name     varchar(150) NOT NULL,
    email_enabled boolean      NOT NULL DEFAULT true,         -- công tắc nhận email thông báo (A07)
    updated_at    timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_contacts PRIMARY KEY (id),
    CONSTRAINT ck_contacts_person_type CHECK (person_type IN ('STUDENT', 'LECTURER'))
);
CREATE INDEX idx_contacts_email ON notification.contacts (email);

-- Thông báo trong ứng dụng. ADMIN -> recipient_id NULL (thông báo chung). UNIQUE (sự kiện, người nhận) để idempotent.
CREATE TABLE notification.notifications (
    id              uuid          NOT NULL DEFAULT gen_random_uuid(),
    recipient_type  varchar(10)   NOT NULL,
    recipient_id    uuid,
    type            varchar(50)   NOT NULL,                   -- = eventType
    title           varchar(200)  NOT NULL,
    content         varchar(2000) NOT NULL,
    link_url        varchar(500),                             -- màn hình cần mở khi chạm
    source_event_id uuid          NOT NULL,
    is_read         boolean       NOT NULL DEFAULT false,
    read_at         timestamptz,
    created_at      timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_notifications PRIMARY KEY (id),
    CONSTRAINT ck_notifications_recipient_type CHECK (recipient_type IN ('STUDENT', 'LECTURER', 'ADMIN')),
    CONSTRAINT ck_notifications_recipient CHECK ((recipient_type = 'ADMIN') = (recipient_id IS NULL)),
    CONSTRAINT ck_notifications_read CHECK (is_read = (read_at IS NOT NULL))
);
CREATE INDEX idx_notifications_recipient ON notification.notifications (recipient_type, recipient_id, created_at DESC);
CREATE INDEX idx_notifications_unread ON notification.notifications (recipient_type, recipient_id) WHERE NOT is_read;
CREATE UNIQUE INDEX uq_notifications_event_recipient ON notification.notifications
    (source_event_id, recipient_type, recipient_id) NULLS NOT DISTINCT;

-- Nhật ký gửi email SMTP; retry khi FAILED.
CREATE TABLE notification.email_logs (
    id              uuid         NOT NULL DEFAULT gen_random_uuid(),
    notification_id uuid,
    source_event_id uuid         NOT NULL,
    to_email        citext       NOT NULL,
    subject         varchar(255) NOT NULL,
    template_code   varchar(50)  NOT NULL,
    status          varchar(10)  NOT NULL DEFAULT 'PENDING',
    attempts        integer      NOT NULL DEFAULT 0,
    last_error      text,
    created_at      timestamptz  NOT NULL DEFAULT now(),
    sent_at         timestamptz,
    CONSTRAINT pk_email_logs PRIMARY KEY (id),
    CONSTRAINT uq_email_logs_event_to UNIQUE (source_event_id, to_email),
    CONSTRAINT fk_email_logs_notification FOREIGN KEY (notification_id)
        REFERENCES notification.notifications (id) ON DELETE SET NULL,
    CONSTRAINT ck_email_logs_status CHECK (status IN ('PENDING', 'SENT', 'FAILED')),
    CONSTRAINT ck_email_logs_sent CHECK ((status = 'SENT') = (sent_at IS NOT NULL)),
    CONSTRAINT ck_email_logs_attempts CHECK (attempts >= 0)
);
CREATE INDEX idx_email_logs_notification_id ON notification.email_logs (notification_id);
CREATE INDEX idx_email_logs_retry ON notification.email_logs (created_at) WHERE status IN ('PENDING', 'FAILED');

-- EventId đã xử lý theo từng handler, chống gửi trùng.
CREATE TABLE notification.processed_events (
    event_id     uuid         NOT NULL,
    consumer     varchar(100) NOT NULL,
    topic        varchar(100) NOT NULL,
    event_type   varchar(100) NOT NULL,
    processed_at timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT pk_processed_events PRIMARY KEY (event_id, consumer)
);
CREATE INDEX idx_processed_events_processed_at ON notification.processed_events (processed_at);

RESET ROLE;
