import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

export interface AppNotification {
  id: string
  type: string
  title: string
  content: string
  linkUrl: string | null
  isRead: boolean
  createdAt: string
}

interface NotificationPage {
  items: AppNotification[]
  page: number
  totalPages: number
}

export const notificationKeys = {
  all: ['notifications'] as const,
  page: (page: number) => ['notifications', 'page', page] as const,
  unread: ['notifications', 'unread-count'] as const,
}

export function useNotifications(page: number) {
  return useQuery({
    queryKey: notificationKeys.page(page),
    queryFn: async () => (await api.get<NotificationPage>('/api/notifications', { params: { page, pageSize: 20 } })).data,
    refetchInterval: 30_000,
  })
}

export function useUnreadCount() {
  return useQuery({
    queryKey: notificationKeys.unread,
    queryFn: async () => (await api.get<{ count: number }>('/api/notifications/unread-count')).data.count,
    refetchInterval: 30_000,
  })
}

export function useMarkNotificationRead() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (id: string) => { await api.patch(`/api/notifications/${id}/read`) },
    onSuccess: () => void client.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}

export function useMarkAllNotificationsRead() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async () => { await api.patch('/api/notifications/read-all') },
    onSuccess: () => void client.invalidateQueries({ queryKey: notificationKeys.all }),
  })
}
