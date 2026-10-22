import type { StatusTone } from '@/shared/components/ui'

const DAY = 24 * 60 * 60 * 1000

/** Ngày giờ theo DESIGN.md mục 9: `18:00 · 17/09/2026`. */
export function formatDateTime(iso: string): string {
  const d = new Date(iso)
  const time = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false })
  const date = d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
  return `${time} · ${date}`
}

/** "vừa xong", "15 phút trước", "2 giờ trước", "3 ngày trước". */
export function formatRelative(iso: string, now: Date = new Date()): string {
  const diff = now.getTime() - new Date(iso).getTime()
  const minutes = Math.floor(diff / 60_000)
  if (minutes < 1) return 'vừa xong'
  if (minutes < 60) return `${minutes} phút trước`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} giờ trước`
  const days = Math.floor(hours / 24)
  return days < 30 ? `${days} ngày trước` : new Date(iso).toLocaleDateString('vi-VN')
}

/** Đầu ngày theo giờ máy (so sánh "hôm nay" / "ngày mai"). */
const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime()

/**
 * Chip hạn của thẻ việc (DESIGN.md 6.3): "Hôm nay", "Ngày mai", "Còn 2 ngày", "Trễ 1 ngày".
 * Trễ → Error (icon `alarm`), trong 2 ngày → Warning, còn lại Neutral.
 */
export function dueChip(dueAt: string, now: Date = new Date(), overdue?: boolean): { text: string; tone: StatusTone; icon: string } {
  const days = Math.round((startOfDay(new Date(dueAt)) - startOfDay(now)) / DAY)
  const isLate = overdue ?? new Date(dueAt).getTime() < now.getTime()
  if (isLate) {
    return { text: days >= 0 ? 'Trễ hạn' : `Trễ ${-days} ngày`, tone: 'error', icon: 'alarm' }
  }
  if (days === 0) return { text: 'Hôm nay', tone: 'warning', icon: 'schedule' }
  if (days === 1) return { text: 'Ngày mai', tone: 'warning', icon: 'schedule' }
  return { text: `Còn ${days} ngày`, tone: days <= 2 ? 'warning' : 'neutral', icon: 'event' }
}

/** "820 KB", "2,4 MB". */
export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toLocaleString('vi-VN', { maximumFractionDigits: 1 })} MB`
}

/** Hai chữ cái đầu của tên (avatar chữ). */
export function initials(name: string): string {
  const parts = name.trim().split(/\s+/)
  const letters = parts.length > 1 ? parts[parts.length - 2][0] + parts[parts.length - 1][0] : parts[0]?.slice(0, 2) ?? '?'
  return letters.toUpperCase()
}
