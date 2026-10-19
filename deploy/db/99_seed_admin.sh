#!/bin/bash
# BE-01 – Tạo tài khoản Admin đầu tiên (Bộ môn). ADMIN_EMAIL phải là tài khoản Google thật dùng để đăng nhập.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname identity_db \
  -v admin_email="${ADMIN_EMAIL:-admin@example.com}" \
  -v admin_name="${ADMIN_NAME:-Bộ môn EXE}" <<'EOSQL'
SET ROLE identity_svc;
INSERT INTO identity.user_accounts (email, full_name, status)
VALUES (:'admin_email', :'admin_name', 'PENDING')
ON CONFLICT (email) DO NOTHING;
INSERT INTO identity.user_account_roles (user_account_id, role_id)
SELECT id, 3 FROM identity.user_accounts WHERE email = :'admin_email'
ON CONFLICT (user_account_id, role_id) DO NOTHING;
RESET ROLE;
EOSQL
