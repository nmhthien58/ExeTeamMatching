import { Link } from 'react-router-dom'
import { ErrorState } from '@/shared/components/States'
import { Skeleton, StatusChip } from '@/shared/components/ui'
import { Drawer } from '@/features/workspace/components'
import { SkillBars } from '@/features/profile/components'
import { useAdminStudent, useCurrentTeam, type AdminStudentProfile } from './api'

/**
 * W10 – Chi tiết sinh viên (FE-16): drawer mở từ W09. Hồ sơ (Admin thấy SĐT, email), kỹ năng, giới thiệu, nhóm hiện tại.
 * Lịch sử lời mời và thay đổi nhóm chưa có API theo sinh viên ở Team Service nên chưa hiện.
 */
export function StudentDrawer({ id, onClose }: { id: string; onClose: () => void }) {
  const student = useAdminStudent(id)
  return (
    <Drawer open onClose={onClose} title={student.data?.fullName ?? 'Chi tiết sinh viên'}>
      {student.error ? (
        <ErrorState error={student.error} onRetry={() => void student.refetch()} />
      ) : !student.data ? (
        <div className="flex flex-col gap-3" aria-busy>
          <Skeleton className="h-20 w-full" />
          <Skeleton className="h-32 w-full" />
        </div>
      ) : (
        <Details student={student.data} />
      )}
    </Drawer>
  )
}

function Row({ label, value }: { label: string; value: string | null | undefined }) {
  return (
    <div className="grid grid-cols-[120px_1fr] gap-2">
      <dt className="text-text-secondary">{label}</dt>
      <dd>{value || '–'}</dd>
    </div>
  )
}

function Details({ student }: { student: AdminStudentProfile }) {
  return (
    <div className="flex flex-col gap-5">
      <dl className="flex flex-col gap-2 text-body">
        <Row label="MSSV" value={student.studentCode} />
        <Row label="Email" value={student.email} />
        <Row label="SĐT" value={student.phone} />
        <Row label="Chuyên ngành" value={student.specialization?.name} />
        <Row label="Mã lớp" value={student.classCode} />
        <Row label="Môn" value={student.courseCode} />
      </dl>
      <section>
        <h3 className="mb-2 font-semibold">Nhóm hiện tại</h3>
        <CurrentTeam student={student} />
      </section>
      <section>
        <h3 className="mb-2 font-semibold">Kỹ năng</h3>
        {student.profileCompleted ? <SkillBars ratings={student.skills} /> : <p className="text-body text-text-secondary">Sinh viên chưa hoàn thiện hồ sơ.</p>}
      </section>
      <section>
        <h3 className="mb-2 font-semibold">Giới thiệu</h3>
        <p className="whitespace-pre-line text-body">{student.description || 'Chưa có giới thiệu.'}</p>
      </section>
    </div>
  )
}

function CurrentTeam({ student }: { student: AdminStudentProfile }) {
  const team = useCurrentTeam(student.currentTeamId)
  if (!student.currentTeamId) {
    return (
      <StatusChip tone={student.isSeekingTeam ? 'warning' : 'neutral'} icon="person_search">
        {student.isSeekingTeam ? 'Chưa có nhóm · đang tìm nhóm' : 'Chưa có nhóm'}
      </StatusChip>
    )
  }
  if (team.error) return <p className="text-body text-error">Không tải được thông tin nhóm.</p>
  if (!team.data) return <Skeleton className="h-10 w-full" />
  return (
    <Link to={`/admin/nhom/${team.data.id}`} className="flex flex-col rounded-md border border-border px-3 py-2 hover:bg-background">
      <span className="font-medium">{team.data.name}</span>
      <span className="text-caption text-text-secondary">
        {team.data.memberCount} thành viên{team.data.lecturerName ? ` · GV ${team.data.lecturerName}` : ''}
      </span>
    </Link>
  )
}
