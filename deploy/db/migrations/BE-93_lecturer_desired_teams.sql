-- Migration BE-93: Add desired_teams and profile_completed_at to academic.lecturers
ALTER TABLE academic.lecturers
    ADD COLUMN IF NOT EXISTS desired_teams_exe101 smallint,
    ADD COLUMN IF NOT EXISTS desired_teams_exe201 smallint,
    ADD COLUMN IF NOT EXISTS profile_completed_at timestamptz;

ALTER TABLE academic.lecturers
    DROP CONSTRAINT IF EXISTS ck_lecturers_desired_teams;

ALTER TABLE academic.lecturers
    ADD CONSTRAINT ck_lecturers_desired_teams CHECK (
        (desired_teams_exe101 IS NULL OR desired_teams_exe101 BETWEEN 0 AND 200)
        AND (desired_teams_exe201 IS NULL OR desired_teams_exe201 BETWEEN 0 AND 200));
