import { useSyncExternalStore } from 'react'
import { getFirebaseApp } from './firebase'

/**
 * Remote Config của web (FE-46). Giá trị mặc định nằm trong code (file `firebase/remote-config-defaults.json`
 * là của mobile và được test so khớp tuyệt đối). Tham số `web_notice`: chuỗi khác rỗng thì layout hiện banner
 * thông báo cho mọi người dùng web – đổi trên Console là web đổi, không cần build lại.
 */
export interface WebRemoteConfig {
  webNotice: string
}

const defaults: WebRemoteConfig = { webNotice: '' }
let current: WebRemoteConfig = defaults
const listeners = new Set<() => void>()
let started = false

function publish(next: WebRemoteConfig) {
  current = next
  listeners.forEach((listener) => listener())
}

/** Tải và kích hoạt một lần (timeout 5 s); lỗi hoặc mất mạng thì giữ mặc định. */
export async function startRemoteConfig(): Promise<void> {
  if (started) return
  started = true
  try {
    const app = await getFirebaseApp()
    if (!app) return
    const sdk = await import('firebase/remote-config')
    const rc = sdk.getRemoteConfig(app)
    rc.settings.fetchTimeoutMillis = 5000
    rc.settings.minimumFetchIntervalMillis = import.meta.env.DEV ? 0 : 3_600_000
    rc.defaultConfig = { web_notice: defaults.webNotice }
    await sdk.fetchAndActivate(rc)
    publish({ webNotice: sdk.getString(rc, 'web_notice') })
    // Cập nhật realtime khi đổi trên Console (Remote Config realtime, SDK web ≥ 11).
    sdk.onConfigUpdate(rc, {
      next: () => {
        void sdk.activate(rc).then(() => publish({ webNotice: sdk.getString(rc, 'web_notice') }))
      },
      error: (error) => console.warn('Remote Config update failed', error),
      complete: () => {},
    })
  } catch (error) {
    console.warn('Remote Config failed, dùng giá trị mặc định', error)
  }
}

export function useRemoteConfig(): WebRemoteConfig {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => current,
  )
}

/** Chỉ cho test. */
export function __setRemoteConfigForTest(next: Partial<WebRemoteConfig>) {
  publish({ ...current, ...next })
}
