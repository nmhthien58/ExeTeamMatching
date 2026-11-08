-- BE-92: Cho phép MSSV 2-3 chữ cái trong team_members, team_baseline_members, change_request_items
ALTER TABLE team.team_members ALTER COLUMN student_code TYPE varchar(10);
ALTER TABLE team.team_members DROP CONSTRAINT ck_team_members_student_code;
ALTER TABLE team.team_members ADD CONSTRAINT ck_team_members_student_code CHECK (student_code ~ '^[A-Z]{2,3}[0-9]{6}$');

ALTER TABLE team.team_baseline_members ALTER COLUMN student_code TYPE varchar(10);
ALTER TABLE team.team_baseline_members DROP CONSTRAINT ck_team_baseline_members_code;
ALTER TABLE team.team_baseline_members ADD CONSTRAINT ck_team_baseline_members_code CHECK (student_code ~ '^[A-Z]{2,3}[0-9]{6}$');

ALTER TABLE team.change_request_items ALTER COLUMN student_code TYPE varchar(10);
ALTER TABLE team.change_request_items DROP CONSTRAINT ck_change_request_items_code;
ALTER TABLE team.change_request_items ADD CONSTRAINT ck_change_request_items_code CHECK (student_code IS NULL OR student_code ~ '^[A-Z]{2,3}[0-9]{6}$');
