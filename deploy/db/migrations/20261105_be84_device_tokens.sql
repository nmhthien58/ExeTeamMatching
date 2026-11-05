\set ON_ERROR_STOP on

SET ROLE notification_svc;

CREATE TABLE IF NOT EXISTS notification.device_tokens (
    id           uuid          NOT NULL DEFAULT gen_random_uuid(),
    user_id      uuid          NOT NULL,
    token        varchar(4096) NOT NULL,
    platform     varchar(10)   NOT NULL,
    created_at   timestamptz   NOT NULL DEFAULT now(),
    last_seen_at timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_device_tokens PRIMARY KEY (id),
    CONSTRAINT uq_device_tokens_token UNIQUE (token),
    CONSTRAINT ck_device_tokens_platform CHECK (platform IN ('android', 'ios'))
);

CREATE INDEX IF NOT EXISTS idx_device_tokens_user ON notification.device_tokens (user_id);

RESET ROLE;
