import { useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
import { ApiError, toApiError } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useIsTeamFormation } from '@/shared/phase/usePhase'
import { useInvite, useMyTeam, type TeamDetail } from '@/features/suggestions/api'
import { InviteDialog } from '@/features/suggestions/InviteDialog'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useInvitedStudentIds, useStudentProfile, type StudentProfile } from './api'
import { Avatar, cardClass, Section, SkillBars } from './components'
import { ProfileSkeleton } from './ProfilePages'

/**
 * S04 – Hồ sơ sinh viên khác (FE-35): kỹ năng, giới thiệu; không có SĐT (rule 11, 16). Email chỉ hiện với người trong nhóm.
 * Trạng thái: trưởng nhóm xem (có nút mời); người khác xem; đã mời; sinh viên đã có nhóm.
 */
export function StudentProfilePage() {
  const { id = '' } = useParams()
  const { user } = useAuth()
  const profile = useStudentProfile(id)
  const team = useMyTeam()

  if (id === user?.studentId) return <Navigate to="/sv/ho-so" replace />
  const notFound = profile.error instanceof ApiError && profile.error.status === 404
  return (
    <>
      <PageHeader title="Hồ sơ sinh viên" />
      {notFound ? (
        <EmptyState icon="person_off" title="Không tìm thấy sinh viên" description="Sinh viên này không còn trong danh sách." />
      ) : profile.error ? (
        <ErrorState error={profile.error} onRetry={() => void profile.refetch()} />
      ) : !profile.data ? (
        <ProfileSkeleton />
      ) : (
        <Details student={profile.data} team={team.data ?? null} />
      )}
    </>
  )
}

function Details({ student, team }: { student: StudentProfile; team: TeamDetail | null }) {
  const teammate = team?.members.find((m) => m.id === student.id)
  // Team trả email của đồng đội; Academic chưa biết quan hệ nhóm nên có thể để trống (rule 11).
  const email = student.email || teammate?.email
  return (
    <div className="flex flex-col gap-4">
      <section className={`${cardClass} flex flex-wrap items-center gap-4`}>
        <Avatar name={student.fullName} />
        <div className="min-w-0 flex-1">
          <h2 className="text-title font-semibold">{student.fullName}</h2>
          <p className="text-body text-text-secondary">{[student.studentCode, student.specialization.name, student.classCode].filter(Boolean).join(' · ')}</p>
          {email && <p className="text-body">{email}</p>}
        </div>
        {teammate ? <StatusChip tone="success" icon="groups">Cùng nhóm với bạn</StatusChip> : <InviteAction student={student} team={team} />}
      </section>
      <Section title="Kỹ năng">
        <SkillBars ratings={student.skills} />
      </Section>
      <Section title="Giới thiệu">
        <p className="whitespace-pre-line text-body">{student.description || 'Chưa có giới thiệu.'}</p>
      </Section>
    </div>
  )
}

function InviteAction({ student, team }: { student: StudentProfile; team: TeamDetail | null }) {
  const isLeader = team?.myRelation === 'LEADER'
  const invited = useInvitedStudentIds(team?.id, isLeader)
  const invite = useInvite(team?.id)
  const snackbar = useSnackbar()
  const { isTeamFormation } = useIsTeamFormation(team?.courseCode)
  const [open, setOpen] = useState(false)
  const [sent, setSent] = useState(false)
  // hasTeam của Academic chưa có dữ liệu Team; API mời trả already-in-team là nguồn chắc chắn.
  const [joined, setJoined] = useState(false)
  const isFull = !!team && team.memberCount >= (team.requirement?.maxSize ?? 6)

  if (student.hasTeam || joined) return <StatusChip tone="neutral" icon="group">Đã có nhóm</StatusChip>
  if (!isLeader || !isTeamFormation) return null
  if (sent || invited.data?.includes(student.id)) return <StatusChip tone="info" icon="send">Đã mời</StatusChip>
  if (invited.isLoading) return null
  if (isFull) return <p className="text-body text-text-secondary">Nhóm đã đủ 6 người</p>

  const close = () => {
    setOpen(false)
    invite.reset()
  }
  return (
    <>
      <Button icon="person_add" onClick={() => setOpen(true)}>Gửi lời mời</Button>
      <InviteDialog
        student={open ? student : null}
        sending={invite.isPending}
        error={invite.error}
        onClose={close}
        onSend={(message) =>
          invite.mutate(
            { studentId: student.id, message: message || undefined },
            {
              onSuccess: () => {
                setSent(true)
                setOpen(false)
                snackbar.show('Đã gửi lời mời')
              },
              onError: (error) => {
                const code = toApiError(error).code
                if (code === 'already-in-team') setJoined(true)
                if (code === 'pending-exists') setSent(true)
              },
            },
          )
        }
      />
      {snackbar.element}
    </>
  )
}
