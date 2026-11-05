// Biến môi trường của web (khai báo trong .env, xem .env.example).

/** Cấu hình Firebase: biến môi trường nếu có, không thì mặc định của project; test (vitest) luôn tắt. */
function firebaseValue(name: string, fallback: string): string {
  if (import.meta.env.MODE === 'test' || import.meta.env.VITE_FIREBASE_API_KEY === 'off') return ''
  const value = (import.meta.env as Record<string, string | undefined>)[name]
  return value ? value : fallback
}
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080',
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
  /**
   * Web app "EXE Team Matching Web" của project Firebase `exe-team-matching` (FE-46). Không phải bí mật – tương
   * đương `mobile/lib/firebase_options.dart`; quyền do App Check / Security Rules quyết định. Ghi đè bằng
   * `VITE_FIREBASE_*`; đặt `VITE_FIREBASE_API_KEY=off` để tắt Analytics / Remote Config (ví dụ khi chạy test).
   */
  firebase: {
    apiKey: firebaseValue('VITE_FIREBASE_API_KEY', 'AIzaSyDWN-tdzKEMZKIU7eEvJWI7S-Yw23L6jd8'),
    authDomain: firebaseValue('VITE_FIREBASE_AUTH_DOMAIN', 'exe-team-matching.firebaseapp.com'),
    projectId: firebaseValue('VITE_FIREBASE_PROJECT_ID', 'exe-team-matching'),
    appId: firebaseValue('VITE_FIREBASE_APP_ID', '1:221888974127:web:ac74927c92ce87183165da'),
    measurementId: firebaseValue('VITE_FIREBASE_MEASUREMENT_ID', 'G-H2J5SWXS87'),
  },
}
