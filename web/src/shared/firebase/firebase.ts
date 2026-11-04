import type { FirebaseApp } from 'firebase/app'
import { env } from '@/shared/config/env'

/** Đủ cấu hình để bật Firebase (FE-46). Thiếu thì mọi service Firebase trên web là no-op. */
export const firebaseConfigured = Boolean(env.firebase.apiKey && env.firebase.appId && env.firebase.projectId)

let appPromise: Promise<FirebaseApp | null> | null = null

/** Khởi tạo Firebase một lần, nạp SDK khi cần (dynamic import) để trang không có cấu hình không tải SDK. */
export function getFirebaseApp(): Promise<FirebaseApp | null> {
  if (!firebaseConfigured) return Promise.resolve(null)
  appPromise ??= import('firebase/app')
    .then(({ initializeApp }) => initializeApp(env.firebase))
    .catch((error: unknown) => {
      console.warn('Firebase init failed', error)
      return null
    })
  return appPromise
}
