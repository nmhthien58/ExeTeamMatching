import { Link } from 'react-router-dom'
import { ErrorState } from '@/shared/components/States'
import { Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useIsTeamFormation, useMyCourse } from '@/shared/phase/usePhase'
import { useMyTeam } from '@/features/suggestions/api'
import { formatDateTime } from '@/features/workspace/format'
import { useTeamRules } from '@/shared/lib/teamRules'

const linkClass = 'inline-flex min-h-10 items-center justify-center rounded-md px-4 font-medium'

/**
 * S30 – Đợt đăng ký đã khóa (FE-40, rule 13, 14): không sửa nhóm được nữa. Đã có nhóm → trạng thái chốt và lối vào dự án;
 * chưa có nhóm → hệ thống sẽ ghép tự động.
 */
export function RoundClosedPage() {
  const rules = useTeamRules()
  const course = useMyCourse()
  const { phase } = useIsTeamFormation(course.data)
  const team = useMyTeam()
  const closedAt = phase?.teamFormationDeadlineAt

  return (
    <>
      <PageHeader title="Đợt đã khóa" />
      <div className="mx-auto flex max-w-[560px] flex-col gap-4">
        <div className="flex items-center gap-3 rounded-md bg-neutral-container px-4 py-3">
          <Icon name="lock" size={20} className="text-text-secondary" />
          <div>
            <p className="text-subtitle font-semibold">Đợt đăng ký đã đóng</p>
            {closedAt && <p className="text-body text-text-secondary">{formatDateTime(closedAt)}</p>}
          </div>
        </div>

        {team.error ? (
          <ErrorState error={team.error} onRetry={() => void team.refetch()} />
        ) : team.isLoading ? (
          <Skeleton className="h-40 rounded-lg" />
        ) : team.data ? (
          <section className="flex flex-col items-center gap-3 rounded-lg bg-surface p-6 text-center shadow-elevation-1">
            <Icon name="groups" size={48} className="text-primary" />
            <p className="text-subtitle font-semibold">{team.data.name}</p>
            {team.data.status === 'FINALIZED' ? (
              <StatusChip tone="info">Đã chốt</StatusChip>
            ) : (
              <StatusChip tone="warning" icon="error">Bộ môn đang xem lại</StatusChip>
            )}
            <p className="text-body text-text-secondary">Không thêm, bớt hay đổi thành viên được nữa. Mọi việc tiếp theo nằm trong dự án.</p>
            <Link to="/sv/du-an" className={`${linkClass} bg-primary text-white`}>
              Mở dự án
            </Link>
          </section>
        ) : (
          <section className="flex flex-col items-center gap-3 rounded-lg bg-surface p-6 text-center shadow-elevation-1">
            <Icon name="auto_awesome" size={48} className="text-primary" />
            <p className="text-subtitle font-semibold">Bạn sẽ được ghép nhóm tự động</p>
            <p className="max-w-[420px] text-body text-text-secondary">
              Hệ thống đưa bạn vào nhóm còn thiếu người hoặc lập nhóm mới đủ {rules.minSize}–{rules.maxSize} người, ít nhất {rules.minSpecializations} chuyên ngành. Bạn sẽ nhận thông báo khi có kết quả.
            </p>
            <Link to="/sv/thong-bao" className={`${linkClass} border border-primary text-primary-dark hover:bg-primary-container`}>
              Xem thông báo
            </Link>
          </section>
        )}
      </div>
    </>
  )
}
