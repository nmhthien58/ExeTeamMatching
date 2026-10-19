import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import { tokenStore, type Role, type TokenResponse, type UserInfo } from '@/shared/api/tokenStore'

interface AuthContextValue {
  user: UserInfo | null
  /** Đăng nhập bằng Google ID token (W01, A03). Ném ApiError (not-in-list, account-locked…). */
  loginWithGoogle: (idToken: string) => Promise<UserInfo>
  logout: () => Promise<void>
  hasRole: (role: Role) => boolean
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(() => tokenStore.getUser())
  const queryClient = useQueryClient()

  useEffect(() => tokenStore.subscribe(setUser), [])

  const loginWithGoogle = useCallback(async (idToken: string) => {
    const { data } = await api.post<TokenResponse>('/api/auth/google', { idToken, client: 'web' })
    tokenStore.save(data)
    return data.user
  }, [])

  const logout = useCallback(async () => {
    const refreshToken = tokenStore.getRefreshToken()
    try {
      if (refreshToken) await api.post('/api/auth/logout', { refreshToken })
    } catch {
      /* đăng xuất phía máy vẫn tiếp tục */
    }
    tokenStore.clear()
    queryClient.clear()
  }, [queryClient])

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      loginWithGoogle,
      logout,
      hasRole: (role) => user?.roles.includes(role) ?? false,
    }),
    [user, loginWithGoogle, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth phải nằm trong AuthProvider')
  return ctx
}

/**
 * Khung giao diện theo vai trò (PROJECT_CONTEXT §2, UserInfo trong identity.yaml):
 * Admin → web quản trị; Student; Lecturer (gồm nhóm mình làm mentor); chỉ Mentor → khung Mentor.
 */
export type Shell = 'admin' | 'student' | 'lecturer' | 'mentor'

export function shellOf(user: UserInfo): Shell {
  if (user.roles.includes('Admin')) return 'admin'
  if (user.roles.includes('Lecturer')) return 'lecturer'
  if (user.roles.includes('Mentor')) return 'mentor'
  return 'student'
}

export const shellHome: Record<Shell, string> = {
  admin: '/admin',
  student: '/sv',
  lecturer: '/gv',
  mentor: '/mentor',
}
