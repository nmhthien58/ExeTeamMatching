-- BE-90 – Sĩ số và số chuyên ngành lấy theo cấu hình W05 nên team_db không còn ép cứng 4–6 người, 2 chuyên ngành.
-- team_db không đọc được rule_settings của Academic: Team Service kiểm tra theo cấu hình, ở đây chỉ giữ khung W05 cho phép
-- (RuleSettingsInputValidator: sĩ số 2–50, chuyên ngành từ 1) để chặn dữ liệu hỏng. Chạy lại nhiều lần vẫn an toàn.
-- docker exec -i exe-postgres psql -U postgres -d team_db -v ON_ERROR_STOP=1 < deploy/db/migrations/BE-90_team_composition_from_w05.sql
SET ROLE team_svc;
SET search_path = team, public;

ALTER TABLE team.teams DROP CONSTRAINT IF EXISTS ck_teams_member_count;
ALTER TABLE team.teams ADD CONSTRAINT ck_teams_member_count CHECK (member_count BETWEEN 0 AND 50);

-- Kiểm tra cơ cấu nhóm lúc COMMIT (deferred): đúng 1 leader khi có thành viên (rule 1);
-- khi FINALIZED: nằm trong khung W05 (rule 1, 2 theo cấu hình do Team Service kiểm tra).
CREATE OR REPLACE FUNCTION team.check_team_composition(p_team_id uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
    v_status  varchar(12);
    v_members integer;
    v_leaders integer;
    v_specs   integer;
BEGIN
    SELECT status INTO v_status FROM team.teams WHERE id = p_team_id;
    IF NOT FOUND OR v_status = 'DISBANDED' THEN
        RETURN;
    END IF;
    SELECT count(*), count(*) FILTER (WHERE role = 'LEADER'), count(DISTINCT specialization_id)
      INTO v_members, v_leaders, v_specs
      FROM team.team_members WHERE team_id = p_team_id;
    IF v_members > 0 AND v_leaders <> 1 THEN
        RAISE EXCEPTION 'Nhóm % phải có đúng 1 trưởng nhóm (đang có %)', p_team_id, v_leaders
            USING ERRCODE = '23514', CONSTRAINT = 'ck_team_exactly_one_leader';
    END IF;
    IF v_status = 'FINALIZED' THEN
        IF v_members NOT BETWEEN 2 AND 50 THEN
            RAISE EXCEPTION 'Nhóm % đã chốt phải có 2–50 thành viên (đang có %)', p_team_id, v_members
                USING ERRCODE = '23514', CONSTRAINT = 'ck_team_finalized_size';
        END IF;
        IF v_specs < 1 THEN
            RAISE EXCEPTION 'Nhóm % đã chốt phải có ít nhất 1 chuyên ngành (đang có %)', p_team_id, v_specs
                USING ERRCODE = '23514', CONSTRAINT = 'ck_team_finalized_specializations';
        END IF;
    END IF;
END $$;

RESET ROLE;
