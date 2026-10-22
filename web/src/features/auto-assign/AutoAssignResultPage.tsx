import { useState, type DragEvent } from 'react'
import clsx from 'clsx'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Banner, Button, Icon, Skeleton, Spinner, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useIsTeamFormation } from '@/shared/phase/usePhase'
import type { StudentRef } from '@/features/suggestions/api'
import { useCourseParam, useCurrentSemester } from '@/features/dashboard/adminApi'
import { CourseToggle } from '@/features/dashboard/CourseToggle'
import { formatDateTime } from '@/features/workspace/format'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { PartialSaveError, useAutoAssignResult, useRunAutoAssign, useSaveMoves, useTeamDetails, type AutoAssignResult, type MemberMove } from './api'
import { AutoAssignTabs } from './AutoAssignTabs'
import { addMove, applyMoves, checkComposition, type DraftTeam } from './compose'

/**
 * W21 – Kết quả ghép tự động và điều chỉnh (FE-27, rule 14): nhóm được bổ sung / tạo mới, chuyển sinh viên giữa các nhóm
 * (kéo thả hoặc chọn "Chuyển sang…"), kiểm tra 4–6 người và ≥ 2 chuyên ngành ngay tại chỗ, rồi lưu một lần.
 * Trạng thái: đang chạy; đã ghép xong; còn sinh viên không ghép được.
 */
export function AutoAssignResultPage() {
  const [course, setCourse] = useCourseParam()
  const semester = useCurrentSemester()
  const result = useAutoAssignResult(semester.data?.id, course)
  const run = useRunAutoAssign(semester.data?.id, course)
  const { isTeamFormation, phase } = useIsTeamFormation(course)
  const afterDeadline = !!phase && !isTeamFormation

  return (
    <>
      <PageHeader title="Kết quả ghép tự động" />
      <AutoAssignTabs />
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <CourseToggle value={course} onChange={setCourse} />
        {afterDeadline && result.data && (
          <Button variant="outline" icon="refresh" loading={run.isPending} className="ml-auto" onClick={() => run.mutate()}>
            Chạy lại
          </Button>
        )}
      </div>

      {run.isPending ? (
        <div role="status" className="flex items-center gap-3 rounded-md bg-secondary-container px-4 py-3 font-semibold text-secondary-dark">
          <Spinner />
          Đang ghép sinh viên còn lẻ…
        </div>
      ) : (semester.error ?? result.error ?? run.error) ? (
        <ErrorState error={semester.error ?? result.error ?? run.error} onRetry={() => void result.refetch()} />
      ) : result.isLoading || semester.isLoading ? (
        <Skeleton className="h-64 rounded-lg" />
      ) : !result.data ? (
        <div className="rounded-lg bg-surface shadow-elevation-1">
          <EmptyState
            icon="auto_awesome"
            title="Chưa chạy ghép tự động"
            description={afterDeadline ? 'Ghép sinh viên còn lẻ vào nhóm cần xem lại và lập nhóm mới.' : 'Hệ thống tự ghép ngay sau hạn chót của đợt lập nhóm.'}
            action={afterDeadline && <Button onClick={() => run.mutate()}>Chạy ghép tự động</Button>}
          />
        </div>
      ) : (
        <Adjustments result={result.data} />
      )}
    </>
  )
}

