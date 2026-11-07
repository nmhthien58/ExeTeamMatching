import { useEffect, useState } from 'react'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Icon, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useInvite, useMyTeam, useSpecializations, useStudentSearch, type StudentRef } from './api'
import { StudentCard } from './components'
import { InviteDialog } from './InviteDialog'

/** Chờ người dùng ngừng gõ rồi mới tìm. */
function useDebounced<T>(value: T, ms = 300) {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), ms)
    return () => window.clearTimeout(timer)
  }, [value, ms])
  return debounced
}

/**
 * S19 – Tìm và mời sinh viên (FE-40): tìm theo tên / MSSV, lọc chuyên ngành, chỉ sinh viên chưa có nhóm; gửi lời mời kèm lời nhắn.
 * Trạng thái: kết quả; không tìm thấy; nhóm đã đủ người; đang gửi.
 */
export function InviteStudentsPage() {
  const team = useMyTeam()
  const specializations = useSpecializations()
  const [search, setSearch] = useState('')
  const [specializationId, setSpecializationId] = useState<number | undefined>()
  const results = useStudentSearch(useDebounced(search.trim()), specializationId)
  const invite = useInvite(team.data?.id)
  const snackbar = useSnackbar()
  const [inviting, setInviting] = useState<StudentRef | null>(null)
  const [invited, setInvited] = useState<Set<string>>(new Set())
  const isLeader = team.data?.myRelation === 'LEADER'
  const isFull = !!team.data && team.data.memberCount >= (team.data.requirement?.maxSize ?? 6)

  return (
    <>
      <PageHeader title="Mời sinh viên" />
      <div className="flex flex-col gap-4">
        {isFull && <Banner tone="success" title={`Nhóm đã đủ ${team.data?.requirement?.maxSize ?? 6} người`}>Không gửi thêm lời mời được.</Banner>}
        {team.data && !isLeader && <p className="text-body text-text-secondary">Chỉ trưởng nhóm gửi được lời mời.</p>}

        <div className="flex flex-wrap gap-3">
          <label className="relative min-w-0 flex-1 basis-64">
            <span className="sr-only">Tìm theo tên hoặc MSSV</span>
            <Icon name="search" className="pointer-events-none absolute left-3 top-2 text-text-secondary" />
            <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Tên hoặc MSSV"
              className="min-h-10 w-full rounded-sm border border-border-strong bg-surface pl-10 pr-3" />
          </label>
          <label className="flex basis-56 flex-col">
            <span className="sr-only">Chuyên ngành</span>
            <select value={specializationId ?? ''} onChange={(e) => setSpecializationId(e.target.value ? Number(e.target.value) : undefined)}
              className="min-h-10 rounded-sm border border-border-strong bg-surface px-3">
              <option value="">Mọi chuyên ngành</option>
              {specializations.data?.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </label>
        </div>

        {results.error ? (
          <ErrorState error={results.error} onRetry={() => void results.refetch()} />
        ) : results.isLoading ? (
          <div role="status" aria-label="Đang tìm" className="grid gap-3 md:grid-cols-2">
            {Array.from({ length: 4 }, (_, i) => (
              <Skeleton key={i} className="h-28 rounded-lg" />
            ))}
          </div>
        ) : results.data!.items.length === 0 ? (
          <EmptyState icon="search_off" title="Không tìm thấy sinh viên chưa có nhóm" description="Thử tên khác, MSSV đầy đủ hoặc bỏ lọc chuyên ngành." />
        ) : (
          <div className="grid gap-3 md:grid-cols-2">
            {results.data!.items.map((student) => (
              <StudentCard
                key={student.id}
                student={student}
                action={
                  isLeader &&
                  (invited.has(student.id) ? (
                    <span className="text-body text-text-secondary">Đã gửi lời mời</span>
                  ) : (
                    <Button variant="outline" icon="person_add" disabled={isFull} onClick={() => setInviting(student)}>
                      Gửi lời mời
                    </Button>
                  ))
                }
              />
            ))}
          </div>
        )}
      </div>

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
                setInvited((s) => new Set(s).add(inviting!.id))
                setInviting(null)
                snackbar.show('Đã gửi lời mời')
              },
            },
          )
        }
      />
      {snackbar.element}
    </>
  )
}
