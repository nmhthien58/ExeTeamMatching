-- BE-87 – thêm bảng client_error_reports vào identity_db đang có dữ liệu (không cần reset volume). Chạy lại nhiều lần vẫn an toàn.
-- docker exec -i exe-postgres psql -U postgres -d identity_db -v ON_ERROR_STOP=1 < deploy/db/migrations/BE-87_client_error_reports.sql
SET ROLE identity_svc;
SET search_path = identity, public;

-- BE-87 – Báo cáo lỗi từ app (Bộ môn xem ở W30, bản chính vẫn ở Firebase Crashlytics). Gửi được cả khi chưa đăng nhập nên
-- user_id có thể null và không có khóa ngoại: báo cáo là dữ liệu theo dõi, không ràng buộc với tài khoản. Không lưu email, tên, IP.
-- first_stack_line là dòng đầu của stack_trace, tính khi nhận, để gom lỗi lặp lại bằng GROUP BY.
CREATE TABLE IF NOT EXISTS identity.client_error_reports (
    id               uuid          NOT NULL DEFAULT gen_random_uuid(),
    kind             varchar(20)   NOT NULL,
    message          varchar(2000) NOT NULL,
    exception_type   varchar(200)  NOT NULL,
    stack_trace      text,                       -- cắt còn 8192 ký tự
    first_stack_line varchar(300)  NOT NULL DEFAULT '',
    platform         varchar(20)   NOT NULL,
    app_version      varchar(30)   NOT NULL,
    device_model     varchar(100),
    os_version       varchar(100),
    user_id          uuid,
    role             varchar(20),
    screen           varchar(200),               -- route lúc xảy ra lỗi
    occurred_at      timestamptz   NOT NULL,
    received_at      timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT pk_client_error_reports PRIMARY KEY (id),
    CONSTRAINT ck_client_error_reports_kind CHECK (kind IN ('fatal', 'non_fatal'))
);
CREATE INDEX IF NOT EXISTS idx_client_error_reports_occurred_at ON identity.client_error_reports (occurred_at DESC);
CREATE INDEX IF NOT EXISTS idx_client_error_reports_kind_occurred_at ON identity.client_error_reports (kind, occurred_at DESC);

RESET ROLE;
