import type { ReactNode } from 'react'
import { Banner, Button } from '@/shared/components/ui'
import type { CollabHub } from './collabHub'
import { HubContext, useCollabHub } from './useRealtime'

/** Test cung cấp hub giả. */
export function HubProvider({ hub, children }: { hub: CollabHub; children: ReactNode }) {
  return <HubContext.Provider value={hub}>{children}</HubContext.Provider>
}

/** Dải trạng thái: "Đang kết nối lại…" khi rớt mạng; mất hẳn kết nối thì có Thử lại. Kết nối bình thường không hiện gì. */
export function RealtimeStatusBar() {
  const { hub, status } = useCollabHub()
  if (status === 'reconnecting') return <Banner tone="warning" title="Đang kết nối lại…">Tin mới sẽ hiện khi kết nối lại.</Banner>
  if (status === 'disconnected')
    return (
      <Banner tone="warning" title="Mất kết nối realtime" action={<Button variant="outline" icon="refresh" onClick={() => hub.retry()}>Thử lại</Button>}>
        Tin mới không tự hiện; tải lại trang hoặc bấm Thử lại.
      </Banner>
    )
  return null
}
