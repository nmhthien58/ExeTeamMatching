import { useInfiniteQuery, useMutation, useQueries, useQuery, useQueryClient, type InfiniteData, type QueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Paged } from '@/shared/components/DataTable'
import type { ProjectFile } from '@/features/files/api'
import type { PersonRef } from '@/features/workspace/api'

// Kiểu theo docs/contracts/openapi/collab.yaml (tag Messages) – C02, C03, quy tắc 22.

export type QuestionStatus = 'OPEN' | 'ANSWERED'

export interface Message {
  id: string
  clientMessageId?: string | null
  channelId: string
  parentMessageId?: string | null
  author: PersonRef
  body: string
  isDeleted: boolean
  isAdvisorQuestion: boolean
  questionStatus?: QuestionStatus | null
  replyCount: number
  attachments?: ProjectFile[]
  createdAt: string
  editedAt?: string | null
}

export interface Channel {
  id: string
  workspaceId: string
  kind: 'TEAM' | 'ADVISORS'
  unreadCount: number
  lastMessage?: Message | null
}

export interface AdvisorQuestion {
  message: Message
  workspaceId: string
  teamName: string
  status: QuestionStatus
  waitingHours: number
}

interface MessagePage {
  items: Message[]
  nextCursor?: string | null
}

export const channelLabel = (kind: Channel['kind']) => (kind === 'TEAM' ? 'Nhóm' : 'Cố vấn')

export const messageKeys = {
  channels: (workspaceId: string) => ['channels', workspaceId] as const,
  history: (channelId: string) => ['messages', channelId] as const,
  thread: (messageId: string) => ['messages', 'thread', messageId] as const,
  openQuestions: ['advisor', 'questions', 'OPEN'] as const,
}

export function useChannels(workspaceId: string | undefined) {
  return useQuery({
    queryKey: messageKeys.channels(workspaceId ?? ''),
    queryFn: async () => (await api.get<Channel[]>(`/api/workspaces/${workspaceId}/channels`)).data,
    enabled: !!workspaceId,
  })
}

/** C02 của cố vấn – kênh của mọi workspace mình phụ trách (API chỉ trả kênh Cố vấn cho cố vấn). */
export function useChannelsOf(workspaceIds: string[]) {
  return useQueries({
    queries: workspaceIds.map((id) => ({
      queryKey: messageKeys.channels(id),
      queryFn: async () => (await api.get<Channel[]>(`/api/workspaces/${id}/channels`)).data,
    })),
  })
}

/** Câu hỏi đang chờ trả lời của mọi nhóm mình là cố vấn (lọc "Cần trả lời"). */
export function useOpenQuestions(enabled: boolean) {
  return useQuery({
    queryKey: messageKeys.openQuestions,
    queryFn: async () => (await api.get<Paged<AdvisorQuestion>>('/api/advisor/questions', { params: { status: 'OPEN', pageSize: 100 } })).data.items,
    enabled,
  })
}

/** C03 – tin gốc, mới nhất trước; trang sau = tin cũ hơn (cursor). */
export function useHistory(channelId: string) {
  return useInfiniteQuery({
    queryKey: messageKeys.history(channelId),
    queryFn: async ({ pageParam }) =>
      (await api.get<MessagePage>(`/api/channels/${channelId}/messages`, { params: { before: pageParam || undefined, limit: 30 } })).data,
    initialPageParam: '',
    getNextPageParam: (last) => last.nextCursor ?? undefined,
  })
}

export function useThread(messageId: string | null) {
  return useQuery({
    queryKey: messageKeys.thread(messageId ?? ''),
    queryFn: async () => (await api.get<{ root: Message; replies: Message[] }>(`/api/messages/${messageId}/thread`)).data,
    enabled: !!messageId,
  })
}

export interface SendInput {
  clientMessageId: string
  body: string
  parentMessageId?: string
  isAdvisorQuestion?: boolean
  attachmentFileIds?: string[]
  mentionPersonIds?: string[]
}

export function useSendMessage(channelId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (input: SendInput) => (await api.post<Message>(`/api/channels/${channelId}/messages`, input)).data,
    onSuccess: (message) => applyMessage(queryClient, message),
  })
}

/** Sửa tin của mình trong 15 phút (collab.yaml); quá hạn server trả 409 `edit-window-closed`. */
export const EDIT_WINDOW_MS = 15 * 60_000
export const canEdit = (m: Message, now = Date.now()) => now - new Date(m.createdAt).getTime() < EDIT_WINDOW_MS

export function useEditMessage() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, body }: { id: string; body: string }) => (await api.patch<Message>(`/api/messages/${id}`, { body })).data,
    onSuccess: (message) => applyMessage(queryClient, message),
  })
}

/** Xóa mềm tin của mình. Màn hình cho Hoàn tác 5 giây trước khi gọi (contract). */
export const deleteMessage = async (id: string) => void (await api.delete(`/api/messages/${id}`))

export function useMarkRead(channelId: string, workspaceId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async (lastReadMessageId: string) => (await api.post(`/api/channels/${channelId}/read`, { lastReadMessageId })).data,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: messageKeys.channels(workspaceId) }),
  })
}

type History = InfiniteData<MessagePage, string>

/** Đưa tin mới / tin đã sửa (REST hoặc hub `messageCreated`, `messageUpdated`) vào cache: lịch sử, luồng, số trả lời. Bỏ trùng theo id. */
export function applyMessage(queryClient: QueryClient, message: Message) {
  if (!message.parentMessageId) {
    queryClient.setQueryData<History>(messageKeys.history(message.channelId), (data) => {
      if (!data) return data
      const exists = data.pages.some((p) => p.items.some((m) => m.id === message.id))
      const pages = exists
        ? data.pages.map((p) => ({ ...p, items: p.items.map((m) => (m.id === message.id ? message : m)) }))
        : [{ ...data.pages[0], items: [message, ...data.pages[0].items] }, ...data.pages.slice(1)]
      return { ...data, pages }
    })
    return
  }
  queryClient.setQueryData<{ root: Message; replies: Message[] }>(messageKeys.thread(message.parentMessageId), (thread) =>
    thread && !thread.replies.some((r) => r.id === message.id) ? { ...thread, replies: [...thread.replies, message] } : thread,
  )
  updateRoot(queryClient, message.channelId, message.parentMessageId, (root) => ({ ...root, replyCount: root.replyCount + 1 }), message.id)
}

const countedReplies = new Set<string>()

/** Sửa tin gốc trong lịch sử (số trả lời, trạng thái câu hỏi, đã xóa). `onceKey` tránh cộng hai lần khi REST và hub cùng báo. */
export function updateRoot(queryClient: QueryClient, channelId: string, rootId: string, update: (m: Message) => Message, onceKey?: string) {
  if (onceKey) {
    if (countedReplies.has(onceKey)) return
    countedReplies.add(onceKey)
  }
  queryClient.setQueryData<History>(messageKeys.history(channelId), (data) =>
    data && { ...data, pages: data.pages.map((p) => ({ ...p, items: p.items.map((m) => (m.id === rootId ? update(m) : m)) })) },
  )
  queryClient.setQueryData<{ root: Message; replies: Message[] }>(messageKeys.thread(rootId), (thread) => thread && { ...thread, root: update(thread.root) })
}
