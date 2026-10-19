export type Role = 'Student' | 'Lecturer' | 'Mentor' | 'Admin'

export interface UserInfo {
  id: string
  email: string
  fullName: string
  roles: Role[]
  studentId?: string | null
  lecturerId?: string | null
  mentorId?: string | null
  isFirstLogin?: boolean
  profileCompleted?: boolean
}

export interface TokenResponse {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: UserInfo
}

const REFRESH_KEY = 'exe.refreshToken'
const USER_KEY = 'exe.user'

type Listener = (user: UserInfo | null) => void

/**
 * Lưu phiên đăng nhập. Access token (15 phút) chỉ giữ trong bộ nhớ; refresh token và thông tin người dùng
 * để trong sessionStorage – đóng tab là phải đăng nhập lại, không tồn tại lâu trên máy dùng chung.
 */
class TokenStore {
  private accessToken: string | null = null
  private listeners = new Set<Listener>()

  getAccessToken() {
    return this.accessToken
  }

  getRefreshToken() {
    return safeGet(REFRESH_KEY)
  }

  getUser(): UserInfo | null {
    const raw = safeGet(USER_KEY)
    if (!raw) return null
    try {
      return JSON.parse(raw) as UserInfo
    } catch {
      return null
    }
  }

  save(tokens: TokenResponse) {
    this.accessToken = tokens.accessToken
    safeSet(REFRESH_KEY, tokens.refreshToken)
    safeSet(USER_KEY, JSON.stringify(tokens.user))
    this.emit(tokens.user)
  }

  clear() {
    this.accessToken = null
    safeRemove(REFRESH_KEY)
    safeRemove(USER_KEY)
    this.emit(null)
  }

  subscribe(listener: Listener) {
    this.listeners.add(listener)
    return () => {
      this.listeners.delete(listener)
    }
  }

  private emit(user: UserInfo | null) {
    this.listeners.forEach((l) => l(user))
  }
}

function safeGet(key: string) {
  try {
    return sessionStorage.getItem(key)
  } catch {
    return null
  }
}
function safeSet(key: string, value: string) {
  try {
    sessionStorage.setItem(key, value)
  } catch {
    /* trình duyệt chặn storage: phiên chỉ sống trong bộ nhớ */
  }
}
function safeRemove(key: string) {
  try {
    sessionStorage.removeItem(key)
  } catch {
    /* bỏ qua */
  }
}

export const tokenStore = new TokenStore()
