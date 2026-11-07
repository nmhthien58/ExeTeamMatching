import { useState } from 'react'
import { Link } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useInvite, useMyTeam, useSuggestedMembers, type StudentRef, type TeamDetail } from './api'
import { FitChip, Reasons, StudentCard } from './components'
import { InviteDialog } from './InviteDialog'

const linkClass = 'inline-flex min-h-10 items-center rounded-md px-4 font-medium'

/**
 * S18 – Gợi ý thành viên (FE-40): sinh viên bù chuyên ngành còn thiếu và kỹ năng yếu của nhóm (Matching.SuggestMembers),
 * kèm lý do; trưởng nhóm gửi lời mời. Trạng thái: có gợi ý; không có gợi ý; đã mời; nhóm đủ 6; đang tải.
 */
export function SuggestedMembersPage() {
  const team = useMyTeam()
  return (
    <>
      <PageHeader title="Gợi ý thành viên" />
      {team.error ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : team.isLoading ? (
        <ListSkeleton />
      ) : !team.data ? (
        <EmptyState icon="group_off" title="Bạn chưa có nhóm" description="Tạo nhóm hoặc xin vào một nhóm trước khi mời thành viên." />
      ) : (
        <Suggestions team={team.data} />
      )}
    </>
  )
}

function Suggestions({ team }: { team: TeamDetail }) {
  const suggestions = useSuggestedMembers(team.id)
  const invite = useInvite(team.id)
  const snackbar = useSnackbar()
  const [inviting, setInviting] = useState<StudentRef | null>(null)
  const isLeader = team.myRelation === 'LEADER'
  const isFull = team.memberCount >= (team.requirement?.maxSize ?? 6)
  const missing = suggestions.data?.missingSpecializations ?? []

  return (
    <div className="flex flex-col gap-4">
      {isFull && <Banner tone="success" title={`Nhóm đã đủ ${team.requirement?.maxSize ?? 6} người`} />}
      {!isLeader && <p className="text-body text-text-secondary">Chỉ trưởng nhóm gửi được lời mời.</p>}
      {missing.length > 0 && (
        <p className="flex flex-wrap items-center gap-2">
          <span className="font-medium">Nhóm đang thiếu:</span>
          {missing.map((m) => (
            <StatusChip key={m.id} tone="warning" icon="error">
              {m.name}
            </StatusChip>
          ))}
        </p>
      )}

      {suggestions.error ? (
        <ErrorState error={suggestions.error} onRetry={() => void suggestions.refetch()} />
      ) : suggestions.isLoading ? (
        <ListSkeleton />
      ) : suggestions.data!.items.length === 0 ? (
        <EmptyState
          icon="person_search"
          title="Chưa có gợi ý phù hợp"
          description="Tìm theo tên hoặc MSSV để mời người bạn đã biết."
          action={
            isLeader && !isFull && (
              <Link to="/sv/nhom/moi" className={`${linkClass} bg-primary text-white`}>
                Tìm sinh viên
              </Link>
            )
          }
        />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {suggestions.data!.items.map((item) => (
            <StudentCard
              key={item.student.id}
              student={item.student}
              skills={item.skills}
              action={
                item.alreadyInvited ? (
                  <StatusChip tone="warning">Đã mời</StatusChip>
                ) : (
                  isLeader && (
                    <Button variant="outline" icon="person_add" disabled={isFull} onClick={() => setInviting(item.student)}>
                      Gửi lời mời
                    </Button>
                  )
                )
              }
            >
              <FitChip score={item.score} />
              <Reasons reasons={item.reasons} />
            </StudentCard>
          ))}
        </div>
      )}

      {isLeader && !isFull && (
        <Link to="/sv/nhom/moi" className="self-start font-medium text-primary-dark hover:underline">
          Tìm theo tên hoặc MSSV
        </Link>
      )}

      <InviteDialog
        key={inviting?.id}
        student={inviting}
        sending={invite.isPending}
        error={invite.error}
        onClose={() => {
          setInviting(null)
          invite.reset()
        }}
        onSend={(message) =>
          invite.mutate(
            { studentId: inviting!.id, message: message || undefined },
            {
              onSuccess: () => {
                setInviting(null)
                snackbar.show('Đã gửi lời mời')
              },
            },
          )
        }
      />
      {snackbar.element}
    </div>
  )
}

function ListSkeleton() {
  return (
    <div role="status" aria-label="Đang tải" className="grid gap-3 md:grid-cols-2">
      {Array.from({ length: 4 }, (_, i) => (
        <Skeleton key={i} className="h-36 rounded-lg" />
      ))}
    </div>
  )
}
