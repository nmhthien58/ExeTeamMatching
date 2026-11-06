import type { CSSProperties } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import { api } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { ErrorState } from '@/shared/components/States'
import { SectionTitle, StatCard } from '@/shared/components/StatCard'
import { Icon, Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useMyCourse, usePhases } from '@/shared/phase/usePhase'
import { formatDateTime } from '@/features/workspace/format'
import { useSuggestedTeams, type TeamCard } from '@/features/suggestions/api'
import { TeamRequirementBar, TeamSuggestionCard } from '@/features/suggestions/components'

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

/** Việc nên làm theo nextAction: tiêu đề, mô tả, nút (chỉ trưởng nhóm với việc của nhóm), đường dẫn. */
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
 * S01 – Trang chủ sinh viên (FE-37, cùng spec MB-11): việc nên làm, nhóm của mình, lời mời / yêu cầu đang chờ,
 * nhóm gợi ý khi chưa có nhóm (S07 của Nguyên).
 * FE-51 (05/11/2026): bố cục cho màn hình rộng như dashboard Bộ môn – thẻ hero nền mực (lời chào, hạn lập nhóm,
 * việc nên làm), hàng thẻ số liệu, lưới gợi ý 3 cột.
 */
export function HomePage() {
  const { user } = useAuth()
  const home = useQuery({ queryKey: ['home'], queryFn: async () => (await api.get<HomeData>('/api/teams/me/home')).data })
  const firstName = user?.fullName.split(' ').at(-1)

  return (
    <>
      <PageHeader title="Trang chủ" />
      {home.error ? (
        <ErrorState error={home.error} onRetry={() => void home.refetch()} />
      ) : home.isLoading ? (
        <HomeSkeleton />
      ) : (
        <Home data={home.data!} firstName={firstName} />
      )}
    </>
  )
}

function Home({ data, firstName }: { data: HomeData; firstName?: string }) {
  const team = data.team
  const forming = data.phase === 'TEAM_FORMATION'
  return (
    <div className="flex flex-col gap-6">
      <Hero data={data} firstName={firstName} />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <StatCard
          index={0}
          icon="groups"
          tone={team ? 'success' : 'warning'}
          label="Thành viên trong nhóm"
          value={team?.memberCount ?? 0}
          hint={team ? `${team.name} · tối đa ${team.requirement?.maxSize ?? 6} người` : 'Bạn chưa có nhóm – tạo hoặc xin vào nhóm'}
          to={team ? '/sv/nhom' : '/sv/kham-pha'}
        />
        <StatCard index={1} icon="mail" tone="primary" label="Lời mời vào nhóm" value={data.pendingInvitationCount} hint="Đang chờ bạn trả lời" to="/sv/loi-moi" />
        <StatCard
          index={2}
          icon="mark_email_unread"
          tone="info"
          label="Yêu cầu tham gia nhóm"
          value={data.pendingJoinRequestCount}
          hint={data.isLeader ? 'Đang chờ bạn duyệt' : 'Trưởng nhóm duyệt'}
          to={data.isLeader ? '/sv/nhom/yeu-cau' : undefined}
        />
      </div>
      {data.isAutoAssigned && (
        <Link to="/sv/nhom/ghep-tu-dong" className="card-hover flex items-center gap-3 rounded-lg bg-info-container p-4 font-medium text-info-dark">
          <Icon name="auto_awesome" />
          <span className="flex-1">Bạn được ghép nhóm tự động – xem lý do và thành viên</span>
          <Icon name="chevron_right" />
        </Link>
      )}
      {team && <MyTeam team={team} />}
      {!team && forming && <Suggestions />}
    </div>
  )
}

