-- BE-01 – job_db (Job Service). Thiết kế: docs/EXE_Database_Design.pdf mục 9.2.
-- Chỉ tạo schema và quyền; bảng do Hangfire.PostgreSql tự tạo khi Job Service khởi động
-- (PostgreSqlStorageOptions { SchemaName = "hangfire", PrepareSchemaIfNecessary = true }).
\connect job_db

CREATE SCHEMA IF NOT EXISTS hangfire AUTHORIZATION job_svc;
