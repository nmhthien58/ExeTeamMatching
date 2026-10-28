import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { toApiError } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { Avatar, Drawer, RoleChip } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import { RealtimeStatusBar } from '@/features/realtime/RealtimeStatusBar'
import { useHubEvent, useHubGroup } from '@/features/realtime/useRealtime'
import type { PersonRef } from '@/features/workspace/api'
import {
  applyMessage,
  channelLabel,
  updateRoot,
  useHistory,
  useMarkRead,
  useSendMessage,
  useThread,
  type Channel,
  type Message,
  type QuestionStatus,
  type SendInput,
} from './api'

const MAX_LENGTH = 2000

interface Pending extends SendInput {
  failed?: string
}

/**
 * C03 – Kênh tin nhắn (FE-38): tin theo ngày (mới nhất ở dưới), bong bóng, luồng trả lời trong drawer, câu hỏi cho cố vấn
 * (Chờ / Đã trả lời), gửi lạc quan kèm "Gửi lại", Enter gửi / Shift + Enter xuống dòng, đánh dấu đã đọc, cập nhật realtime.
 */
// shortcut: chưa có đính kèm file (API chưa nhận), @nhắc tên, sửa / xóa tin trên giao diện; bổ sung khi cần.
export function ChannelView({ channel, title, readOnly, isStudent }: { channel: Channel; title: string; readOnly: boolean; isStudent: boolean }) {
  const queryClient = useQueryClient()
  const history = useHistory(channel.id)
  const markRead = useMarkRead(channel.id, channel.workspaceId)
  const [thread, setThread] = useState<string | null>(null)
  const isMine = useIsMine()
  const listRef = useRef<HTMLOListElement>(null)

  useHubGroup('Channel', channel.id)
  useHubEvent<Message>('messageCreated', (m) => m.channelId === channel.id && applyMessage(queryClient, m))
  useHubEvent<Message>('messageUpdated', (m) => m.channelId === channel.id && applyMessage(queryClient, m))
  useHubEvent<{ channelId: string; messageId: string }>('messageDeleted', (e) =>
    e.channelId === channel.id && updateRoot(queryClient, channel.id, e.messageId, (m) => ({ ...m, isDeleted: true, body: '' })),
  )
  useHubEvent<{ channelId: string; messageId: string; status: QuestionStatus }>('questionStatusChanged', (e) =>
    e.channelId === channel.id && updateRoot(queryClient, channel.id, e.messageId, (m) => ({ ...m, questionStatus: e.status })),
  )

  const messages = (history.data?.pages ?? []).flatMap((p) => p.items).reverse()
  const newest = messages.at(-1)
  const lastMarked = useRef<string>()
  useEffect(() => {
    if (readOnly || !newest || newest.id === lastMarked.current) return
    lastMarked.current = newest.id
    markRead.mutate(newest.id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [newest?.id, readOnly])
  useEffect(() => {
    const list = listRef.current
    if (list) list.scrollTop = list.scrollHeight
  }, [newest?.id])

  return (
    <section aria-label={`Kênh ${title}`} className="flex min-h-[480px] flex-1 flex-col overflow-hidden rounded-lg bg-surface shadow-elevation-1">
      <header className="flex items-center gap-2 border-b border-border px-4 py-3">
        <Icon name={channel.kind === 'TEAM' ? 'groups' : 'support_agent'} />
        <h2 className="flex-1 text-subtitle font-semibold">{title}</h2>
      </header>
      <RealtimeStatusBar />
      {history.error ? (
        <div className="p-4">
          <ErrorState error={history.error} onRetry={() => void history.refetch()} />
        </div>
      ) : history.isLoading ? (
        <div role="status" aria-label="Đang tải tin nhắn" className="flex flex-col gap-3 p-4">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className={clsx('h-12 w-2/3 rounded-lg', i % 2 && 'self-end')} />
          ))}
        </div>
      ) : (
        <ol ref={listRef} aria-label="Tin nhắn" className="flex flex-1 flex-col gap-2 overflow-y-auto p-4">
          {history.hasNextPage && (
            <li className="self-center">
              <Button variant="text" loading={history.isFetchingNextPage} onClick={() => void history.fetchNextPage()}>
                Tải tin cũ hơn
              </Button>
            </li>
          )}
          {messages.length === 0 && (
            <li>
              <EmptyState icon="forum" title="Chưa có tin nhắn." description={`Bắt đầu trò chuyện ở kênh ${channelLabel(channel.kind)}.`} />
            </li>
          )}
          {messages.map((m, i) => (
            <MessageItem key={m.id} message={m} mine={isMine(m.author)} dayChanged={!sameDay(messages[i - 1]?.createdAt, m.createdAt)} onThread={() => setThread(m.id)} />
          ))}
        </ol>
      )}
      {readOnly ? (
        <p className="border-t border-border px-4 py-3 text-text-secondary">Bộ môn chỉ xem kênh này.</p>
      ) : (
        <Composer channel={channel} allowQuestion={channel.kind === 'ADVISORS' && isStudent} />
      )}
      <ThreadDrawer messageId={thread} channel={channel} readOnly={readOnly} onClose={() => setThread(null)} />
    </section>
  )
}

