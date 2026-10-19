#!/bin/bash
# BE-01, BE-60 – Tạo login role và 6 database (mỗi service 1 database).
# Chạy tự động một lần bởi image postgres khi volume còn trống (docker-entrypoint-initdb.d).
# Mật khẩu lấy từ biến môi trường (xem deploy/.env.example); giá trị mặc định chỉ dùng cho máy dev.
set -euo pipefail

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v identity_pw="${IDENTITY_DB_PASSWORD:-identity_dev_pw}" \
  -v academic_pw="${ACADEMIC_DB_PASSWORD:-academic_dev_pw}" \
  -v team_pw="${TEAM_DB_PASSWORD:-team_dev_pw}" \
  -v notification_pw="${NOTIFICATION_DB_PASSWORD:-notification_dev_pw}" \
  -v job_pw="${JOB_DB_PASSWORD:-job_dev_pw}" \
  -v collab_pw="${COLLAB_DB_PASSWORD:-collab_dev_pw}" <<'EOSQL'
CREATE ROLE identity_svc     LOGIN PASSWORD :'identity_pw';
CREATE ROLE academic_svc     LOGIN PASSWORD :'academic_pw';
CREATE ROLE team_svc         LOGIN PASSWORD :'team_pw';
CREATE ROLE notification_svc LOGIN PASSWORD :'notification_pw';
CREATE ROLE job_svc          LOGIN PASSWORD :'job_pw';
CREATE ROLE collab_svc       LOGIN PASSWORD :'collab_pw';

CREATE DATABASE identity_db     OWNER identity_svc;
CREATE DATABASE academic_db     OWNER academic_svc;
CREATE DATABASE team_db         OWNER team_svc;
CREATE DATABASE notification_db OWNER notification_svc;
CREATE DATABASE job_db          OWNER job_svc;
CREATE DATABASE collab_db       OWNER collab_svc;

-- Mỗi role chỉ kết nối được database của mình.
REVOKE CONNECT, TEMPORARY ON DATABASE identity_db, academic_db, team_db, notification_db, job_db, collab_db FROM PUBLIC;
GRANT CONNECT ON DATABASE identity_db     TO identity_svc;
GRANT CONNECT ON DATABASE academic_db     TO academic_svc;
GRANT CONNECT ON DATABASE team_db         TO team_svc;
GRANT CONNECT ON DATABASE notification_db TO notification_svc;
GRANT CONNECT ON DATABASE job_db          TO job_svc;
GRANT CONNECT ON DATABASE collab_db       TO collab_svc;

ALTER ROLE identity_svc     SET search_path = identity, public;
ALTER ROLE academic_svc     SET search_path = academic, public;
ALTER ROLE team_svc         SET search_path = team, public;
ALTER ROLE notification_svc SET search_path = notification, public;
ALTER ROLE job_svc          SET search_path = hangfire, public;
ALTER ROLE collab_svc       SET search_path = collab, public;
EOSQL
