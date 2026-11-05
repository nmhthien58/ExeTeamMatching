import { useEffect, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import { toApiError } from '@/shared/api/apiClient'
import { useAuth } from '@/shared/auth/AuthProvider'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon, Skeleton, StatusChip } from '@/shared/components/ui'
import { Avatar, Drawer, RoleChip } from '@/features/workspace/components'
import { formatDateTime, formatSize } from '@/features/workspace/format'
import { useSnackbar } from '@/features/workspace/useSnackbar'
import { downloadFile, fileIcon, useFolder, type ProjectFile } from '@/features/files/api'
import { RealtimeStatusBar } from '@/features/realtime/RealtimeStatusBar'
import { useHubEvent, useHubGroup } from '@/features/realtime/useRealtime'
import { isStudentRole, useWorkspace, type PersonRef } from '@/features/workspace/api'
import {
  applyMessage,
  canEdit,
  channelLabel,
  deleteMessage,
  updateRoot,
  useHistory,
  useMarkRead,
  useSendMessage,
  useThread,
  useEditMessage,
  type Channel,
  type Message,
  type QuestionStatus,
  type SendInput,
} from './api'

const MAX_LENGTH = 2000
const MAX_ATTACHMENTS = 5

interface Pending extends SendInput {
  failed?: string
}

/**
 * C03 – Kênh tin nhắn (FE-38): tin theo ngày (mới nhất ở dưới), bong bóng, luồng trả lời trong drawer, câu hỏi cho cố vấn
 * (Chờ / Đã trả lời), gửi lạc quan kèm "Gửi lại", Enter gửi / Shift + Enter xuống dòng, đánh dấu đã đọc, cập nhật realtime,
 * đính kèm file có sẵn ở C06 và @nhắc tên.
 */
// shortcut: chưa có sửa / xóa tin trên giao diện; bổ sung khi cần.
export function ChannelView({ channel, title, readOnly, isStudent }: { channel: Channel; title: string; readOnly: boolean; isStudent: boolean }) {
  const queryClient = useQueryClient()
  const history = useHistory(channel.id)
  const markRead = useMarkRead(channel.id, channel.workspaceId)
  const [thread, setThread] = useState<string | null>(null)
  const isMine = useIsMine()
  const listRef = useRef<HTMLOListElement>(null)
  const edit = useEditMessage()
  const snackbar = useSnackbar()
  // Tin đang chờ xóa: ẩn ngay, 5 giây sau mới gọi API (Hoàn tác thì hiện lại).
  const [hidden, setHidden] = useState<Set<string>>(new Set())
  const unhide = (id: string) => setHidden((s) => new Set([...s].filter((x) => x !== id)))
  const remove = (m: Message) => {
    setHidden((s) => new Set(s).add(m.id))
    const timer = window.setTimeout(async () => {
      try {
        await deleteMessage(m.id)
        updateRoot(queryClient, channel.id, m.id, (x) => ({ ...x, isDeleted: true, body: '' }))
      } catch (error) {
        snackbar.show(toApiError(error).detail ?? 'Không xóa được tin')
      } finally {
        unhide(m.id)
      }
    }, 5000)
    snackbar.show('Đã xóa tin', () => {
      window.clearTimeout(timer)
      unhide(m.id)
    })
  }

  useHubGroup('Channel', channel.id)
  useHubEvent<Message>('messageCreated', (m) => m.channelId === channel.id && applyMessage(queryClient, m))
  useHubEvent<Message>('messageUpdated', (m) => m.channelId === channel.id && applyMessage(queryClient, m))
  useHubEvent<{ channelId: string; messageId: string }>('messageDeleted', (e) =>
    e.channelId === channel.id && updateRoot(queryClient, channel.id, e.messageId, (m) => ({ ...m, isDeleted: true, body: '' })),
  )
  useHubEvent<{ channelId: string; messageId: string; status: QuestionStatus }>('questionStatusChanged', (e) =>
    e.channelId === channel.id && updateRoot(queryClient, channel.id, e.messageId, (m) => ({ ...m, questionStatus: e.status })),
  )

  const messages = (history.data?.pages ?? []).flatMap((p) => p.items).filter((m) => !hidden.has(m.id)).reverse()
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
            <MessageItem
              key={m.id}
              message={m}
              mine={isMine(m.author)}
              dayChanged={!sameDay(messages[i - 1]?.createdAt, m.createdAt)}
              onThread={() => setThread(m.id)}
              onEdit={readOnly ? undefined : (body) => edit.mutateAsync({ id: m.id, body })}
              onDelete={readOnly ? undefined : () => remove(m)}
            />
          ))}
        </ol>
      )}
      {readOnly ? (
        <p className="border-t border-border px-4 py-3 text-text-secondary">Bộ môn chỉ xem kênh này.</p>
      ) : (
        <Composer channel={channel} allowQuestion={channel.kind === 'ADVISORS' && isStudent} />
      )}
      <ThreadDrawer messageId={thread} channel={channel} readOnly={readOnly} onClose={() => setThread(null)} />
      {snackbar.element}
    </section>
  )
}