function Adjustments({ result }: { result: AutoAssignResult }) {
  const newIds = new Set(result.newTeams.map((t) => t.id))
  const teamCards = [...result.filledTeams, ...result.newTeams]
  const details = useTeamDetails(teamCards.map((t) => t.id))
  const save = useSaveMoves()
  const snackbar = useSnackbar()
  const [moves, setMoves] = useState<MemberMove[]>([])
  const loading = details.some((d) => d.isLoading)
  const failed = details.find((d) => d.error)

  const base: DraftTeam[] = teamCards.map((t, i) => ({ id: t.id, name: t.name, isNew: newIds.has(t.id), members: details[i]?.data?.members ?? [] }))
  const draft = applyMoves(base, result.unassignedStudents, moves)
  const movedIds = new Set(moves.map((m) => m.studentId))
  const blocked = draft.teams.some((t) => checkComposition(t.members).tooMany)
  const stillInvalid = draft.teams.filter((t) => !checkComposition(t.members).isValid).length

  const moveTo = (student: StudentRef, toTeamId: string) => {
    const from = draft.teams.find((t) => t.members.some((m) => m.id === student.id))?.id ?? null
    if (from !== toTeamId) setMoves((current) => addMove(current, { studentId: student.id, fromTeamId: from, toTeamId }))
  }

  const saveAll = () =>
    save.mutate(moves, {
      onSuccess: (count) => {
        setMoves([])
        snackbar.show(`Đã lưu ${count} điều chỉnh`)
      },
      onError: (error) => {
        if (error instanceof PartialSaveError) setMoves((current) => current.slice(error.saved))
      },
    })

  if (failed?.error) return <ErrorState error={failed.error} onRetry={() => void failed.refetch()} />

  return (
    <div className="flex flex-col gap-4">
      <p className="text-body text-text-secondary">
        Ghép lúc {formatDateTime(result.runAt)} · Bổ sung {result.filledTeams.length} nhóm · Tạo mới {result.newTeams.length} nhóm
      </p>
      {draft.unassigned.length > 0 ? (
        <Banner tone="warning" title={`Còn ${draft.unassigned.length} sinh viên chưa ghép được`}>
          Thêm từng người vào nhóm còn chỗ, hoặc để ở W22 sau khi chốt.
        </Banner>
      ) : (
        <Banner tone="success" title="Mọi sinh viên đã có nhóm" />
      )}

      <div className="sticky top-16 z-10 flex flex-wrap items-center gap-3 rounded-md bg-surface px-4 py-3 shadow-elevation-1">
        <span className="font-medium">{moves.length === 0 ? 'Chưa có điều chỉnh' : `${moves.length} điều chỉnh chưa lưu`}</span>
        {stillInvalid > 0 && (
          <StatusChip tone="warning" icon="error">
            {stillInvalid} nhóm chưa đủ điều kiện
          </StatusChip>
        )}
        {blocked && <span className="text-body text-error">Có nhóm quá 6 người – chuyển bớt trước khi lưu.</span>}
        {save.error && (
          <span role="alert" className="text-body text-error">
            {save.error instanceof PartialSaveError ? `Đã lưu ${save.error.saved}, lỗi: ${save.error.cause.detail ?? save.error.cause.title}` : 'Lưu không thành công'}
          </span>
        )}
        <div className="ml-auto flex gap-2">
          {moves.length > 0 && (
            <Button variant="text" onClick={() => setMoves([])}>
              Bỏ điều chỉnh
            </Button>
          )}
          <Button loading={save.isPending} disabled={moves.length === 0 || blocked} onClick={saveAll}>
            Lưu điều chỉnh
          </Button>
        </div>
      </div>

      {loading ? (
        <div role="status" aria-label="Đang tải nhóm" className="grid gap-4 lg:grid-cols-3">
          {teamCards.map((t) => (
            <Skeleton key={t.id} className="h-56 rounded-lg" />
          ))}
        </div>
      ) : (
        <div className="grid items-start gap-4 lg:grid-cols-3">
          {draft.unassigned.length > 0 && (
            <StudentColumn title="Chưa ghép" icon="person_off" students={draft.unassigned} teams={draft.teams} movedIds={movedIds} onMove={moveTo} />
          )}
          {draft.teams.map((team) => (
            <TeamColumn key={team.id} team={team} teams={draft.teams} movedIds={movedIds} onMove={moveTo} />
          ))}
        </div>
      )}
      {snackbar.element}
    </div>
  )
}

