import { Link } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Icon, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useMyTeam } from '@/features/suggestions/api'
import { DomainChips, TeamRequirementBar } from '@/features/suggestions/components'
import { Avatar, RoleChip } from '@/features/workspace/components'

/**
 * S31 – Bạn đã được ghép nhóm tự động (FE-40, rule 14): nhóm do hệ thống lập, thành viên kèm liên hệ (chỉ đồng đội thấy – rule 11),
 * các bước tiếp theo.
 */
export function AutoAssignedPage() {
  const team = useMyTeam()

  return (
    <>
      <PageHeader title="Nhóm của bạn" />
      {team.error ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : team.isLoading ? (
        <Skeleton className="h-64 rounded-lg" />
      ) : !team.data ? (
        <EmptyState icon="hourglass_top" title="Chưa có kết quả ghép" description="Hệ thống đang ghép nhóm. Bạn sẽ nhận thông báo khi xong." />
      ) : (
        <div className="mx-auto flex max-w-[640px] flex-col gap-4">
          <section className="flex flex-col gap-2 rounded-lg bg-primary-container p-4">
            <p className="flex items-center gap-2 text-subtitle font-semibold text-primary-dark">
              <Icon name="auto_awesome" size={20} />
              Bạn đã được ghép vào {team.data.name}
            </p>
            <DomainChips domains={team.data.domains} />
            <TeamRequirementBar memberCount={team.data.memberCount} maxSize={team.data.requirement?.maxSize} />
          </section>

          <section className="rounded-lg bg-surface p-4 shadow-elevation-1">
            <h2 className="mb-2 text-subtitle font-semibold">Thành viên</h2>
            <ul>
              {team.data.members.map((m) => (
                <li key={m.id} className="flex items-start gap-3 border-b border-border py-3 last:border-none">
                  <Avatar name={m.fullName} size={40} />
                  <div className="min-w-0 flex-1">
                    <p className="flex flex-wrap items-center gap-2 font-medium">
                      {m.fullName}
                      <RoleChip role={m.role === 'LEADER' ? 'LEADER' : undefined} />
                    </p>
                    <p className="line-clamp-2 text-body text-text-secondary">
                      {m.studentCode}
                      {m.specialization && ` · ${m.specialization.name}`}
                    </p>
                    <p className="flex flex-wrap gap-x-4 text-body">
                      {m.email && (
                        <a href={`mailto:${m.email}`} className="text-primary-dark hover:underline">
                          {m.email}
                        </a>
                      )}
                      {m.phone && (
                        <a href={`tel:${m.phone}`} className="text-primary-dark hover:underline">
                          {m.phone}
                        </a>
                      )}
                    </p>
                  </div>
                </li>
              ))}
            </ul>
          </section>

          <section className="rounded-lg bg-surface p-4 shadow-elevation-1">
            <h2 className="mb-2 text-subtitle font-semibold">Bước tiếp theo</h2>
            <NextStep to="/sv/du-an/tin-nhan" icon="forum" title="Chào nhóm" description="Nhắn kênh Nhóm để làm quen và hẹn buổi đầu." />
            <NextStep to="/sv/du-an" icon="rocket_launch" title="Mở dự án" description="Việc, file và lịch họp của nhóm." />
            <NextStep to="/sv/nhom" icon="groups" title="Xem nhóm" description="Đề tài, giảng viên hướng dẫn và mentor." />
          </section>
        </div>
      )}
    </>
  )
}

/** Dòng điều hướng (DESIGN.md 6.1): icon, tiêu đề, dòng phụ, mũi tên. */
function NextStep({ to, icon, title, description }: { to: string; icon: string; title: string; description: string }) {
  return (
    <Link to={to} className="flex min-h-14 items-center gap-3 rounded-md px-2 hover:bg-background">
      <Icon name={icon} className="text-primary" />
      <span className="min-w-0 flex-1">
        <span className="block text-body-lg">{title}</span>
        <span className="block text-body text-text-secondary">{description}</span>
      </span>
      <Icon name="chevron_right" className="text-text-secondary" />
    </Link>
  )
}
