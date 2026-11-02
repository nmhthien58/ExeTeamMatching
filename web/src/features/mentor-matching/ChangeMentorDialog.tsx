import { Banner } from '@/shared/components/ui'
import { ChangeAdvisorDialog } from '@/features/lecturer-matching/ChangeAdvisorDialog'
import { mentorMatchingErrorMessage } from '@/features/lecturer-matching/errors'
import { useChangeMentor, wouldExceedPreferred, type AdvisorMatchingRow, type ChangeMentorResult, type MentorLoad } from './api'

/**
 * Đổi / gán mentor tay (W28, rule 20). Chọn mentor sẽ vượt số mong muốn thì cảnh báo trước khi lưu (không chặn).
 * Một mentor mỗi nhóm: đổi mentor thì mentor cũ mất quyền vào dự án ngay – dialog nói rõ điều này và nút chính ghi "Đổi mentor" để xác nhận.
 */
export function ChangeMentorDialog({
  row,
  loads,
  onClose,
  onDone,
}: {
  row: AdvisorMatchingRow | null
  loads: MentorLoad[]
  onClose: () => void
  onDone: (message: string, result: ChangeMentorResult) => void
}) {
  const change = useChangeMentor()
  return (
    <ChangeAdvisorDialog
      row={row}
      loads={loads}
      noun="mentor"
      label="Mentor"
      currentSuffix="đang mentor"
      notePlaceholder="Ví dụ: Nhóm cần mentor có kinh nghiệm Fintech"
      pending={change.isPending}
      error={change.error ? mentorMatchingErrorMessage(change.error) : null}
      notice={<p className="text-text-secondary">Mỗi nhóm có một mentor. {row?.advisorName} sẽ không còn vào được dự án của nhóm; lịch sử trao đổi vẫn giữ lại.</p>}
      optionHint={(l) => (l.preferredTeams ? ` · mong muốn ${l.preferredTeams}` : '')}
      warning={(selected) =>
        wouldExceedPreferred(selected) && (
          <Banner tone="warning" title="Vượt số nhóm mong muốn">
            {selected.fullName} muốn mentor {selected.preferredTeams} nhóm; gán nhóm này sẽ thành {selected.assignedCount + 1}/{selected.maxTeams} nhóm.
          </Banner>
        )
      }
      onClose={onClose}
      onSubmit={(mentorId, note, selectedName, isAssign) => {
        if (!row) return
        change.mutate(
          { teamId: row.team.id, mentorId, note },
          {
            onSuccess: (result) => {
              const name = result.assignment?.mentorName ?? selectedName ?? 'mentor'
              onDone(`Đã ${isAssign ? 'gán' : 'đổi sang'} ${name} cho nhóm ${row.team.name}`, result)
            },
          },
        )
      }}
    />
  )
}