function TeamColumn({ team, teams, movedIds, onMove }: {
  team: DraftTeam
  teams: DraftTeam[]
  movedIds: Set<string>
  onMove: (student: StudentRef, toTeamId: string) => void
}) {
  const [over, setOver] = useState(false)
  const composition = checkComposition(team.members)
  const onDrop = (e: DragEvent) => {
    e.preventDefault()
    setOver(false)
    const student = JSON.parse(e.dataTransfer.getData('application/json')) as StudentRef
    onMove(student, team.id)
  }
  return (
    <section
      aria-label={team.name}
      onDragOver={(e) => {
        e.preventDefault()
        setOver(true)
      }}
      onDragLeave={() => setOver(false)}
      onDrop={onDrop}
      className={clsx('rounded-lg bg-surface p-4 shadow-elevation-1', over && 'ring-2 ring-secondary')}
    >
      <header className="mb-2 flex flex-wrap items-center gap-2">
        <h2 className="min-w-0 flex-1 truncate text-subtitle font-semibold">{team.name}</h2>
        <StatusChip tone="neutral" icon={team.isNew ? 'auto_awesome' : 'group_add'}>
          {team.isNew ? 'Nhóm mới' : 'Bổ sung'}
        </StatusChip>
      </header>
      <p className="mb-2 flex flex-wrap items-center gap-2">
        {composition.isValid ? (
          <StatusChip tone="success">Đủ điều kiện</StatusChip>
        ) : (
          <StatusChip tone="warning" icon="error">
            {composition.problems.join(' · ')}
          </StatusChip>
        )}
        <span className="text-body text-text-secondary">{team.members.length}/6 thành viên</span>
      </p>
      <MemberList students={team.members} teams={teams.filter((t) => t.id !== team.id)} movedIds={movedIds} onMove={onMove} />
    </section>
  )
}

function StudentColumn({ title, icon, students, teams, movedIds, onMove }: {
  title: string
  icon: string
  students: StudentRef[]
  teams: DraftTeam[]
  movedIds: Set<string>
  onMove: (student: StudentRef, toTeamId: string) => void
}) {
  return (
    <section aria-label={title} className="rounded-lg bg-warning-container p-4">
      <h2 className="mb-2 flex items-center gap-2 text-subtitle font-semibold text-warning">
        <Icon name={icon} />
        {title} ({students.length})
      </h2>
      <MemberList students={students} teams={teams} movedIds={movedIds} onMove={onMove} />
    </section>
  )
}

/** Dòng thành viên kéo được; menu "Chuyển sang…" là cách làm không cần kéo. */
function MemberList({ students, teams, movedIds, onMove }: {
  students: StudentRef[]
  teams: DraftTeam[]
  movedIds: Set<string>
  onMove: (student: StudentRef, toTeamId: string) => void
}) {
  return (
    <ul className="flex flex-col">
      {students.map((s) => (
        <li
          key={s.id}
          draggable
          onDragStart={(e) => e.dataTransfer.setData('application/json', JSON.stringify(s))}
          className="flex min-h-14 cursor-grab items-center gap-2 border-b border-border py-2 last:border-none active:cursor-grabbing"
        >
          <Icon name="drag_indicator" className="text-text-secondary" />
          <span className="min-w-0 flex-1">
            <span className="flex items-center gap-2 font-medium">
              <span className="truncate">{s.fullName}</span>
              {movedIds.has(s.id) && <span className="shrink-0 rounded-full bg-secondary-container px-2 text-caption font-semibold text-secondary-dark">Mới chuyển</span>}
            </span>
            <span className="block text-body text-text-secondary">
              {s.studentCode} · {s.specialization?.name ?? 'Chưa rõ chuyên ngành'}
            </span>
          </span>
          <label>
            <span className="sr-only">Chuyển {s.fullName} sang</span>
            <select value="" onChange={(e) => e.target.value && onMove(s, e.target.value)} className="h-10 max-w-36 rounded-sm border border-border-strong bg-surface px-2 text-body">
              <option value="">Chuyển sang…</option>
              {teams.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name} ({t.members.length})
                </option>
              ))}
            </select>
          </label>
        </li>
      ))}
    </ul>
  )
}
