import { useState } from 'react'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Card } from '@/features/workspace/components'
import { useMyTeam } from '@/features/suggestions/api'
import { useTeamAuditLogs } from '@/features/audit-log/api'
import { formatDateTime } from '@/features/workspace/format'
import { ListSkeleton } from './components'

const icons: Record<string, string> = {
  MEMBER_JOINED: 'person_add',
  MEMBER_ADD_PENDING: 'person_add',
  MEMBER_LEFT: 'person_remove',
  MEMBER_REMOVED: 'person_remove',
  MEMBER_REMOVE_PENDING: 'person_remove',
  LEADER_CHANGED: 'star',
  INVITATION_SENT: 'mail',
  INVITATION_CANCELLED: 'mail',
  JOIN_REQUEST_SUBMITTED: 'how_to_reg',
  JOIN_REQUEST_CANCELLED: 'how_to_reg',
  RECRUITMENT_POST_OPENED: 'campaign',
  RECRUITMENT_POST_UPDATED: 'campaign',
  RECRUITMENT_POST_CLOSED: 'campaign',
}

/** S22 – Lịch sử hoạt động nhóm (FE-37, cùng spec MB-26): ai làm gì, lúc nào; mới nhất trước, phân trang 20. */
export function TeamHistoryPage() {
  const team = useMyTeam()
  const [page, setPage] = useState(1)
  const logs = useTeamAuditLogs(team.data?.id, page)
  return (
    <>
      <PageHeader title="Lịch sử hoạt động" />
      {team.error ? (
        <ErrorState error={team.error} onRetry={() => void team.refetch()} />
      ) : team.isLoading ? (
        <ListSkeleton />
      ) : !team.data ? (
        <EmptyState icon="group_off" title="Bạn chưa có nhóm" />
      ) : logs.error ? (
        <ErrorState error={logs.error} onRetry={() => void logs.refetch()} />
      ) : logs.isLoading ? (
        <ListSkeleton />
      ) : logs.data!.items.length === 0 ? (
        <EmptyState icon="history" title="Chưa có hoạt động" description="Các thay đổi của nhóm sẽ hiện ở đây." />
      ) : (
        <Card className="max-w-3xl p-0">
          <ol aria-label="Lịch sử hoạt động">
            {logs.data!.items.map((log) => (
              <li key={log.id} className="flex items-start gap-3 border-b border-border px-4 py-3 last:border-b-0">
                <Icon name={icons[log.action] ?? 'edit_note'} className="mt-0.5 text-text-secondary" />
                <span className="flex-1">
                  <span className="block">{log.summary}</span>
                  <span className="text-caption text-text-secondary">
                    {log.actorName} · {formatDateTime(log.occurredAt)}
                    {log.reason && ` · ${log.reason}`}
                  </span>
                </span>
              </li>
            ))}
          </ol>
          {logs.data!.totalPages > 1 && (
            <nav aria-label="Phân trang" className="flex items-center justify-center gap-3 border-t border-border p-2">
              <Button variant="text" disabled={page <= 1} onClick={() => setPage(page - 1)}>
                Mới hơn
              </Button>
              <span>
                Trang {page}/{logs.data!.totalPages}
              </span>
              <Button variant="text" disabled={page >= logs.data!.totalPages} onClick={() => setPage(page + 1)}>
                Cũ hơn
              </Button>
            </nav>
          )}
        </Card>
      )}
    </>
  )
}