function useIsMine() {
  const { user } = useAuth()
  const ids = [user?.studentId, user?.lecturerId, user?.mentorId].filter(Boolean)
  return (author: PersonRef) => ids.includes(author.personId)
}

const sameDay = (a: string | undefined, b: string) => !!a && new Date(a).toDateString() === new Date(b).toDateString()

function MessageItem({ message: m, mine, dayChanged, onThread }: { message: Message; mine: boolean; dayChanged: boolean; onThread?: () => void }) {
  return (
    <>
      {dayChanged && (
        <li className="my-2 self-center rounded-full bg-background px-3 text-caption text-text-secondary">{new Date(m.createdAt).toLocaleDateString('vi-VN')}</li>
      )}
      <li className={clsx('group flex max-w-[80%] gap-2', mine ? 'flex-row-reverse self-end' : 'self-start')}>
        {!mine && <Avatar name={m.author.displayName} isMentor={m.author.role === 'MENTOR'} />}
        <div className="flex flex-col gap-1">
          {!mine && (
            <span className="flex items-center gap-2 text-caption font-semibold">
              {m.author.displayName} <RoleChip role={m.author.role} />
            </span>
          )}
          <div className={clsx('rounded-lg px-3 py-2', mine ? 'rounded-tr-sm border border-border bg-surface' : 'rounded-tl-sm bg-info-container')}>
            {m.isDeleted ? <em className="text-text-secondary">Tin nhắn đã bị xóa</em> : <p className="whitespace-pre-wrap break-words">{m.body}</p>}
            <span className="mt-1 block text-caption text-text-secondary">
              {formatDateTime(m.createdAt)}
              {m.editedAt && ' · đã sửa'}
            </span>
          </div>
          <span className="flex flex-wrap items-center gap-2">
            {m.isAdvisorQuestion && (
              <StatusChip tone={m.questionStatus === 'ANSWERED' ? 'success' : 'warning'}>
                {m.questionStatus === 'ANSWERED' ? 'Đã trả lời' : 'Chờ trả lời'}
              </StatusChip>
            )}
            {onThread && !m.isDeleted && (
              <Button variant="text" icon="reply" onClick={onThread} className="min-h-8 px-2">
                {m.replyCount > 0 ? `${m.replyCount} trả lời` : 'Trả lời'}
              </Button>
            )}
          </span>
        </div>
      </li>
    </>
  )
}

