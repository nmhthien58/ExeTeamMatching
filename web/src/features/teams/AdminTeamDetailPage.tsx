import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { toApiError } from '@/shared/api/apiClient'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Dialog, Icon, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Avatar, Card, Drawer } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { useTeamAuditLogs } from '@/features/audit-log/api'
import { DomainChips, TeamRequirementBar } from '@/features/suggestions/components'
import type { StudentRef, TeamMember } from '@/features/suggestions/api'
import { teamStatusLabel, type FullTeam } from '@/features/my_team/api'
import { ListSkeleton } from '@/features/my_team/components'
import { useAdminTeam, useAdminTeamAction, useMoveTargets, useUnmatchedStudents, type AdminTeamAction } from './api'

type Pending = { action: AdminTeamAction; title: string; body: string; confirm: string; done: string }

/**
 * W16 – Chi tiết nhóm cho Bộ môn (FE-22): thành viên, ý tưởng, cố vấn, điều kiện, nhật ký thay đổi; Admin chốt / mở lại nhóm
 * (quy tắc 13), thêm sinh viên chưa có nhóm, chuyển thành viên sang nhóm khác – mọi thao tác qua dialog xác nhận.
 */
// shortcut: chưa bớt thành viên khỏi nhóm (API chỉ có thêm / chuyển cho Admin); mục Mentor chỉ hiện tên (gán / gỡ ở BE-74, BE-80).
export function AdminTeamDetailPage() {
  const { teamId = '' } = useParams()
  const team = useAdminTeam(teamId)
  if (team.error) return <ErrorState error={team.error} onRetry={() => void team.refetch()} />
  if (team.isLoading) return <ListSkeleton />
  return <Detail team={team.data!} />
}