/** Thẻ hero: lời chào, môn và giai đoạn, việc nên làm kèm nút, hạn lập nhóm đếm ngược. */
function Hero({ data, firstName }: { data: HomeData; firstName?: string }) {
  const myCourse = useMyCourse()
  const phases = usePhases()
  const phase = phases.data?.find((p) => p.courseCode === myCourse.data) ?? phases.data?.[0]
  const action = actions[data.nextAction]
  const deadline = data.phase === 'TEAM_FORMATION' ? phase?.teamFormationDeadlineAt : undefined
  const days = deadline ? Math.max(0, Math.ceil((new Date(deadline).getTime() - Date.now()) / 86_400_000)) : undefined
  const showButton = !!action?.button && (!action.leaderOnly || data.isLeader)
  const canExplore = data.nextAction === 'CREATE_OR_JOIN'
  return (
    <section aria-label="Tổng quan" className="relative overflow-hidden rounded-lg bg-ink p-7 text-white shadow-elevation-2">
      <span aria-hidden className="pointer-events-none absolute -right-24 -top-24 size-72 rounded-full bg-brand-orange/30 blur-3xl" />
      <span aria-hidden className="pointer-events-none absolute -bottom-28 left-1/3 size-64 rounded-full bg-info/25 blur-3xl" />
      <div className="relative flex flex-wrap items-end gap-8">
        <div className="min-w-0 flex-1 basis-[420px]">
          <p className="text-caption font-semibold uppercase tracking-[0.08em] text-ink-muted">
            {myCourse.data ?? 'EXE'} · {data.phase === 'TEAM_FORMATION' ? 'Giai đoạn lập nhóm' : 'Học kỳ đang diễn ra'}
          </p>
          <h2 className="mt-2 text-[30px] font-semibold leading-10 tracking-[-0.015em]">Chào {firstName ?? 'bạn'}</h2>
          {action && (
            <p className="mt-1 max-w-[560px] text-body text-ink-text">
              <span className="font-semibold text-white">{action.title}.</span> {action.text}
            </p>
          )}
          {(showButton || canExplore) && (
            <div className="mt-5 flex flex-wrap gap-3">
              {showButton && (
                <Link
                  to={action.to!}
                  className="inline-flex min-h-11 items-center gap-2 rounded-md bg-primary px-5 font-semibold text-white shadow-sm transition-all duration-200 hover:shadow-glow active:scale-[0.98]"
                >
                  {action.button}
                  <Icon name="arrow_forward" size={20} />
                </Link>
              )}
              {canExplore && (
                <Link
                  to="/sv/kham-pha"
                  className="inline-flex min-h-11 items-center gap-2 rounded-md border border-white/20 bg-white/5 px-5 font-semibold text-white transition-colors hover:bg-white/10"
                >
                  <Icon name="travel_explore" size={20} />
                  Khám phá nhóm
                </Link>
              )}
            </div>
          )}
        </div>
        {days !== undefined && deadline && (
          <div className="rounded-md border border-white/10 bg-white/5 px-6 py-5 text-center">
            <p className="text-caption font-semibold uppercase tracking-[0.08em] text-ink-muted">Hạn lập nhóm</p>
            <p className="mt-1 text-[44px] font-semibold leading-none tabular-nums text-brand-orange">{days}</p>
            <p className="mt-1 text-caption text-ink-text">ngày · {formatDateTime(deadline)}</p>
          </div>
        )}
      </div>
    </section>
  )
}

function MyTeam({ team }: { team: TeamCard }) {
  return (
    <section>
      <SectionTitle icon="groups" title="Nhóm của bạn" />
      <Link to="/sv/nhom" className="card-hover block rounded-lg bg-surface p-6 shadow-elevation-1">
        <span className="block text-title font-semibold">{team.name}</span>
        {team.projectName && <span className="block text-text-secondary">{team.projectName}</span>}
        <span className="mt-3 block">
          <TeamRequirementBar memberCount={team.memberCount} maxSize={team.requirement?.maxSize ?? 6} />
        </span>
      </Link>
    </section>
  )
}

/** "Nhóm phù hợp với bạn" – 3 gợi ý đầu; mức phù hợp chỉ để tham khảo, luôn kèm lý do. */
function Suggestions() {
  const suggestions = useSuggestedTeams()
  const items = suggestions.data?.slice(0, 3) ?? []
  if (items.length === 0) return null
  return (
    <section>
      <div className="flex items-start justify-between gap-4">
        <SectionTitle icon="auto_awesome" title="Nhóm phù hợp với bạn" hint="Gợi ý theo kỹ năng và chuyên ngành nhóm đang thiếu" />
        <Link to="/sv/kham-pha/goi-y" className="inline-flex shrink-0 items-center gap-1 font-medium text-info-dark hover:underline">
          Xem tất cả
          <Icon name="arrow_forward" size={20} />
        </Link>
      </div>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {items.map((s, i) => (
          <Link
            key={s.team.id}
            to={`/sv/kham-pha/nhom/${s.team.id}`}
            className="card-hover stagger-in block h-full rounded-lg [&>article]:h-full"
            style={{ '--i': i + 3 } as CSSProperties}
          >
            <TeamSuggestionCard team={s.team} score={s.score} reasons={s.reasons} />
          </Link>
        ))}
      </div>
    </section>
  )
}

function HomeSkeleton() {
  return (
    <div className="flex flex-col gap-6" aria-busy="true" aria-label="Đang tải">
      <Skeleton className="h-[180px] rounded-lg" />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-[150px] rounded-lg" />
        ))}
      </div>
      <Skeleton className="h-[220px] rounded-lg" />
    </div>
  )
}
