import type { Analytics } from 'firebase/analytics'
import { getFirebaseApp } from './firebase'

let analyticsPromise: Promise<Analytics | null> | null = null

function getAnalyticsInstance(): Promise<Analytics | null> {
  analyticsPromise ??= getFirebaseApp().then(async (app) => {
    if (!app) return null
    const { getAnalytics, isSupported } = await import('firebase/analytics')
    return (await isSupported()) ? getAnalytics(app) : null
  })
  return analyticsPromise
}

async function safe(what: string, call: (analytics: Analytics, sdk: typeof import('firebase/analytics')) => void) {
  try {
    const analytics = await getAnalyticsInstance()
    if (!analytics) return
    call(analytics, await import('firebase/analytics'))
  } catch (error) {
    console.warn(`Analytics ${what} failed`, error)
  }
}

/**
 * Analytics của web (FE-46) – cùng tên sự kiện với mobile (docs/analytics-plan.md). Màn hình gọi qua đây,
 * không gọi SDK. Không đưa email / tên vào tham số. Chưa cấu hình Firebase thì không làm gì.
 */
export const analytics = {
  setUserId: (id: string | null) => safe('setUserId', (a, sdk) => sdk.setUserId(a, id)),
  logLogin: (method = 'google') => safe('login', (a, sdk) => sdk.logEvent(a, 'login', { method, client: 'web' })),
  logLogout: () => safe('logout', (a, sdk) => sdk.logEvent(a, 'logout', { client: 'web' })),
  /** Sự kiện khác của Bộ môn (duyệt đổi nhóm, đăng thông báo…): tên snake_case, tham số không có PII. */
  logEvent: (name: string, params?: Record<string, string | number>) => safe(name, (a, sdk) => sdk.logEvent(a, name, params)),
}
