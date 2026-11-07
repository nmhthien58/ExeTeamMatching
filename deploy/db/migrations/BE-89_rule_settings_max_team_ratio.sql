-- BE-89 – Hệ số số nhóm tối đa của môn trong kỳ (W05): số nhóm tối đa = floor(số sinh viên của môn × hệ số). Chạy lại nhiều lần vẫn an toàn.
-- docker exec -i exe-postgres psql -U postgres -d academic_db -v ON_ERROR_STOP=1 < deploy/db/migrations/BE-89_rule_settings_max_team_ratio.sql
SET ROLE academic_svc;
SET search_path = academic, public;

ALTER TABLE academic.rule_settings ADD COLUMN IF NOT EXISTS max_team_ratio numeric(3,2) NOT NULL DEFAULT 0.20;
ALTER TABLE academic.rule_settings DROP CONSTRAINT IF EXISTS ck_rule_settings_team_ratio;
ALTER TABLE academic.rule_settings ADD CONSTRAINT ck_rule_settings_team_ratio CHECK (max_team_ratio BETWEEN 0.01 AND 1.00);

RESET ROLE;
