#!/bin/bash
# BE-01 – Tạo tài khoản Admin đầu tiên (Bộ môn). ADMIN_EMAIL phải là tài khoản Google thật dùng để đăng nhập.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname identity_db \
  -v admin_email="${ADMIN_EMAIL:-admin@example.com}" \
  -v admin_name="${ADMIN_NAME:-Bộ môn EXE}" <<'EOSQL'
SET ROLE identity_svc;
INSERT INTO identity.user_accounts (email, full_name, role_id, status)
VALUES (:'admin_email', :'admin_name', 3, 'PENDING')
ON CONFLICT (email) DO NOTHING;
RESET ROLE;
EOSQL
