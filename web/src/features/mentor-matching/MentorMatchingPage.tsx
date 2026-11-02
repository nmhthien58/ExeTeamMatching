import { useState } from 'react'
import clsx from 'clsx'
import { Banner } from '@/shared/components/ui'
import { timeLeft, useCourseParam } from '@/features/dashboard/adminApi'
import { EarlyMatchingDialog } from '@/features/lecturer-matching/EarlyMatchingDialog'
import { mentorMatchingErrorMessage } from '@/features/lecturer-matching/errors'
import { LoadPanel } from '@/features/lecturer-matching/LoadPanel'
import {
  AdvisorCell,
  EditCell,
  InterestsCell,
  MatchedSummary,
  MatchingLayout,
  MatchingTable,
  ReasonCell,
  StatCard,
  SummarySkeleton,
} from '@/features/lecturer-matching/MatchingLayout'
import { useEarlyMatching, useMatchingParams } from '@/features/lecturer-matching/useMatchingPage'
import { mentorReasonLabel } from '@/features/my_team/advisorsApi'
import { formatDateTime } from '@/features/workspace/format'
import {
  countInterestedMentors,
  isFull,
  PAGE_SIZE,
  useConfirmMentorMatching,
  useDiscardMentorMatching,
  useMentorMatchingOverview,
  usePreviewMentorMatching,
  type AdvisorMatchingRow,
  type MentorMatchingOverview,
} from './api'
import { ChangeMentorDialog } from './ChangeMentorDialog'

/**
 * W28 – Ghép mentor (FE-43, quy tắc 19, 20), dùng chung khung với W19.
 * Trước tuần bắt đầu mentor: lúc hệ thống tự ghép, tiến độ (nhóm chưa có mentor, mentor đang quan tâm, mentor còn chỗ) và
 * "Ghép sớm" → xem trước → chốt. Sau khi ghép: bảng nhóm · giảng viên hướng dẫn · mentor · lý do · quan tâm, lọc "Chưa có mentor",
 * thanh tải từng mentor so với số mong muốn / tối đa, đổi mentor tay (cảnh báo vượt mong muốn, chặn vượt tối đa).
 */
export function MentorMatchingPage() {
  const [course, setCourse] = useCourseParam()
  const params = useMatchingParams('chua-co-mentor')
  const { withoutAdvisor, search, page } = params
  const overview = useMentorMatchingOverview(course, { withoutAdvisor, search, page })
  const data = overview.data
  const early = useEarlyMatching({
    preview: usePreviewMentorMatching(course),
    confirm: useConfirmMentorMatching(),
    discard: useDiscardMentorMatching(),
    errorMessage: mentorMatchingErrorMessage,
    confirmedMessage: (run) => `Đã ghép mentor cho ${run.matchedCount} nhóm`,
  })
  const [editing, setEditing] = useState<AdvisorMatchingRow | null>(null)

  const applied = data?.lastRun?.status === 'APPLIED'
  const beforeMatching = !!data?.matchingAt && new Date(data.matchingAt).getTime() > Date.now() && !applied

  return (
    <MatchingLayout
      title="Ghép mentor"
      advisorNoun="mentor"
      tableLabel="Nhóm và mentor"
      course={course}
      onCourse={setCourse}
      early={early}
      overview={overview}
      params={params}
      summary={<Summary data={data} isLoading={overview.isLoading} beforeMatching={beforeMatching} />}
      loadPanel={
        <LoadPanel
          loads={data?.advisorLoads ?? []}
          isLoading={overview.isLoading}
          title="Số nhóm của mentor"
          subtitle="Tính chung EXE101 và EXE201 trong học kỳ."
          emptyText="Chưa có mentor đang hoạt động. Bộ môn thêm mentor ở mục Mentor."
          showPreferred
        />
      }
      table={
        <MatchingTable
          overview={overview}
          params={params}
          columns={['Nhóm', 'Giảng viên hướng dẫn', 'Mentor', 'Lý do ghép', 'Quan tâm']}
          advisorNoun="mentor"
          emptyDescription="Nhóm của môn trong học kỳ sẽ hiện ở đây."
          renderRow={(row) => <Row key={row.team.id} row={row} beforeMatching={beforeMatching} onEdit={() => setEditing(row)} />}
        />
      }
      dialogs={
        <>
          {early.previewRun && (
            <EarlyMatchingDialog
              run={early.previewRun}
              onClose={early.closePreview}
              onConfirm={early.onConfirm}
              onDiscard={early.onDiscard}
              confirming={early.confirming}
              discarding={early.discarding}
              error={early.dialogError}
              advisorNoun="mentor"
              reasonLabel={mentorReasonLabel}
            />
          )}
          {editing && (
            <ChangeMentorDialog
              key={editing.team.id}
              row={editing}
              loads={data?.advisorLoads ?? []}
              onClose={() => setEditing(null)}
              onDone={(message, result) => {
                setEditing(null)
                const warning = result.warnings?.find((w) => w.code === 'ABOVE_PREFERRED')
                early.snackbar.show(warning ? `${message}. Lưu ý: vượt số nhóm mong muốn của mentor.` : message)
              }}
            />
          )}
        </>
      }
    />
  )
}