function useIsMine() {
  const { user } = useAuth()
  const ids = [user?.studentId, user?.lecturerId, user?.mentorId].filter(Boolean)
  return (author: PersonRef) => ids.includes(author.personId)
}

const sameDay = (a: string | undefined, b: string) => !!a && new Date(a).toDateString() === new Date(b).toDateString()

// shortcut: sửa / xóa chỉ ở tin gốc trong kênh, chưa có cho trả lời trong luồng; thêm vào ThreadDrawer khi cần.
function MessageItem({
  message: m,
  mine,
  dayChanged,
  onThread,
  onEdit,
  onDelete,
}: {
  message: Message
  mine: boolean
  dayChanged: boolean
  onThread?: () => void
  onEdit?: (body: string) => Promise<unknown>
  onDelete?: () => void
}) {
  const [draft, setDraft] = useState<string | null>(null)
  const [editError, setEditError] = useState<string | null>(null)
  const ownActions = mine && !m.isDeleted
  const save = async () => {
    const body = draft?.trim()
    if (!body || !onEdit) return
    try {
      await onEdit(body)
      setDraft(null)
      setEditError(null)
    } catch (error) {
      setEditError(toApiError(error).detail ?? 'Không sửa được tin')
    }
  }
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
            {m.isDeleted ? (
              <em className="text-text-secondary">Tin nhắn đã bị xóa</em>
            ) : draft !== null ? (
              <div className="flex flex-col gap-2">
                <textarea
                  aria-label="Sửa tin nhắn"
                  rows={2}
                  value={draft}
                  maxLength={MAX_LENGTH}
                  onChange={(e) => setDraft(e.target.value)}
                  className="min-w-64 resize-none rounded-sm border border-border-strong px-3 py-2"
                />
                {editError && (
                  <span role="alert" className="text-caption text-error">
                    {editError}
                  </span>
                )}
                <span className="flex justify-end gap-2">
                  <Button variant="text" className="min-h-8 px-2" onClick={() => setDraft(null)}>
                    Hủy
                  </Button>
                  <Button className="min-h-8 px-3" disabled={!draft.trim()} onClick={() => void save()}>
                    Lưu
                  </Button>
                </span>
              </div>
            ) : (
              <p className="whitespace-pre-wrap break-words">{m.body}</p>
            )}
            {m.attachments?.map((f) => (
              <button
                key={f.id}
                type="button"
                onClick={() => void downloadFile(f.id)}
                aria-label={`Tải xuống ${f.name}`}
                className="mt-1 flex min-h-10 w-full items-center gap-2 rounded-md border border-border bg-surface px-2 text-left hover:bg-background"
              >
                <Icon name={fileIcon(f.name)} className="text-text-secondary" />
                <span className="min-w-0 flex-1 truncate">{f.name}</span>
                <span className="text-caption text-text-secondary">{formatSize(f.sizeBytes)}</span>
              </button>
            ))}
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
            {ownActions && onEdit && draft === null && canEdit(m) && (
              <Button variant="text" icon="edit" onClick={() => setDraft(m.body)} className="min-h-8 px-2">
                Sửa
              </Button>
            )}
            {ownActions && onDelete && draft === null && (
              <Button variant="text" icon="delete" onClick={onDelete} className="min-h-8 px-2">
                Xóa
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
  const [attached, setAttached] = useState<ProjectFile[]>([])
  const [picking, setPicking] = useState(false)
  const [mentions, setMentions] = useState<PersonRef[]>([])
  const textarea = useRef<HTMLTextAreaElement>(null)
  const people = useWorkspace(channel.workspaceId).data?.people
  const isMine = useIsMine()
  // shortcut: chỉ gợi ý khi đang gõ @ ở cuối ô soạn tin; cần thì theo vị trí con trỏ.
  const mentionQuery = /(?:^|\s)@([^@\n]*)$/.exec(body)?.[1]?.toLowerCase()
  const suggestions =
    mentionQuery === undefined
      ? []
      : (people ?? []).filter(
          (p) => !isMine(p) && (channel.kind === 'ADVISORS' || isStudentRole(p.role ?? undefined)) && p.displayName.toLowerCase().includes(mentionQuery),
        )

  const mention = (person: PersonRef) => {
    setBody(body.replace(/@[^@\n]*$/, () => `@${person.displayName} `))
    setMentions((list) => [...list.filter((p) => p.personId !== person.personId), person])
    textarea.current?.focus()
  }
  const toggleFile = (file: ProjectFile) =>
    setAttached((list) => (list.some((f) => f.id === file.id) ? list.filter((f) => f.id !== file.id) : [...list, file]))

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
    const mentionPersonIds = mentions.filter((p) => text.includes(`@${p.displayName}`)).map((p) => p.personId)
    submit({
      clientMessageId: crypto.randomUUID(),
      body: text,
      parentMessageId,
      isAdvisorQuestion: asQuestion || undefined,
      attachmentFileIds: attached.length > 0 ? attached.map((f) => f.id) : undefined,
      mentionPersonIds: mentionPersonIds.length > 0 ? mentionPersonIds : undefined,
    })
    setBody('')
    setAsQuestion(false)
    setAttached([])
    setMentions([])
    setPicking(false)
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
                onClick={() =>
                  submit({
                    clientMessageId: p.clientMessageId,
                    body: p.body,
                    parentMessageId: p.parentMessageId,
                    isAdvisorQuestion: p.isAdvisorQuestion,
                    attachmentFileIds: p.attachmentFileIds,
                    mentionPersonIds: p.mentionPersonIds,
                  })
                }
              >
                Gửi lại
              </Button>
            </>
          ) : (
            <span className="text-text-secondary">Đang gửi…</span>
          )}
        </p>
      ))}
      {picking && <AttachPanel workspaceId={channel.workspaceId} selected={attached} onToggle={toggleFile} onClose={() => setPicking(false)} />}
      {attached.length > 0 && (
        <ul aria-label="File đính kèm" className="mb-2 flex flex-wrap gap-2">
          {attached.map((f) => (
            <li key={f.id} className="flex min-h-8 max-w-60 items-center gap-1 rounded-full bg-info-container pl-3 text-caption">
              <span className="truncate">{f.name}</span>
              <button type="button" aria-label={`Bỏ ${f.name}`} onClick={() => toggleFile(f)} className="grid size-8 place-items-center">
                <Icon name="close" size={20} />
              </button>
            </li>
          ))}
        </ul>
      )}
      {suggestions.length > 0 && (
        <ul aria-label="Nhắc tên" className="mb-2 max-h-40 overflow-y-auto rounded-md border border-border">
          {suggestions.map((p) => (
            <li key={p.personId}>
              <button type="button" onClick={() => mention(p)} className="flex min-h-10 w-full items-center gap-2 px-3 text-left hover:bg-background">
                <span className="flex-1 truncate">{p.displayName}</span>
                <RoleChip role={p.role} />
              </button>
            </li>
          ))}
        </ul>
      )}
      <form
        className="flex items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault()
          sendDraft()
        }}
      >
        <div className="flex flex-1 flex-col gap-1">
          <textarea
            ref={textarea}
            aria-label="Tin nhắn"
            rows={2}
            value={body}
            maxLength={MAX_LENGTH}
            placeholder={parentMessageId ? 'Trả lời trong luồng' : 'Nhập tin nhắn (Enter để gửi, Shift + Enter xuống dòng)'}
            onChange={(e) => setBody(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey && !e.nativeEvent.isComposing) {
                e.preventDefault()
                if (suggestions.length > 0) mention(suggestions[0])
                else sendDraft()
              }
            }}
            className="resize-none rounded-sm border border-border-strong px-3 py-2"
          />
          <span className="flex items-center justify-between text-caption text-text-secondary">
            <span className="flex items-center gap-3">
              <Button type="button" variant="text" icon="attach_file" className="min-h-8 px-2" onClick={() => setPicking(!picking)}>
                Đính kèm
              </Button>
              {allowQuestion && (
                <label className="inline-flex min-h-8 items-center gap-2">
                  <input type="checkbox" className="size-5" checked={asQuestion} onChange={(e) => setAsQuestion(e.target.checked)} />
                  Hỏi cố vấn
                </label>
              )}
            </span>
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

