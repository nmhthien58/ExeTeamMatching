import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { env } from '@/shared/config/env'
import { tokenStore } from '@/shared/api/tokenStore'

/** Trạng thái kết nối hiển thị cho người dùng. */
export type HubStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

/** Nhóm trên hub (collab.yaml): `JoinChannel` / `JoinWorkspace` và `Leave…` tương ứng. */
export type HubGroup = 'Channel' | 'Workspace'

/** Phần của HubConnection mà client dùng – test thay bằng bản giả. */
export type HubConnectionLike = Pick<HubConnection, 'start' | 'stop' | 'on' | 'off' | 'invoke' | 'onreconnecting' | 'onreconnected' | 'onclose'>

// shortcut: token lấy lúc (re)kết nối, không tự refresh – kết nối lại sau khi access token hết hạn (15 phút) sẽ báo mất kết nối
// cho tới khi một lời gọi REST refresh token; bấm Thử lại là đủ. Thêm refresh vào accessTokenFactory nếu hay gặp.
const createConnection = (): HubConnectionLike =>
  new HubConnectionBuilder()
    .withUrl(`${env.apiBaseUrl}/hubs/collab`, { accessTokenFactory: () => tokenStore.getAccessToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

/**
 * Một kết nối `/hubs/collab` dùng chung cho mọi màn workspace (FE-33). Kết nối khi có màn đầu tiên dùng, ngắt khi màn cuối rời;
 * tự kết nối lại (withAutomaticReconnect) và vào lại các kênh / workspace đang nghe sau khi kết nối lại.
 */
export class CollabHub {
  private connection: HubConnectionLike | null = null
  private users = 0
  private groups = new Map<string, number>()
  private listeners = new Set<(status: HubStatus) => void>()
  status: HubStatus = 'idle'

  constructor(private readonly factory: () => HubConnectionLike = createConnection) {}

  subscribe(listener: (status: HubStatus) => void) {
    this.listeners.add(listener)
    return () => void this.listeners.delete(listener)
  }

  /** Màn hình bắt đầu dùng hub; trả hàm thôi dùng. */
  acquire() {
    if (this.users++ === 0) void this.start()
    return () => {
      if (--this.users === 0) void this.stop()
    }
  }

  /** Thử kết nối lại sau khi mất hẳn kết nối (nút "Thử lại"). */
  retry() {
    if (this.status === 'disconnected') void this.start()
  }

  on(event: string, handler: (payload: unknown) => void) {
    this.ensure().on(event, handler)
    return () => this.connection?.off(event, handler)
  }

  join(group: HubGroup, id: string) {
    const key = `${group}:${id}`
    this.groups.set(key, (this.groups.get(key) ?? 0) + 1)
    if (this.status === 'connected' && this.groups.get(key) === 1) void this.invoke(`Join${group}`, id)
    return () => {
      const left = (this.groups.get(key) ?? 1) - 1
      if (left > 0) return void this.groups.set(key, left)
      this.groups.delete(key)
      if (this.status === 'connected') void this.invoke(`Leave${group}`, id)
    }
  }

  private ensure() {
    if (!this.connection) {
      const c = (this.connection = this.factory())
      c.onreconnecting(() => this.setStatus('reconnecting'))
      c.onreconnected(() => {
        this.setStatus('connected')
        this.rejoin()
      })
      c.onclose(() => this.setStatus(this.users > 0 ? 'disconnected' : 'idle'))
    }
    return this.connection
  }

  private async start() {
    this.setStatus('connecting')
    try {
      await this.ensure().start()
      this.setStatus('connected')
      this.rejoin()
    } catch {
      this.setStatus('disconnected')
    }
  }

  private async stop() {
    await this.connection?.stop()
    this.setStatus('idle')
  }

  private rejoin() {
    for (const key of this.groups.keys()) {
      const [group, id] = key.split(':') as [HubGroup, string]
      void this.invoke(`Join${group}`, id)
    }
  }

  // Lỗi vào kênh (không có quyền, kênh không tồn tại) không chặn màn hình – dữ liệu vẫn tải bằng REST.
  private async invoke(method: string, id: string) {
    try {
      await this.connection?.invoke(method, id)
    } catch {
      /* bỏ qua */
    }
  }

  private setStatus(status: HubStatus) {
    this.status = status
    for (const l of this.listeners) l(status)
  }
}

export const collabHub = new CollabHub()
