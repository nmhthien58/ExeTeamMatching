import { useIsTeamFormation } from '@/shared/phase/usePhase'
import { Card } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import type { FullTeam } from './api'
import { lecturerReasonLabel, mentorReasonLabel, useCheckpointsDone, useTeamLecturer, useTeamMentor } from './advisorsApi'

function Advisor({ title, name, reason }: { title: string; name: string; reason?: string }) {
  return (
    <p className="flex flex-wrap items-center gap-2">
      <span>
        {title}: {name}
      </span>
      {reason && <span className="rounded-full bg-info-container px-2 text-caption font-semibold text-info-dark">{reason}</span>}
    </p>
  )
}

/**
 * Mục Cố vấn ở S15 (flutter-handoff S15): giảng viên và mentor kèm lý do ghép; chưa ghép → "Chốt giảng viên lúc 18:00 · 17/09/2026"
 * / "Ghép từ tuần 6". API lỗi hoặc đang tải thì dùng tên có sẵn trong thẻ nhóm.
 */
export function AdvisorsCard({ team }: { team: FullTeam }) {
  const lecturer = useTeamLecturer(team.id).data
  const mentor = useTeamMentor(team.id).data
  const deadline = useIsTeamFormation(team.courseCode).phase?.teamFormationDeadlineAt
  return (
    <Card>
      <h3 className="mb-2 font-semibold">Cố vấn</h3>
      <Advisor
        title="Giảng viên"
        name={lecturer?.lecturerName ?? team.lecturerName ?? (deadline ? `Chốt giảng viên lúc ${formatDateTime(deadline)}` : 'Chưa ghép')}
        reason={lecturerReasonLabel(lecturer?.reason, lecturer?.preferenceRank)}
      />
      <Advisor title="Mentor" name={mentor?.mentorName ?? team.mentorName ?? 'Ghép từ tuần 6'} reason={mentorReasonLabel(mentor?.reason)} />
    </Card>
  )
}

/** "Checkpoint n/4" của nhóm EXE101 (quy tắc 27); ẩn khi chưa có dự án hoặc nhóm EXE201. */
export function CheckpointCard({ team, workspaceId }: { team: FullTeam; workspaceId?: string }) {
  const done = useCheckpointsDone(team.courseCode === 'EXE101' ? workspaceId : undefined).data
  if (done === undefined) return null
  return (
    <Card>
      <h3 className="mb-2 font-semibold">Checkpoint {done}/4</h3>
      <div role="progressbar" aria-label="Checkpoint đã trình bày" aria-valuemin={0} aria-valuemax={4} aria-valuenow={done} className="h-2 rounded-full bg-neutral-container">
        <div className="h-2 rounded-full bg-primary" style={{ width: `${(done / 4) * 100}%` }} />
      </div>
    </Card>
  )
}