function Detail({ team }: { team: FullTeam }) {
  const action = useAdminTeamAction(team.id)
  const snackbar = useSnackbar()
  const [pending, setPending] = useState<Pending | null>(null)
  const [adding, setAdding] = useState(false)
  const [moving, setMoving] = useState<TeamMember | null>(null)
  const status = teamStatusLabel[team.status]
  const maxSize = team.requirement?.maxSize ?? 6
  const finalized = team.status === 'FINALIZED'
  const disbanded = team.status === 'DISBANDED'
  const error = action.error ? toApiError(action.error) : null

  const confirm = () =>
    pending &&
    action.mutate(pending.action, {
      onSuccess: () => {
        snackbar.show(pending.done)
        setPending(null)
        setAdding(false)
        setMoving(null)
      },
    })
  const ask = (p: Pending) => {
    action.reset()
    setPending(p)
  }

  return (
    <>
      <PageHeader
        title={team.name}
        action={
          !disbanded && (
            <Button
              icon={finalized ? 'lock_open' : 'verified'}
              onClick={() =>
                ask(
                  finalized
                    ? { action: { kind: 'reopen' }, title: 'Mở lại nhóm?', body: 'Nhóm trở lại trạng thái đang hoạt động, thành viên sửa được nhóm.', confirm: 'Mở lại', done: 'Đã mở lại nhóm' }
                    : { action: { kind: 'finalize' }, title: 'Chốt nhóm?', body: 'Hệ thống kiểm tra 4–6 thành viên và ít nhất 2 chuyên ngành; sau khi chốt nhóm không tự đổi thành viên được.', confirm: 'Chốt nhóm', done: 'Đã chốt nhóm' },
                )
              }
            >
              {finalized ? 'Mở lại nhóm' : 'Chốt nhóm'}
            </Button>
          )
        }
      />
      <Link to={`/admin/nhom?mon=${team.courseCode}`} className="mb-4 inline-flex min-h-10 items-center gap-1 font-medium text-info-dark">
        <Icon name="arrow_back" size={20} /> Danh sách nhóm
      </Link>
      <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
        <div className="flex flex-col gap-4">
          <Card>
            <div className="flex flex-wrap items-center gap-2">
              <StatusChip tone={status.tone} icon={status.icon}>
                {status.text}
              </StatusChip>
              <span className="text-text-secondary">{team.courseCode}</span>
              {team.isInherited && <StatusChip tone="info">Kế thừa EXE101</StatusChip>}
              {team.isAutoAssigned && <StatusChip tone="info">Ghép tự động</StatusChip>}
            </div>
            {team.projectName && <p className="mt-2 text-title">{team.projectName}</p>}
            <p className="mt-1 text-text-secondary">{team.ideaDescription ?? 'Chưa có mô tả ý tưởng.'}</p>
            <div className="mt-3">
              <DomainChips domains={team.domains} />
            </div>
          </Card>
          <Card>
            <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
              <h2 className="font-semibold">
                Thành viên ({team.memberCount}/{maxSize})
              </h2>
              {!finalized && !disbanded && team.memberCount < maxSize && (
                <Button variant="outline" icon="person_add" onClick={() => setAdding(true)}>
                  Thêm thành viên
                </Button>
              )}
            </div>
            <TeamRequirementBar memberCount={team.memberCount} maxSize={maxSize} />
            {team.requirement && !team.requirement.isValid && (
              <p className="mt-2 text-warning">
                Chưa đủ điều kiện: cần {team.requirement.minSize}–{maxSize} thành viên và ít nhất {team.requirement.minSpecializations} chuyên ngành
                {team.requirement.missingSpecializations.length > 0 && ` (thiếu ${team.requirement.missingSpecializations.map((s) => s.name).join(', ')})`}.
              </p>
            )}
            <ul className="mt-3 flex flex-col">
              {team.members.map((m) => (
                <li key={m.id} className="flex min-h-14 items-center gap-3 border-b border-border last:border-b-0">
                  <Avatar name={m.fullName} />
                  <span className="min-w-0 flex-1">
                    <span className="flex items-center gap-2 font-medium">
                      {m.fullName}
                      {m.role === 'LEADER' && <span className="rounded-full bg-neutral-container px-2 text-caption font-semibold text-text-secondary">Trưởng nhóm</span>}
                    </span>
                    <span className="text-caption text-text-secondary">
                      {m.studentCode}
                      {m.specialization && ` · ${m.specialization.name}`}
                    </span>
                  </span>
                  {!finalized && !disbanded && (
                    <Button variant="text" icon="swap_horiz" onClick={() => setMoving(m)}>
                      Chuyển nhóm
                    </Button>
                  )}
                </li>
              ))}
            </ul>
          </Card>
          <History teamId={team.id} />
        </div>
        <Card className="self-start">
          <h2 className="mb-2 font-semibold">Cố vấn</h2>
          <p>Giảng viên: {team.lecturerName ?? 'Chưa ghép'}</p>
          <p>Mentor: {team.mentorName ?? 'Chưa ghép'}</p>
          {team.finalizedAt && <p className="mt-2 text-caption text-text-secondary">Chốt lúc {formatDateTime(team.finalizedAt)}</p>}
        </Card>
      </div>

      <AddMemberDrawer
        open={adding}
        course={team.courseCode}
        onClose={() => setAdding(false)}
        onPick={(s, reason) =>
          ask({ action: { kind: 'add', studentId: s.id, reason }, title: `Thêm ${s.fullName} vào nhóm?`, body: `${s.studentCode} vào ${team.name} ngay, được thông báo trong ứng dụng.`, confirm: 'Thêm', done: 'Đã thêm thành viên' })
        }
      />
      {/* Chỉ mount khi đang chọn: Dialog dùng chung id tiêu đề cố định nên hai dialog cùng lúc sẽ trùng tên. */}
      {moving && (
        <MoveDialog
          key={moving.id}
          member={moving}
          team={team}
          onClose={() => setMoving(null)}
          onPick={(target) => {
            ask({
              action: { kind: 'move', studentId: moving.id, targetTeamId: target.id },
              title: `Chuyển ${moving.fullName}?`,
              body: `Từ ${team.name} sang ${target.name}.${moving.role === 'LEADER' ? ' Trưởng nhóm được chuyển cho thành viên khác.' : ''}`,
              confirm: 'Chuyển',
              done: 'Đã chuyển thành viên',
            })
            setMoving(null)
          }}
        />
      )}
      <Dialog
        open={!!pending}
        onClose={() => setPending(null)}
        title={pending?.title ?? ''}
        actions={
          <>
            <Button variant="text" onClick={() => setPending(null)}>
              Hủy
            </Button>
            <Button loading={action.isPending} onClick={confirm}>
              {pending?.confirm}
            </Button>
          </>
        }
      >
        {pending?.body}
        {error && (
          <p role="alert" className="mt-2 text-error">
            {error.detail ?? error.title}
          </p>
        )}
      </Dialog>
      {snackbar.element}
    </>
  )
}

