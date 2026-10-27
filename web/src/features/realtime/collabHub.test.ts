import { describe, expect, it, vi } from 'vitest'
import { CollabHub, type HubConnectionLike } from './collabHub'

function fakeConnection() {
  const handlers: Record<string, ((...args: never[]) => void)[]> = {}
  let reconnecting = () => {}
  let reconnected = () => {}
  const connection = {
    start: vi.fn(async () => {}),
    stop: vi.fn(async () => {}),
    invoke: vi.fn<(method: string, id: string) => Promise<undefined>>(async () => undefined),
    on: vi.fn((event: string, h: (...args: never[]) => void) => void (handlers[event] = [...(handlers[event] ?? []), h])),
    off: vi.fn(),
    onreconnecting: vi.fn((cb: () => void) => void (reconnecting = cb)),
    onreconnected: vi.fn((cb: () => void) => void (reconnected = cb)),
    onclose: vi.fn(),
  }
  return {
    connection: connection as unknown as HubConnectionLike & typeof connection,
    emit: (event: string, payload: unknown) => handlers[event]?.forEach((h) => h(payload as never)),
    drop: () => reconnecting(),
    back: () => reconnected(),
  }
}

const flush = () => new Promise((r) => setTimeout(r, 0))

describe('CollabHub (FE-33)', () => {
  it('kết nối khi có màn đầu tiên, vào kênh, ngắt khi màn cuối rời', async () => {
    const fake = fakeConnection()
    const hub = new CollabHub(() => fake.connection)
    const releaseA = hub.acquire()
    const releaseB = hub.acquire()
    await flush()
    expect(fake.connection.start).toHaveBeenCalledTimes(1)
    expect(hub.status).toBe('connected')

    const leave = hub.join('Channel', 'c1')
    expect(fake.connection.invoke).toHaveBeenCalledWith('JoinChannel', 'c1')
    leave()
    expect(fake.connection.invoke).toHaveBeenCalledWith('LeaveChannel', 'c1')

    releaseA()
    expect(fake.connection.stop).not.toHaveBeenCalled()
    releaseB()
    await flush()
    expect(fake.connection.stop).toHaveBeenCalledTimes(1)
  })

  it('báo đang kết nối lại, vào lại kênh khi có mạng; chuyển sự kiện tới handler', async () => {
    const fake = fakeConnection()
    const hub = new CollabHub(() => fake.connection)
    const statuses: string[] = []
    hub.subscribe((s) => statuses.push(s))
    hub.acquire()
    hub.join('Workspace', 'w1')
    await flush()
    const received: unknown[] = []
    hub.on('messageCreated', (p) => received.push(p))
    fake.emit('messageCreated', { id: 'm1' })

    fake.drop()
    fake.back()
    expect(statuses).toEqual(['connecting', 'connected', 'reconnecting', 'connected'])
    expect(fake.connection.invoke.mock.calls.filter(([m]) => m === 'JoinWorkspace')).toHaveLength(2)
    expect(received).toEqual([{ id: 'm1' }])
  })

  it('không kết nối được → mất kết nối; Thử lại kết nối lần nữa', async () => {
    const fake = fakeConnection()
    fake.connection.start.mockRejectedValueOnce(new Error('401'))
    const hub = new CollabHub(() => fake.connection)
    hub.acquire()
    await flush()
    expect(hub.status).toBe('disconnected')
    hub.retry()
    await flush()
    expect(hub.status).toBe('connected')
  })
})