/** Chọn tối đa 5 file có sẵn của dự án (C06) ngay trong khung soạn tin, nên dùng được cả trong drawer luồng. File EXE101 không đính kèm được. */
function AttachPanel({ workspaceId, selected, onToggle, onClose }: { workspaceId: string; selected: ProjectFile[]; onToggle: (file: ProjectFile) => void; onClose: () => void }) {
  const [folderId, setFolderId] = useState<string | undefined>()
  const folder = useFolder(workspaceId, folderId)
  const content = folder.data
  const folders = content?.folders.filter((f) => !f.isArchive) ?? []
  const files = content?.files.filter((f) => !f.isArchive) ?? []
  return (
    <section aria-label="Chọn file đính kèm" className="mb-2 rounded-md border border-border p-2">
      <div className="flex items-center justify-between">
        {folderId ? (
          <Button type="button" variant="text" icon="arrow_back" className="min-h-8 px-2" onClick={() => setFolderId(content?.folder?.parentId ?? undefined)}>
            {content?.folder?.name ?? 'Quay lại'}
          </Button>
        ) : (
          <span className="px-2 text-caption text-text-secondary">Chọn tối đa {MAX_ATTACHMENTS} file của dự án</span>
        )}
        <button type="button" aria-label="Đóng chọn file" onClick={onClose} className="grid size-8 place-items-center">
          <Icon name="close" size={20} />
        </button>
      </div>
      {folder.error ? (
        <ErrorState error={folder.error} onRetry={() => void folder.refetch()} />
      ) : !content ? (
        <Skeleton className="h-12" />
      ) : folders.length === 0 && files.length === 0 ? (
        <p className="py-3 text-center text-text-secondary">Chưa có file. Tải lên ở mục File trước.</p>
      ) : (
        <ul className="max-h-48 overflow-y-auto">
          {folders.map((f) => (
            <li key={f.id}>
              <button type="button" onClick={() => setFolderId(f.id)} className="flex min-h-10 w-full items-center gap-2 rounded-md px-2 text-left hover:bg-background">
                <Icon name="folder" className="text-primary" />
                <span className="flex-1 truncate">{f.name}</span>
                <Icon name="chevron_right" className="text-text-secondary" />
              </button>
            </li>
          ))}
          {files.map((f) => {
            const checked = selected.some((s) => s.id === f.id)
            return (
              <li key={f.id}>
                <label className="flex min-h-10 items-center gap-2 rounded-md px-2 hover:bg-background">
                  <input type="checkbox" className="size-5" checked={checked} disabled={!checked && selected.length >= MAX_ATTACHMENTS} onChange={() => onToggle(f)} />
                  <Icon name={fileIcon(f.name)} className="text-text-secondary" />
                  <span className="min-w-0 flex-1 truncate">{f.name}</span>
                  <span className="text-caption text-text-secondary">{formatSize(f.sizeBytes)}</span>
                </label>
              </li>
            )
          })}
        </ul>
      )}
    </section>
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
