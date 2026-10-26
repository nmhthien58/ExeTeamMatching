import { Link } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Icon, StatusChip } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useIsTeamFormation, useMyCourse } from '@/shared/phase/usePhase'
import { Card } from '@/features/workspace/components'
import { useMyTeam } from '@/features/suggestions/api'
import { DomainChips, TeamRequirementBar } from '@/features/suggestions/components'
import { teamStatusLabel, type FullTeam } from './api'
import { linkClass, ListSkeleton } from './components'

/**
 * S15 – Nhóm của tôi (FE-37, cùng spec MB-20): tên + trạng thái, điều kiện nhóm, cố vấn, thay đổi chờ duyệt (EXE201 kế thừa),
 * lối tắt Thành viên / Yêu cầu / Gợi ý / Sửa. Chưa có nhóm → EmptyState (Tạo nhóm, Khám phá chỉ trong đợt lập nhóm).
 */
export function MyTeamPage() {
  const team = useMyTeam()
  const { isTeamFormation } = useIsTeamFormation(useMyCourse().data)
  return (
    <>
      <PageHeader title="Nhóm của tôi" />
      {team.error ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : team.isLoading ? (
        <ListSkeleton />
      ) : !team.data ? (
        <EmptyState
          icon="group_add"
          title="Bạn chưa có nhóm"
          description={isTeamFormation ? 'Tạo nhóm mới hoặc xin vào nhóm ở Khám phá.' : 'Đợt lập nhóm đã đóng; Bộ môn sẽ ghép bạn vào nhóm.'}
          action={
            isTeamFormation && (
              <span className="flex gap-2">
                <Link to="/sv/nhom/tao" className={`${linkClass} bg-primary text-white`}>
                  Tạo nhóm
                </Link>
                <Link to="/sv/kham-pha" className={`${linkClass} border border-border-strong text-info-dark`}>
                  Khám phá
                </Link>
              </span>
            )
          }
        />
      ) : (
        <Overview team={team.data as FullTeam} />
      )}
    </>
  )
}

function Overview({ team }: { team: FullTeam }) {
  const status = teamStatusLabel[team.status]
  const isLeader = team.myRelation === 'LEADER'
  const maxSize = team.requirement?.maxSize ?? 6
  const canEdit = isLeader && (team.status === 'DRAFT' || team.status === 'ACTIVE')
  const shortcuts = [
    { to: '/sv/nhom/thanh-vien', icon: 'groups', label: 'Thành viên', detail: `${team.memberCount}/${maxSize}`, show: true },
    { to: '/sv/nhom/yeu-cau', icon: 'mark_email_unread', label: 'Yêu cầu và lời mời', show: isLeader },
    { to: '/sv/nhom/goi-y', icon: 'person_search', label: 'Gợi ý thành viên', show: canEdit && team.memberCount < maxSize },
    { to: '/sv/nhom/sua', icon: 'edit', label: 'Sửa thông tin', show: canEdit },
  ].filter((s) => s.show)

  return (
    <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
      <div className="flex flex-col gap-4">
        <Card>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h2 className="text-headline font-semibold">{team.name}</h2>
            <StatusChip tone={status.tone} icon={status.icon}>
              {status.text}
            </StatusChip>
          </div>
          {team.projectName && <p className="mt-1 text-title">{team.projectName}</p>}
          {team.ideaDescription && <p className="mt-2 text-text-secondary">{team.ideaDescription}</p>}
          <div className="mt-3">
            <DomainChips domains={team.domains} />
          </div>
        </Card>
        <Card>
          <h3 className="mb-2 font-semibold">Điều kiện nhóm</h3>
          <TeamRequirementBar memberCount={team.memberCount} maxSize={maxSize} />
          <ul className="mt-2 flex flex-col gap-1 text-body">
            <Check ok={team.memberCount >= (team.requirement?.minSize ?? 4)} label={`${team.requirement?.minSize ?? 4}–${maxSize} thành viên`} />
            <Check
              ok={(team.requirement?.specializationCount ?? 0) >= (team.requirement?.minSpecializations ?? 2)}
              label={`Ít nhất ${team.requirement?.minSpecializations ?? 2} chuyên ngành`}
            />
          </ul>
        </Card>
        {team.hasPendingChanges && (
          <Card>
            <Link to="/sv/nhom/thanh-vien" className="flex items-center gap-3">
              <Icon name="hourglass_top" />
              <span className="flex-1">
                <span className="block font-semibold">Thay đổi chờ duyệt</span>
                <span className="text-text-secondary">Có hiệu lực khi Bộ môn duyệt yêu cầu đổi nhóm.</span>
              </span>
              <Icon name="chevron_right" />
            </Link>
          </Card>
        )}
      </div>
      <div className="flex flex-col gap-4">
        <Card>
          <h3 className="mb-2 font-semibold">Cố vấn</h3>
          <p>Giảng viên: {team.lecturerName ?? 'Chưa ghép'}</p>
          <p>Mentor: {team.mentorName ?? 'Ghép từ tuần 6'}</p>
        </Card>
        <Card className="p-0">
          <nav aria-label="Lối tắt nhóm">
            {shortcuts.map((s) => (
              <Link key={s.to} to={s.to} className="flex min-h-12 items-center gap-3 border-b border-border px-4 last:border-b-0 hover:bg-background">
                <Icon name={s.icon} />
                <span className="flex-1">{s.label}</span>
                {s.detail && <span className="text-text-secondary">{s.detail}</span>}
                <Icon name="chevron_right" />
              </Link>
            ))}
          </nav>
        </Card>
      </div>
    </div>
  )
}

function Check({ ok, label }: { ok: boolean; label: string }) {
  return (
    <li className={ok ? 'text-success' : 'text-warning'}>
      <span className="inline-flex items-center gap-2">
        <Icon name={ok ? 'check_circle' : 'error'} size={20} />
        {label}
        <span className="sr-only">{ok ? '– đạt' : '– chưa đạt'}</span>
      </span>
    </li>
  )
}
