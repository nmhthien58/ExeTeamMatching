-- BE-88 – Bỏ phiếu đổi nhóm trưởng (docs/de-xuat-bau-nhom-truong.md mục 4). Chạy lại nhiều lần vẫn an toàn.
-- docker exec -i exe-postgres psql -U postgres -d team_db -v ON_ERROR_STOP=1 < deploy/db/migrations/BE-88_leader_votes.sql
SET ROLE team_svc;
SET search_path = team, public;

-- Đề nghị đổi nhóm trưởng. Mỗi nhóm một đề nghị đang mở; hạn 48 giờ, Team Service đóng đề nghị hết hạn khi đọc hoặc bỏ phiếu.
CREATE TABLE IF NOT EXISTS team.leader_votes (
    id                     uuid         NOT NULL,
    team_id                uuid         NOT NULL,
    proposed_by_student_id uuid         NOT NULL,
    nominee_student_id     uuid         NOT NULL,
    reason                 varchar(300),
    status                 varchar(16)  NOT NULL DEFAULT 'OPEN',
    expires_at             timestamptz  NOT NULL,
    created_at             timestamptz  NOT NULL DEFAULT now(),
    closed_at              timestamptz,
    CONSTRAINT pk_leader_votes PRIMARY KEY (id),
    CONSTRAINT fk_leader_votes_team FOREIGN KEY (team_id) REFERENCES team.teams (id) ON DELETE CASCADE,
    CONSTRAINT ck_leader_votes_status CHECK (status IN ('OPEN', 'PASSED', 'REJECTED', 'EXPIRED', 'CANCELLED')),
    CONSTRAINT ck_leader_votes_closed CHECK ((status = 'OPEN') = (closed_at IS NULL))
);
CREATE UNIQUE INDEX IF NOT EXISTS uq_leader_votes_one_open ON team.leader_votes (team_id) WHERE status = 'OPEN';

CREATE TABLE IF NOT EXISTS team.leader_vote_ballots (
    vote_id    uuid        NOT NULL,
    student_id uuid        NOT NULL,
    approve    boolean     NOT NULL,
    voted_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_leader_vote_ballots PRIMARY KEY (vote_id, student_id),
    CONSTRAINT fk_leader_vote_ballots_vote FOREIGN KEY (vote_id) REFERENCES team.leader_votes (id) ON DELETE CASCADE
);

RESET ROLE;
