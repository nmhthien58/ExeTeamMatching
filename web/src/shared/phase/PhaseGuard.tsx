import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { Button, Dialog } from '@/shared/components/ui'
import { useIsTeamFormation, useMyCourse } from './usePhase'

/**
 * Rule 18: sau khi đợt lập nhóm đóng, đường dẫn cũ tới Khám phá (S05, S08, S10) mở hộp nhắc thay vì màn hình
 * (user-flow.md mục 0). Bọc các route Khám phá bằng component này.
 */
export function TeamFormationOnly({ children }: { children: ReactNode }) {
  const course = useMyCourse()
  const { isTeamFormation, phase, isLoading } = useIsTeamFormation(course.data)
  const navigate = useNavigate()

  if (isLoading || isTeamFormation) return <>{children}</>

  const closedAt = phase?.teamFormationDeadlineAt
    ? new Date(phase.teamFormationDeadlineAt).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit' })
    : null

  return (
    <Dialog
      open
      onClose={() => navigate('/sv', { replace: true })}
      title="Đợt lập nhóm đã đóng"
      actions={
        <>
          <Button variant="text" onClick={() => navigate('/sv', { replace: true })}>
            Đóng
          </Button>
          <Button onClick={() => navigate('/sv/du-an', { replace: true })}>Mở dự án</Button>
        </>
      }
    >
      {closedAt ? `Đợt lập nhóm đã đóng lúc ${closedAt}.` : 'Đợt lập nhóm của môn đã đóng.'} Khám phá nhóm không còn mở.
    </Dialog>
  )
}
