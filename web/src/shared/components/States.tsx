import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '@/shared/api/apiClient'
import { Banner, Button, Icon, Skeleton } from './ui'

/**
 * W26 – trạng thái dùng chung: đang tải (bảng 8 hàng skeleton), trống, không có kết quả lọc,
 * lỗi + Thử lại, không có quyền (403), không tìm thấy trang (404). Không có màn hình ngõ cụt.
 */

export function EmptyState({
  icon = 'inbox',
  title,
  description,
  action,
}: {
  icon?: string
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-2 px-6 py-12 text-center">
      <Icon name={icon} size={48} className="text-text-secondary" />
      <p className="text-subtitle font-semibold">{title}</p>
      {description && <p className="max-w-[420px] text-body text-text-secondary">{description}</p>}
      {action && <div className="mt-2">{action}</div>}
    </div>
  )
}

export function NoResults({ onClear }: { onClear: () => void }) {
  return (
    <EmptyState
      icon="search_off"
      title="Không tìm thấy kết quả"
      description="Thử bỏ bớt bộ lọc hoặc đổi từ khóa."
      action={
        <Button variant="outline" onClick={onClear}>
          Xóa bộ lọc
        </Button>
      }
    />
  )
}

/** Banner lỗi kèm Thử lại; hiện mã tra cứu để báo lại khi cần. */
export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const e = error instanceof ApiError ? error : undefined
  const title = e?.isNetwork ? 'Mất kết nối. Kiểm tra mạng rồi thử lại.' : (e?.title ?? 'Không tải được dữ liệu')
  return (
    <Banner
      tone="error"
      title={title}
      action={
        onRetry && (
          <Button variant="outline" icon="refresh" onClick={onRetry}>
            Thử lại
          </Button>
        )
      }
    >
      {e?.detail && <p>{e.detail}</p>}
      {e?.traceId && <p className="text-caption text-text-secondary">Mã tra cứu: {e.traceId}</p>}
    </Banner>
  )
}

/** 8 hàng skeleton cao 52 px trong khung bảng (DESIGN.md 8.1). */
export function TableSkeleton({ columns = 4, rows = 8 }: { columns?: number; rows?: number }) {
  return (
    <div role="status" aria-label="Đang tải">
      {Array.from({ length: rows }, (_, r) => (
        <div key={r} className="flex h-[52px] items-center gap-6 border-b border-border px-4">
          {Array.from({ length: columns }, (_, c) => (
            <Skeleton key={c} className={c === 0 ? 'h-4 w-40' : 'h-4 w-24'} />
          ))}
        </div>
      ))}
    </div>
  )
}

export function Forbidden({ homeHref = '/' }: { homeHref?: string }) {
  return (
    <FullPage>
      <EmptyState
        icon="lock"
        title="Bạn không có quyền xem trang này"
        description="Trang này dành cho vai trò khác. Nếu bạn nghĩ đây là nhầm lẫn, hãy liên hệ Bộ môn EXE."
        action={
          <Link to={homeHref} className="inline-flex min-h-10 items-center rounded-md bg-primary px-4 font-medium text-white">
            Về trang chủ
          </Link>
        }
      />
    </FullPage>
  )
}

export function NotFound() {
  return (
    <FullPage>
      <EmptyState
        icon="explore_off"
        title="Không tìm thấy trang"
        description="Đường dẫn có thể đã cũ hoặc gõ nhầm."
        action={
          <Link to="/" className="inline-flex min-h-10 items-center rounded-md bg-primary px-4 font-medium text-white">
            Về trang chủ
          </Link>
        }
      />
    </FullPage>
  )
}

function FullPage({ children }: { children: ReactNode }) {
  return <main className="grid min-h-screen place-items-center bg-background p-4">{children}</main>
}
