import { Drawer } from '@/features/workspace/components'
import { formatDateTime } from '@/features/workspace/format'
import type { ClientError } from './api'
import { KindChip } from './KindChip'

/** Chi tiết một báo cáo lỗi: thông tin máy, màn đang mở và stack trace. */
export function ClientErrorDrawer({ error, onClose }: { error: ClientError; onClose: () => void }) {
  const device = [error.deviceModel, error.osVersion].filter(Boolean).join(' · ')
  const rows: [string, string][] = [
    ['Thời điểm', formatDateTime(error.occurredAt)],
    ['Server nhận lúc', formatDateTime(error.receivedAt)],
    ['Màn hình', error.screen ?? '–'],
    ['Phiên bản app', error.appVersion],
    ['Nền tảng', error.platform],
    ['Thiết bị', device || '–'],
    ['Vai trò', error.role ?? '–'],
    ['Mã người dùng', error.userId ?? 'Chưa đăng nhập'],
  ]

  return (
    <Drawer open onClose={onClose} title={error.exceptionType}>
      <div className="flex flex-col gap-5">
        <div className="flex flex-col items-start gap-2">
          <KindChip kind={error.kind} />
          <p className="break-words text-body">{error.message}</p>
        </div>

        <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-body">
          {rows.map(([label, value]) => (
            <div key={label} className="contents">
              <dt className="text-text-secondary">{label}</dt>
              <dd className="min-w-0 break-words">{value}</dd>
            </div>
          ))}
        </dl>

        <section>
          <h3 className="mb-2 text-subtitle font-semibold">Stack trace</h3>
          {error.stackTrace ? (
            <pre
              /* Vùng cuộn phải nhận Tab để người dùng bàn phím đọc hết stack trace (WCAG 2.1.1); có tên vùng và viền focus (CR-03, MB-59). */
              tabIndex={0}
              role="region"
              aria-label="Stack trace"
              className="max-h-96 overflow-auto rounded-sm bg-ink p-4 font-mono text-caption text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary"
            >
              {error.stackTrace}
            </pre>
          ) : (
            <p className="text-text-secondary">Báo cáo này không có stack trace.</p>
          )}
        </section>
      </div>
    </Drawer>
  )
}