/** Trước tuần mentor: lúc tự ghép và 3 thẻ tiến độ. */
function Summary({ data, isLoading, beforeMatching }: { data?: MentorMatchingOverview; isLoading: boolean; beforeMatching: boolean }) {
  if (isLoading || !data) {
    return <SummarySkeleton />
  }

  if (beforeMatching && data.matchingAt) {
    const left = timeLeft(data.matchingAt)
    const interested = countInterestedMentors(data.items)
    const partial = data.totalPages != null ? data.totalPages > 1 : data.items.length >= PAGE_SIZE
    const withRoom = data.advisorLoads.filter((l) => !isFull(l)).length
    return (
      <div className="flex flex-col gap-3">
        <Banner tone={left.tone === 'error' ? 'warning' : 'info'} title={`Hệ thống tự ghép mentor lúc ${formatDateTime(data.matchingAt)}`}>
          Tuần bắt đầu mentor của học kỳ · {left.text}
        </Banner>
        <div className="grid gap-3 sm:grid-cols-3">
          <StatCard label="Nhóm chưa có mentor" value={String(data.teamsWithoutAdvisor)} />
          <StatCard label="Mentor đang quan tâm" value={String(interested)} hint={partial ? 'Tính trên các nhóm đang hiện' : undefined} />
          <StatCard label="Mentor còn nhận nhóm" value={`${withRoom}/${data.advisorLoads.length}`} />
        </div>
      </div>
    )
  }

  return <MatchedSummary data={data} advisorNoun="mentor" />
}

function Row({ row, beforeMatching, onEdit }: { row: AdvisorMatchingRow; beforeMatching: boolean; onEdit: () => void }) {
  const interests = row.interests.filter((i) => i.status === 'OPEN' || i.advisorId === row.advisorId)
  const reason = mentorReasonLabel(row.reason)
  return (
    <tr className="border-b border-border align-top last:border-b-0">
      <td className="min-w-[180px] px-4 py-3">
        <p className="font-medium">{row.team.name}</p>
        {row.team.projectName && <p className="text-caption text-text-secondary">{row.team.projectName}</p>}
        {row.team.domains.length > 0 && <p className="text-caption text-text-secondary">{row.team.domains.map((d) => d.name).join(' · ')}</p>}
      </td>
      <td className="min-w-[160px] px-4 py-3">{row.team.lecturerName ?? <span className="text-text-secondary">Chưa có</span>}</td>
      <AdvisorCell name={row.advisorName} noun="mentor" beforeMatching={beforeMatching} className="min-w-[160px] px-4 py-3" />
      <ReasonCell reason={reason} />
      <InterestsCell empty={interests.length === 0}>
        {interests.map((i) => (
          <li key={i.id} className={clsx(i.advisorId === row.advisorId && 'font-semibold')}>
            {i.advisorName}
            {i.advisorId === row.advisorId && <span className="sr-only"> (đã ghép)</span>}
          </li>
        ))}
      </InterestsCell>
      <EditCell assigned={!!row.advisorId} noun="mentor" teamName={row.team.name} onEdit={onEdit} />
    </tr>
  )
}
