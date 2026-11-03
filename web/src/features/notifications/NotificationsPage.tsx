import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import { EmptyState, ErrorState } from '@/shared/components/States'
import { Button, Icon } from '@/shared/components/ui'
import { PageHeader } from '@/shared/layout/PageHeader'
import { notificationKeys, useMarkAllNotificationsRead, useMarkNotificationRead, useNotifications, useUnreadCount, type AppNotification } from './api'

function destination(link: string | null, roleRoot: string): string | null {
  if (!link) return null
  try {
    const uri = new URL(link)
    if (uri.protocol !== 'app:') return null
    switch (uri.hostname) {
      case 'teams': return roleRoot === '/sv' ? '/sv/nhom' : `${roleRoot}/nhom`
      case 'invitations': return roleRoot === '/sv' ? '/sv/loi-moi' : roleRoot
      case 'change-requests': return roleRoot === '/sv' ? '/sv/yeu-cau-doi-nhom' : `${roleRoot}/nhom`
      case 'workspaces': return roleRoot === '/sv' ? '/sv/du-an' : uri.pathname.split('/')[1] ? `${roleRoot}/du-an/${uri.pathname.split('/')[1]}` : roleRoot
      case 'meeting-schedule': return roleRoot === '/sv' ? '/sv/du-an/lich-hop' : roleRoot === '/gv' ? '/gv/lich' : roleRoot
      case 'lecturer': return roleRoot === '/sv' ? '/sv/nhom/co-van' : `${roleRoot}/de-tai`
      default: return null
    }
  } catch { return null }
}

function sameDay(a: Date, b: Date) {
  return a.getFullYear() === b.getFullYear() && a.getMonth() === b.getMonth() && a.getDate() === b.getDate()
}

function dayLabel(date: Date) {
  const today = new Date()
  if (sameDay(date, today)) return 'Hôm nay'
  const yesterday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - 1)
  if (sameDay(date, yesterday)) return 'Hôm qua'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium' }).format(date)
}

export function NotificationsPage({ roleRoot }: { roleRoot: '/sv' | '/gv' | '/mentor' }) {
  const navigate = useNavigate()
  const client = useQueryClient()
  const [page, setPage] = useState(1)
  const [pages, setPages] = useState<AppNotification[]>([])
  const notifications = useNotifications(page)
  const unread = useUnreadCount()
  const markRead = useMarkNotificationRead()
  const markAll = useMarkAllNotificationsRead()
  const items = page === 1 ? notifications.data?.items ?? [] : [...pages, ...(notifications.data?.items ?? [])]

  const refresh = async () => {
    setPage(1)
    setPages([])
    await Promise.all([client.refetchQueries({ queryKey: notificationKeys.page(1), type: 'all' }), unread.refetch()])
  }
  const nextPage = () => {
    setPages(items)
    setPage(page + 1)
  }
  const open = async (item: AppNotification) => {
    try {
      if (!item.isRead) await markRead.mutateAsync(item.id)
      const target = destination(item.linkUrl, roleRoot)
      if (target) navigate(target)
    } catch { /* The mutation error is displayed below. */ }
  }

  return <>
    <PageHeader title="Thông báo" />
    <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
      <p className="text-text-secondary">{unread.data ? `${unread.data} thông báo chưa đọc` : 'Thông báo của bạn'}</p>
      <div className="flex flex-wrap gap-2">
        <Button variant="outline" icon="refresh" onClick={() => void refresh()}>Làm mới</Button>
        <Button variant="text" disabled={!unread.data || markAll.isPending} onClick={() => void markAll.mutateAsync()}>Đánh dấu đã đọc tất cả</Button>
      </div>
    </div>
    {markRead.error || markAll.error ? <p role="alert" className="mb-3 text-error">Không đánh dấu được thông báo. Vui lòng thử lại.</p> : null}
    {notifications.isLoading ? <p role="status">Đang tải thông báo…</p> : notifications.error ? <ErrorState error={notifications.error} onRetry={() => void notifications.refetch()} /> : items.length === 0 ? <EmptyState icon="notifications" title="Chưa có thông báo" description="Thông báo mới sẽ xuất hiện ở đây." /> : (
      <div className="space-y-4">
        <ul className="divide-y divide-border rounded-xl bg-surface">
          {items.map((item, index) => {
            const created = new Date(item.createdAt)
            const previous = index > 0 ? new Date(items[index - 1].createdAt) : null
            return <li key={item.id}>
              {(!previous || !sameDay(created, previous)) && <h2 className="bg-background px-4 py-2 font-semibold">{dayLabel(created)}</h2>}
              <button type="button" className="flex min-h-16 w-full items-start gap-3 p-4 text-left hover:bg-primary-container" onClick={() => void open(item)}>
                <span className="mt-1 w-5 shrink-0" aria-label={item.isRead ? 'Đã đọc' : 'Chưa đọc'}>{item.isRead ? <Icon name="notifications" size={20} /> : <Icon name="circle" size={20} className="text-primary" />}</span>
                <span className="min-w-0 flex-1"><span className={`block ${item.isRead ? '' : 'font-semibold'}`}>{item.title}</span><span className="block text-text-secondary">{item.content}</span></span>
                <time className="shrink-0 text-caption text-text-secondary" dateTime={item.createdAt}>{new Intl.DateTimeFormat('vi-VN', { hour: '2-digit', minute: '2-digit' }).format(created)}</time>
              </button>
            </li>
          })}
        </ul>
        {page < (notifications.data?.totalPages ?? 1) && <Button variant="outline" disabled={notifications.isFetching} onClick={nextPage}>Xem thêm</Button>}
      </div>
    )}
  </>
}
