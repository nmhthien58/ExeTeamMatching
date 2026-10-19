// Biến môi trường của web (khai báo trong .env, xem .env.example).
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:8080',
  googleClientId: import.meta.env.VITE_GOOGLE_CLIENT_ID ?? '',
}
