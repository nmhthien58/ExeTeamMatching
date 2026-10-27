import { createContext, useContext, useEffect, useRef, useSyncExternalStore } from 'react'
import { collabHub, type CollabHub, type HubGroup } from './collabHub'

/** Hub dùng chung; test cung cấp hub giả bằng `HubProvider`. */
export const HubContext = createContext<CollabHub>(collabHub)

/** Màn workspace dùng realtime: giữ kết nối khi màn còn mở, trả trạng thái kết nối. */
export function useCollabHub() {
  const hub = useContext(HubContext)
  useEffect(() => hub.acquire(), [hub])
  const status = useSyncExternalStore(
    (cb) => hub.subscribe(cb),
    () => hub.status,
  )
  return { hub, status }
}

/** Nghe kênh / workspace khi `id` có giá trị; tự rời khi đổi id hoặc rời màn. */
export function useHubGroup(group: HubGroup, id: string | undefined) {
  const hub = useContext(HubContext)
  useEffect(() => (id ? hub.join(group, id) : undefined), [hub, group, id])
}

/** Nghe một sự kiện server → client (`messageCreated`…); handler luôn là bản mới nhất. */
export function useHubEvent<T>(event: string, handler: (payload: T) => void) {
  const hub = useContext(HubContext)
  const ref = useRef(handler)
  ref.current = handler
  useEffect(() => hub.on(event, (payload) => ref.current(payload as T)), [hub, event])
}
