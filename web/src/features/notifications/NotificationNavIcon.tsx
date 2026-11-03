import { Icon } from '@/shared/components/ui'
import { useUnreadCount } from './api'

export function NotificationNavIcon({ filled = false }: { filled?: boolean }) {
  const { data: count } = useUnreadCount()
  return <span className="relative inline-flex size-6 items-center justify-center">
    <Icon name="notifications" filled={filled} className={filled ? 'text-primary' : 'text-text-secondary'} />
    {!!count && <span className="absolute -right-3 -top-2 min-w-4 rounded-full bg-error px-1 text-center text-caption font-semibold text-white" aria-label={`${count} thông báo chưa đọc`}>{count > 99 ? '99+' : count}</span>}
  </span>
}
