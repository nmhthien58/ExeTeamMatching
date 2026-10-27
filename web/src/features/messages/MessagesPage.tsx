import { useSearchParams } from 'react-router-dom'
import clsx from 'clsx'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { Avatar, Card, RoleChip } from '@/features/workspace/components'
import { formatRelative } from '@/features/workspace/format'
import { isStudentRole, useMyWorkspaces, type PersonRef } from '@/features/workspace/api'
import { useWorkspaceContext } from '@/features/workspace/context'
import { ListSkeleton } from '@/features/my_team/components'
import { channelLabel, useChannels, useChannelsOf, useOpenQuestions, type Channel } from './api'
import { ChannelView } from './ChannelView'

interface Row {
  channel: Channel
  title: string
  needsReply?: boolean
}

/**
 * C02 + C03 của sinh viên trong dự án (FE-38): hai kênh Nhóm và Cố vấn bên trái, kênh đang chọn ở giữa (`?kenh=`),
 * thành viên nhóm ở cột phải khi ≥ 1280 px. Bộ môn xem được nhưng không gửi (quy tắc 21).
 */
export function WorkspaceMessagesPage() {
  const { workspace } = useWorkspaceContext()
  const channels = useChannels(workspace.id)
  const rows = (channels.data ?? []).map((c) => ({ channel: c, title: channelLabel(c.kind) }))
  return (
    <>
      <PageHeader title="Tin nhắn" />
      {channels.error ? (
        <ErrorState error={channels.error} onRetry={() => void channels.refetch()} />
      ) : channels.isLoading ? (
        <ListSkeleton count={2} />
      ) : (
        <Conversations rows={rows} readOnly={workspace.myRole === 'ADMIN'} isStudent={isStudentRole(workspace.myRole)} people={workspace.people} />
      )}
    </>
  )
}

/**
 * C02 + C03 của giảng viên / mentor (FE-38): một dòng cho kênh Cố vấn của mỗi nhóm, sắp theo tin mới nhất,
 * lọc "Tất cả / Cần trả lời" (`?loc=can-tra-loi`). Kênh Nhóm không hiện với cố vấn (quy tắc 22).
 */
export function AdvisorMessagesPage() {
  const [params, setParams] = useSearchParams()
  const needsReplyOnly = params.get('loc') === 'can-tra-loi'
  const workspaces = useMyWorkspaces()
  const channelQueries = useChannelsOf((workspaces.data ?? []).map((w) => w.id))
  const questions = useOpenQuestions(true)
  const waiting = new Set((questions.data ?? []).map((q) => q.message.channelId))
  const teamName = new Map((workspaces.data ?? []).map((w) => [w.id, w.teamName]))
  const rows: Row[] = channelQueries
    .flatMap((q) => q.data ?? [])
    .map((c) => ({ channel: c, title: `${teamName.get(c.workspaceId) ?? 'Nhóm'} · Cố vấn`, needsReply: waiting.has(c.id) }))
    .filter((r) => !needsReplyOnly || r.needsReply)
    .sort((a, b) => (b.channel.lastMessage?.createdAt ?? '').localeCompare(a.channel.lastMessage?.createdAt ?? ''))
  const error = workspaces.error ?? channelQueries.find((q) => q.error)?.error
  const loading = workspaces.isLoading || channelQueries.some((q) => q.isLoading)
  const setFilter = (value?: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set('loc', value)
    else next.delete('loc')
    next.delete('kenh')
    setParams(next, { replace: true })
  }

  return (
    <>
      <PageHeader title="Tin nhắn" />
      <div role="radiogroup" aria-label="Lọc hội thoại" className="mb-4 inline-flex rounded-full border border-border-strong p-1">
        {[
          { value: undefined, label: 'Tất cả' },
          { value: 'can-tra-loi', label: `Cần trả lời${questions.data?.length ? ` (${questions.data.length})` : ''}` },
        ].map((o) => {
          const selected = (o.value === 'can-tra-loi') === needsReplyOnly
          return (
            <button
              key={o.label}
              type="button"
              role="radio"
              aria-checked={selected}
              onClick={() => setFilter(o.value)}
              className={clsx('min-h-10 rounded-full px-4 font-medium', selected ? 'bg-primary text-white' : 'text-text-primary')}
            >
              {o.label}
            </button>
          )
        })}
      </div>
      {error ? (
        <ErrorState error={error} onRetry={() => void workspaces.refetch()} />
      ) : loading ? (
        <ListSkeleton />
      ) : (
        <Conversations rows={rows} readOnly={false} isStudent={false} emptyText={needsReplyOnly ? 'Không còn câu hỏi chờ trả lời.' : 'Bạn chưa được ghép với nhóm nào.'} />
      )}
    </>
  )
}

