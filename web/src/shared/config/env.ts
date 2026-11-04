// Biến môi trường của web (khai báo trong .env, xem .env.example).
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080',
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
  /** Web app của project Firebase `exe-team-matching` (FE-46). Để trống thì Analytics / Remote Config tắt. */
  firebase: {
    apiKey: import.meta.env.VITE_FIREBASE_API_KEY ?? '',
    authDomain: import.meta.env.VITE_FIREBASE_AUTH_DOMAIN ?? '',
    projectId: import.meta.env.VITE_FIREBASE_PROJECT_ID ?? '',
    appId: import.meta.env.VITE_FIREBASE_APP_ID ?? '',
    measurementId: import.meta.env.VITE_FIREBASE_MEASUREMENT_ID ?? '',
  },
}
