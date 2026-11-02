import { useChangeInstructor, type AdvisorLoad, type AdvisorMatchingRow } from './api'
import { ChangeAdvisorDialog } from './ChangeAdvisorDialog'
import { matchingErrorMessage } from './errors'

/** Đổi / gán giảng viên hướng dẫn tay (W19). */
export function ChangeLecturerDialog({
  row,
  loads,
  onClose,
  onDone,
}: {
  row: AdvisorMatchingRow | null
  loads: AdvisorLoad[]
  onClose: () => void
  onDone: (message: string) => void
}) {
  const change = useChangeInstructor()
  return (
    <ChangeAdvisorDialog
      row={row}
      loads={loads}
      noun="giảng viên"
      label="Giảng viên"
      currentSuffix="đang hướng dẫn"
      notePlaceholder="Ví dụ: Cân bằng số nhóm giữa các giảng viên"
      pending={change.isPending}
      error={change.error ? matchingErrorMessage(change.error) : null}
      onClose={onClose}
      onSubmit={(lecturerId, note, name, isAssign) => {
        if (!row) return
        change.mutate(
          { teamId: row.team.id, lecturerId, note },
          { onSuccess: () => onDone(`Đã ${isAssign ? 'gán' : 'đổi sang'} ${name ?? 'giảng viên'} cho nhóm ${row.team.name}`) },
        )
      }}
    />
  )
}