function Conversations({ rows, readOnly, isStudent, people, emptyText = 'Chưa có kênh nào.' }: {
  rows: Row[]
  readOnly: boolean
  isStudent: boolean
  people?: PersonRef[]
  emptyText?: string
}) {
  const [params, setParams] = useSearchParams()
  const selected = rows.find((r) => r.channel.id === params.get('kenh')) ?? rows[0]
  if (rows.length === 0) return <EmptyState icon="forum" title={emptyText} />
  return (
    <div className="grid gap-4 lg:grid-cols-[360px_1fr] xl:grid-cols-[360px_1fr_320px]">
      <nav aria-label="Hội thoại" className="flex flex-col gap-1 self-start rounded-lg bg-surface p-2 shadow-elevation-1">
        {rows.map((r) => (
          <button
            key={r.channel.id}
            type="button"
            aria-current={r === selected ? 'true' : undefined}
            onClick={() => {
              const next = new URLSearchParams(params)
              next.set('kenh', r.channel.id)
              setParams(next, { replace: true })
            }}
            className={clsx('flex min-h-14 items-center gap-3 rounded-md px-3 py-2 text-left', r === selected ? 'bg-primary-container' : 'hover:bg-background')}
          >
            <Icon name={r.channel.kind === 'TEAM' ? 'groups' : 'support_agent'} />
            <span className="min-w-0 flex-1">
              <span className="flex items-center gap-2">
                <span className="truncate font-semibold">{r.title}</span>
                {r.needsReply && <span className="whitespace-nowrap rounded-full bg-warning-container px-2 text-caption font-semibold text-warning">Cần trả lời</span>}
              </span>
              <span className="block truncate text-caption text-text-secondary">
                {r.channel.lastMessage
                  ? `${r.channel.lastMessage.author.displayName}: ${r.channel.lastMessage.isDeleted ? 'Tin nhắn đã bị xóa' : r.channel.lastMessage.body}`
                  : 'Chưa có tin nhắn'}
              </span>
            </span>
            <span className="flex flex-col items-end gap-1 text-caption text-text-secondary">
              {r.channel.lastMessage && formatRelative(r.channel.lastMessage.createdAt)}
              {r.channel.unreadCount > 0 && (
                <span className="grid min-w-6 place-items-center rounded-full bg-primary px-1.5 font-semibold text-white" aria-label={`${r.channel.unreadCount} tin chưa đọc`}>
                  {r.channel.unreadCount}
                </span>
              )}
            </span>
          </button>
        ))}
      </nav>
      {selected && <ChannelView key={selected.channel.id} channel={selected.channel} title={selected.title} readOnly={readOnly} isStudent={isStudent} />}
      {people && people.length > 0 && (
        <Card className="hidden self-start xl:block">
          <h3 className="mb-2 font-semibold">Thành viên</h3>
          <ul className="flex flex-col gap-2">
            {people.map((p) => (
              <li key={p.personId} className="flex items-center gap-2">
                <Avatar name={p.displayName} size={24} isMentor={p.role === 'MENTOR'} />
                <span className="flex-1 truncate">{p.displayName}</span>
                <RoleChip role={p.role} />
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  )
}