/** Khung soạn tin dính đáy; tin gửi lỗi giữ lại với "Gửi lại" (cùng clientMessageId nên không bị trùng). */
function Composer({ channel, parentMessageId, allowQuestion = false }: { channel: Channel; parentMessageId?: string; allowQuestion?: boolean }) {
  const send = useSendMessage(channel.id)
  const [body, setBody] = useState('')
  const [asQuestion, setAsQuestion] = useState(false)
  const [pending, setPending] = useState<Pending[]>([])

  const submit = (input: SendInput) => {
    setPending((list) => [...list.filter((p) => p.clientMessageId !== input.clientMessageId), input])
    send.mutate(input, {
      onSuccess: () => setPending((list) => list.filter((p) => p.clientMessageId !== input.clientMessageId)),
      onError: (error) =>
        setPending((list) => list.map((p) => (p.clientMessageId === input.clientMessageId ? { ...p, failed: toApiError(error).detail ?? 'Gửi lỗi' } : p))),
    })
  }
  const sendDraft = () => {
    const text = body.trim()
    if (!text || text.length > MAX_LENGTH) return
    submit({ clientMessageId: crypto.randomUUID(), body: text, parentMessageId, isAdvisorQuestion: asQuestion || undefined })
    setBody('')
    setAsQuestion(false)
  }

  return (
    <div className="border-t border-border p-3">
      {pending.map((p) => (
        <p key={p.clientMessageId} className="mb-2 flex items-center justify-end gap-2 text-caption">
          <span className="max-w-[60%] truncate rounded-lg border border-border px-3 py-1">{p.body}</span>
          {p.failed ? (
            <>
              <span role="alert" className="text-error">
                Gửi lỗi
              </span>
              <Button
                variant="text"
                icon="refresh"
                className="min-h-8 px-2"
                onClick={() => submit({ clientMessageId: p.clientMessageId, body: p.body, parentMessageId: p.parentMessageId, isAdvisorQuestion: p.isAdvisorQuestion })}
              >
                Gửi lại
              </Button>
            </>
          ) : (
            <span className="text-text-secondary">Đang gửi…</span>
          )}
        </p>
      ))}
      <form
        className="flex items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault()
          sendDraft()
        }}
      >
        <div className="flex flex-1 flex-col gap-1">
          <textarea
            aria-label="Tin nhắn"
            rows={2}
            value={body}
            maxLength={MAX_LENGTH}
            placeholder={parentMessageId ? 'Trả lời trong luồng' : 'Nhập tin nhắn (Enter để gửi, Shift + Enter xuống dòng)'}
            onChange={(e) => setBody(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
                e.preventDefault()
                sendDraft()
              }
            }}
            className="resize-none rounded-sm border border-border-strong px-3 py-2"
          />
          <span className="flex items-center justify-between text-caption text-text-secondary">
            {allowQuestion ? (
              <label className="inline-flex min-h-8 items-center gap-2">
                <input type="checkbox" className="size-5" checked={asQuestion} onChange={(e) => setAsQuestion(e.target.checked)} />
                Hỏi cố vấn
              </label>
            ) : (
              <span />
            )}
            {body.length > MAX_LENGTH - 200 && (
              <span>
                {body.length}/{MAX_LENGTH}
              </span>
            )}
          </span>
        </div>
        <Button type="submit" icon="send" disabled={!body.trim()}>
          Gửi
        </Button>
      </form>
    </div>
  )
}

function ThreadDrawer({ messageId, channel, readOnly, onClose }: { messageId: string | null; channel: Channel; readOnly: boolean; onClose: () => void }) {
  const thread = useThread(messageId)
  const isMine = useIsMine()
  return (
    <Drawer open={!!messageId} onClose={onClose} title="Luồng trả lời" footer={!readOnly && messageId ? <div className="w-full"><Composer channel={channel} parentMessageId={messageId} /></div> : undefined}>
      {thread.error ? (
        <ErrorState error={thread.error} onRetry={() => void thread.refetch()} />
      ) : !thread.data ? (
        <Skeleton className="h-16 rounded-lg" />
      ) : (
        <ol aria-label="Trả lời" className="flex flex-col gap-2">
          <MessageItem message={thread.data.root} mine={isMine(thread.data.root.author)} dayChanged={false} />
          <li className="border-b border-border pb-1 text-caption text-text-secondary">{thread.data.replies.length} trả lời</li>
          {thread.data.replies.map((r) => (
            <MessageItem key={r.id} message={r} mine={isMine(r.author)} dayChanged={false} />
          ))}
        </ol>
      )}
    </Drawer>
  )
}
