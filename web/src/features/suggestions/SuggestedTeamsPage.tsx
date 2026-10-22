import { Link } from 'react-router-dom'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Skeleton } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { useSuggestedTeams } from './api'
import { TeamSuggestionCard } from './components'

const linkClass = 'inline-flex min-h-10 items-center rounded-md px-4 font-medium'

/**
 * S07 – Nhóm gợi ý cho bạn (FE-40): danh sách từ Matching.SuggestTeams kèm mức phù hợp và lý do.
 * Trạng thái: có gợi ý; hồ sơ chưa đủ dữ liệu (chưa có gợi ý → hoàn thiện kỹ năng); đang tải; lỗi.
 */
export function SuggestedTeamsPage() {
  const suggestions = useSuggestedTeams()

  return (
    <>
      <PageHeader title="Gợi ý cho bạn" />
      {suggestions.error ? (
        <ErrorState error={suggestions.error} onRetry={() => void suggestions.refetch()} />
      ) : suggestions.isLoading ? (
        <div role="status" aria-label="Đang tải gợi ý" className="grid gap-3 md:grid-cols-2">
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-40 rounded-lg" />
          ))}
        </div>
      ) : suggestions.data!.length === 0 ? (
        <EmptyState
          icon="person_search"
          title="Chưa đủ dữ liệu để gợi ý"
          description="Đánh giá đủ 5 kỹ năng trong hồ sơ để hệ thống tìm nhóm cần đúng thế mạnh của bạn."
          action={
            <Link to="/sv/ho-so" className={`${linkClass} bg-primary text-white`}>
              Hoàn thiện hồ sơ
            </Link>
          }
        />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {suggestions.data!.map((s) => (
            <TeamSuggestionCard
              key={s.team.id}
              team={s.team}
              score={s.score}
              reasons={s.reasons}
              action={
                <Link to={`/sv/kham-pha/nhom/${s.team.id}`} className={`${linkClass} border border-secondary text-secondary hover:bg-secondary-container`}>
                  Xem nhóm
                </Link>
              }
            />
          ))}
        </div>
      )}
    </>
  )
}
