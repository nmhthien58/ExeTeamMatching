import { useState } from 'react'
import clsx from 'clsx'
import { Banner } from '@/shared/components/ui'
import { timeLeft, useCourseParam } from '@/features/dashboard/adminApi'
import { lecturerReasonLabel } from '@/features/my_team/advisorsApi'
import { formatDateTime } from '@/features/workspace/format'
import {
  countInterestedAdvisors,
  PAGE_SIZE,
  useConfirmMatching,
  useDiscardMatching,
  useLecturerMatchingOverview,
  usePreviewMatching,
  type AdvisorMatchingOverview,
  type AdvisorMatchingRow,
} from './api'
import { ChangeLecturerDialog } from './ChangeLecturerDialog'
import { matchingErrorMessage } from './errors'
import { LoadPanel } from './LoadPanel'
import { AdvisorCell, EditCell, InterestsCell, MatchedSummary, MatchingLayout, MatchingTable, ReasonCell, StatCard, SummarySkeleton } from './MatchingLayout'
import { useEarlyMatching, useMatchingParams } from './useMatchingPage'

/**
 * W19 – Ghép giảng viên hướng dẫn (FE-25, quy tắc 9, 19).
 * Trước hạn: tiến độ (nhóm đã có nguyện vọng, giảng viên đang quan tâm) và "Ghép sớm" → xem trước → chốt.
 * Sau khi ghép: bảng nhóm · giảng viên · lý do · nguyện vọng · quan tâm, lọc "Chưa có giảng viên",
 * thanh tải từng giảng viên, đổi giảng viên tay kèm lý do.
 */
export function LecturerMatchingPage() {
  const [course, setCourse] = useCourseParam()
  const params = useMatchingParams('chua-co-gv')
  const { withoutAdvisor, search, page } = params
  const overview = useLecturerMatchingOverview(course, { withoutAdvisor, search, page })
  const data = overview.data
  const early = useEarlyMatching({
    preview: usePreviewMatching(course),
    confirm: useConfirmMatching(),
    discard: useDiscardMatching(),
    errorMessage: matchingErrorMessage,
    confirmedMessage: (run) => `Đã ghép giảng viên cho ${run.matchedCount} nhóm`,
  })
  const [editing, setEditing] = useState<AdvisorMatchingRow | null>(null)

  const beforeMatching = !!data?.matchingAt && data.lastRun?.status !== 'APPLIED'

  return (
    <MatchingLayout
      title="Ghép giảng viên"
      advisorNoun="giảng viên"
      tableLabel="Nhóm và giảng viên"
      course={course}
      onCourse={setCourse}
      early={early}
      overview={overview}
      params={params}
      summary={<Summary data={data} isLoading={overview.isLoading} beforeMatching={beforeMatching} filtered={withoutAdvisor || !!search} />}
      loadPanel={<LoadPanel loads={data?.advisorLoads ?? []} isLoading={overview.isLoading} />}
      table={
        <MatchingTable
          overview={overview}
          params={params}
          columns={['Nhóm', 'Giảng viên', 'Lý do ghép', 'Nguyện vọng', 'Quan tâm']}
          advisorNoun="giảng viên"
          emptyDescription="Nhóm lập trong đợt lập nhóm sẽ hiện ở đây."
          renderRow={(row) => <Row key={row.team.id} row={row} beforeMatching={beforeMatching} onEdit={() => setEditing(row)} />}
        />
      }
      dialogs={
        editing && (
          <ChangeLecturerDialog
            key={editing.team.id}
            row={editing}
            loads={data?.advisorLoads ?? []}
            onClose={() => setEditing(null)}
            onDone={(message) => {
              setEditing(null)
              early.snackbar.show(message)
            }}
          />
        )
      }
    />
  )
}

/** Trước hạn: hạn tự ghép và 3 thẻ tiến độ. */
function Summary({
  data,
  isLoading,
  beforeMatching,
  filtered,
}: {
  data?: AdvisorMatchingOverview
  isLoading: boolean
  beforeMatching: boolean
  filtered: boolean
}) {
  if (isLoading || !data) {
    return <SummarySkeleton />
  }

  if (beforeMatching && data.matchingAt) {
    const left = timeLeft(data.matchingAt)
    const total = !filtered ? data.totalCount : undefined
    const interested = countInterestedAdvisors(data.items)
    const partial = data.totalPages != null ? data.totalPages > 1 : data.items.length >= PAGE_SIZE
    return (
      <div className="flex flex-col gap-3">
        <Banner tone={left.tone} title={`Hệ thống tự ghép lúc ${formatDateTime(data.matchingAt)}`}>
          {left.text}
        </Banner>
        <div className="grid gap-3 sm:grid-cols-3">
          <StatCard
            label="Nhóm đã có nguyện vọng"
            value={total != null ? `${data.teamsWithPreferences}/${total}` : String(data.teamsWithPreferences)}
            progress={total ? data.teamsWithPreferences / total : undefined}
          />
          <StatCard label="Giảng viên đang quan tâm" value={String(interested)} hint={partial ? 'Tính trên các nhóm đang hiện' : undefined} />
          <StatCard label="Nhóm chưa có giảng viên" value={String(data.teamsWithoutAdvisor)} />
        </div>
      </div>
    )
  }

  return <MatchedSummary data={data} advisorNoun="giảng viên" />
}

function Row({ row, beforeMatching, onEdit }: { row: AdvisorMatchingRow; beforeMatching: boolean; onEdit: () => void }) {
  const interests = row.interests.filter((i) => i.status === 'OPEN' || i.advisorId === row.advisorId)
  const reason = lecturerReasonLabel(row.reason, row.preferenceRank)
  return (
    <tr className="border-b border-border align-top last:border-b-0">
      <td className="min-w-[160px] px-4 py-3">
        <p className="font-medium">{row.team.name}</p>
        {row.team.projectName && <p className="text-caption text-text-secondary">{row.team.projectName}</p>}
      </td>
      <AdvisorCell name={row.advisorName} noun="giảng viên" beforeMatching={beforeMatching} className="min-w-[180px] px-4 py-3" />
      <ReasonCell reason={reason} />
      <td className="min-w-[180px] px-4 py-3">
        {row.preferences.length === 0 ? (
          <span className="text-text-secondary">Chưa chọn</span>
        ) : (
          <ol className="flex flex-col gap-1">
            {row.preferences.map((p, i) => (
              <li key={p.lecturerId} className={clsx(p.lecturerId === row.advisorId && 'font-semibold')}>
                <span className="tabular-nums text-text-secondary">{i + 1}.</span> {p.fullName}
                {p.lecturerId === row.advisorId && <span className="sr-only"> (đã ghép)</span>}
              </li>
            ))}
          </ol>
        )}
      </td>
      <InterestsCell empty={interests.length === 0}>
        {interests.map((i) => (
          <li key={i.id}>
            {i.advisorName}
            {i.wasPreviousInstructor && <span className="text-caption text-text-secondary"> · đã hướng dẫn EXE101</span>}
          </li>
        ))}
      </InterestsCell>
      <EditCell assigned={!!row.advisorId} noun="giảng viên" teamName={row.team.name} onEdit={onEdit} />
    </tr>
  )
}
