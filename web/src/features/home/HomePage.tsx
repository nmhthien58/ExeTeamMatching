import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { ErrorState } from '@/shared/components/States'
import { Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useSuggestedTeams, type TeamCard } from '@/features/suggestions/api'
import { TeamRequirementBar, TeamSuggestionCard } from '@/features/suggestions/components'
import { linkClass, ListSkeleton } from '@/features/my_team/components'

/** `GET /api/teams/me/home` (team.yaml). */
interface HomeData {
  team?: TeamCard | null
  isLeader: boolean
  pendingInvitationCount: number
  pendingJoinRequestCount: number
  nextAction: string
  phase: 'TEAM_FORMATION' | 'SEMESTER_RUNNING'
  isAutoAssigned: boolean
}

/** Thẻ việc nên làm theo nextAction: tiêu đề, mô tả, nút (chỉ trưởng nhóm với việc của nhóm), đường dẫn. */
const actions: Record<string, { title: string; text: string; button?: string; to?: string; leaderOnly?: boolean }> = {
  CREATE_OR_JOIN: { title: 'Bạn chưa có nhóm', text: 'Tạo nhóm mới hoặc xin vào một nhóm đang tuyển.', button: 'Tạo nhóm', to: '/sv/nhom/tao' },
  INVITE_MEMBERS: { title: 'Nhóm cần thêm thành viên', text: 'Nhóm cần 4–6 người.', button: 'Mời thành viên', to: '/sv/nhom/goi-y', leaderOnly: true },
  ADD_SPECIALIZATION: { title: 'Nhóm cần thêm chuyên ngành', text: 'Nhóm cần ít nhất 2 chuyên ngành.', button: 'Gợi ý thành viên', to: '/sv/nhom/goi-y', leaderOnly: true },
  CHOOSE_LECTURER: { title: 'Chọn giảng viên', text: 'Thêm nguyện vọng giảng viên trước hạn.', button: 'Xem nhóm', to: '/sv/nhom' },
  SUBMIT_CHANGE_REQUEST: { title: 'Gửi yêu cầu đổi nhóm', text: 'Nhóm có thay đổi chưa gửi Bộ môn.', button: 'Xem nhóm', to: '/sv/nhom' },
  WAIT_REVIEW: { title: 'Đang chờ Bộ môn duyệt', text: 'Yêu cầu đổi nhóm đang được xem xét.' },
  OPEN_WORKSPACE: { title: 'Nhóm đã chốt', text: 'Làm việc cùng nhóm trong Dự án.', button: 'Mở dự án', to: '/sv/du-an' },
}

/**
 * S01 – Trang chủ sinh viên (FE-37, cùng spec MB-11): thẻ việc nên làm, thẻ nhóm, lời mời / yêu cầu đang chờ,
 * nhóm gợi ý khi chưa có nhóm (S07 của Nguyên). Hạn chót hiện ở khung trang (RoleShell).
 */
export function HomePage() {
  const { user } = useAuth()
  const home = useQuery({ queryKey: ['home'], queryFn: async () => (await api.get<HomeData>('/api/teams/me/home')).data })
  const firstName = user?.fullName.split(' ').at(-1)

  return (
    <>
      <PageHeader title={firstName ? `Chào ${firstName}` : 'Trang chủ'} />
      {home.error ? (
        <ErrorState error={home.error} onRetry={() => void home.refetch()} />
      ) : home.isLoading ? (
        <ListSkeleton />
      ) : (
        <Home data={home.data!} />
      )}
    </>
  )
}

function Home({ data }: { data: HomeData }) {
  const action = actions[data.nextAction]
  const team = data.team
  return (
    <div className="grid gap-4 lg:grid-cols-[2fr_1fr]">
      <div className="flex flex-col gap-4">
        {data.isAutoAssigned && <Row to="/sv/nhom/ghep-tu-dong" icon="auto_awesome" label="Bạn được ghép nhóm tự động" />}
        {action && (
          <Card>
            <h2 className="text-title font-semibold">{action.title}</h2>
            <p className="text-text-secondary">{action.text}</p>
            {action.button && (!action.leaderOnly || data.isLeader) && (
              <div className="mt-3 flex gap-2">
                <Link to={action.to!} className={`${linkClass} bg-primary text-white`}>
                  {action.button}
                </Link>
                {data.nextAction === 'CREATE_OR_JOIN' && (
                  <Link to="/sv/kham-pha" className={`${linkClass} border border-border-strong text-info-dark`}>
                    Khám phá
                  </Link>
                )}
              </div>
            )}
          </Card>
        )}
        {team && (
          <Card>
            <Link to="/sv/nhom" className="block">
              <span className="block text-title font-semibold">{team.name}</span>
              {team.projectName && <span className="block text-text-secondary">{team.projectName}</span>}
              <span className="mt-2 block">
                <TeamRequirementBar memberCount={team.memberCount} maxSize={team.requirement?.maxSize ?? 6} />
              </span>
            </Link>
          </Card>
        )}
        {!team && data.phase === 'TEAM_FORMATION' && <Suggestions />}
      </div>
      <div className="flex flex-col gap-3">
        {data.pendingInvitationCount > 0 && <Row to="/sv/loi-moi" icon="mail" label="Lời mời vào nhóm" count={data.pendingInvitationCount} />}
        {data.pendingJoinRequestCount > 0 && (
          <Row to="/sv/nhom/yeu-cau" icon="mark_email_unread" label="Yêu cầu tham gia" count={data.pendingJoinRequestCount} />
        )}
      </div>
    </div>
  )
}

function Row({ to, icon, label, count }: { to: string; icon: string; label: string; count?: number }) {
  return (
    <Card className="p-0">
      <Link to={to} className="flex min-h-12 items-center gap-3 px-4 hover:bg-background">
        <Icon name={icon} />
        <span className="flex-1">{label}</span>
        {count !== undefined && (
          <span className="rounded-full bg-primary px-2 text-caption font-semibold text-white" aria-label={`${count} đang chờ`}>
            {count}
          </span>
        )}
        <Icon name="chevron_right" />
      </Link>
    </Card>
  )
}

/** "Nhóm phù hợp với bạn" – 3 gợi ý đầu; mức phù hợp chỉ để tham khảo, luôn kèm lý do. */
function Suggestions() {
  const suggestions = useSuggestedTeams()
  const items = suggestions.data?.slice(0, 3) ?? []
  if (items.length === 0) return null
  return (
    <section>
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-title font-semibold">Nhóm phù hợp với bạn</h2>
        <Link to="/sv/kham-pha/goi-y" className="font-medium text-info-dark">
          Xem tất cả
        </Link>
      </div>
      <div className="grid gap-3 md:grid-cols-2">
        {items.map((s) => (
          <Link key={s.team.id} to={`/sv/kham-pha/nhom/${s.team.id}`}>
            <TeamSuggestionCard team={s.team} score={s.score} reasons={s.reasons} />
          </Link>
        ))}
      </div>
    </section>
  )
}