function History({ teamId }: { teamId: string }) {
  const [page, setPage] = useState(1)
  const logs = useTeamAuditLogs(teamId, page)
  return (
    <Card>
      <h2 className="mb-2 font-semibold">Nhật ký thay đổi</h2>
      {logs.error ? (
        <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />
      ) : logs.isLoading ? (
        <ListSkeleton count={2} />
      ) : logs.data!.items.length === 0 ? (
        <p className="text-text-secondary">Chưa có thay đổi.</p>
      ) : (
        <>
          <ol aria-label="Nhật ký thay đổi">
            {logs.data!.items.map((log) => (
              <li key={log.id} className="border-b border-border py-2 last:border-b-0">
                <span className="block">{log.summary}</span>
                <span className="text-caption text-text-secondary">
                  {log.actorName} · {formatDateTime(log.occurredAt)}
                  {log.reason && ` · ${log.reason}`}
                </span>
              </li>
            ))}
          </ol>
          {logs.data!.totalPages > 1 && (
            <nav aria-label="Phân trang nhật ký" className="mt-2 flex items-center justify-center gap-3">
              <Button variant="text" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                Mới hơn
              </Button>
              <span>
                Trang {page}/{logs.data!.totalPages}
              </span>
              <Button variant="text" disabled={page >= logs.data!.totalPages} onClick={() => setPage(page + 1)}>
                Cũ hơn
              </Button>
            </nav>
          )}
        </>
      )}
    </Card>
  )
}

/** Chọn sinh viên chưa có nhóm cùng môn để thêm vào nhóm, kèm lý do (ghi vào nhật ký). */
function AddMemberDrawer({ open, course, onClose, onPick }: { open: boolean; course: FullTeam['courseCode']; onClose: () => void; onPick: (s: StudentRef, reason?: string) => void }) {
  const [search, setSearch] = useState('')
  const [reason, setReason] = useState('')
  const students = useUnmatchedStudents(open ? course : undefined, search.trim())
  return (
    <Drawer open={open} onClose={onClose} title="Thêm thành viên">
      <label className="mb-3 flex flex-col gap-1">
        <span className="font-medium">Tìm sinh viên chưa có nhóm</span>
        <input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Tên hoặc MSSV" className="rounded-sm border border-border-strong px-3 py-2" />
      </label>
      <label className="mb-3 flex flex-col gap-1">
        <span className="font-medium">Lý do (không bắt buộc)</span>
        <input value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} className="rounded-sm border border-border-strong px-3 py-2" />
      </label>
      {students.error ? (
        <ErrorState error={students.error} onRetry={() => void students.refetch()} />
      ) : !students.data ? (
        <ListSkeleton count={2} />
      ) : students.data.length === 0 ? (
        <EmptyState icon="person_search" title="Không có sinh viên phù hợp" description={`Mọi sinh viên ${course} đã có nhóm hoặc không khớp từ khóa.`} />
      ) : (
        <ul>
          {students.data.map((s) => (
            <li key={s.id} className="flex min-h-14 items-center gap-3 border-b border-border last:border-b-0">
              <span className="flex-1">
                <span className="block font-medium">{s.fullName}</span>
                <span className="text-caption text-text-secondary">
                  {s.studentCode}
                  {s.specialization && ` · ${s.specialization.name}`}
                </span>
              </span>
              <Button variant="outline" onClick={() => onPick(s, reason.trim() || undefined)}>
                Chọn
              </Button>
            </li>
          ))}
        </ul>
      )}
    </Drawer>
  )
}

/** Chọn nhóm đích (cùng môn, còn chỗ) để chuyển thành viên sang. */
function MoveDialog({ member, team, onClose, onPick }: { member: TeamMember | null; team: FullTeam; onClose: () => void; onPick: (target: { id: string; name: string }) => void }) {
  const targets = useMoveTargets(member ? team.courseCode : undefined, team.id)
  const [targetId, setTargetId] = useState('')
  const target = targets.data?.find((t) => t.id === targetId)
  return (
    <Dialog
      open={!!member}
      onClose={onClose}
      title={`Chuyển ${member?.fullName ?? ''} sang nhóm khác`}
      actions={
        <>
          <Button variant="text" onClick={onClose}>
            Hủy
          </Button>
          <Button disabled={!target} onClick={() => target && onPick(target)}>
            Tiếp tục
          </Button>
        </>
      }
    >
      {targets.error ? (
        <Banner tone="error" title="Không tải được danh sách nhóm" />
      ) : (
        <label className="flex flex-col gap-1 text-text-primary">
          <span className="font-medium">Nhóm đích</span>
          <select value={targetId} onChange={(e) => setTargetId(e.target.value)} className="h-10 rounded-sm border border-border-strong bg-surface px-3">
            <option value="">Chọn nhóm còn chỗ</option>
            {(targets.data ?? []).map((t) => (
              <option key={t.id} value={t.id}>
                {t.name} · {t.memberCount}/{t.requirement?.maxSize ?? 6}
              </option>
            ))}
          </select>
        </label>
      )}
    </Dialog>
  )
}
