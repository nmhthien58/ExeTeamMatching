import axios, { AxiosError, type AxiosInstance, type InternalAxiosRequestConfig } from 'axios'
import { env } from '@/shared/config/env'
import { tokenStore, type TokenResponse } from './tokenStore'

/**
 * Lỗi API thống nhất từ ProblemDetails (docs/contracts/README.md). Màn hình đọc `code` để hiện câu
 * phù hợp, `fieldErrors` để báo lỗi tại từng ô; `traceId` hiện ở banner lỗi để báo lại khi cần.
 */
export class ApiError extends Error {
  readonly status: number
  /** Phần cuối của `type`, ví dụ `round-closed`, `validation`, `not-workspace-member`. */
  readonly code: string
  readonly title: string
  readonly detail?: string
  readonly traceId?: string
  readonly fieldErrors: Record<string, string[]>

  constructor(init: {
    status: number
    code: string
    title: string
    detail?: string
    traceId?: string
    fieldErrors?: Record<string, string[]>
  }) {
    super(init.detail ?? init.title)
    this.name = 'ApiError'
    this.status = init.status
    this.code = init.code
    this.title = init.title
    this.detail = init.detail
    this.traceId = init.traceId
    this.fieldErrors = init.fieldErrors ?? {}
  }

  /** Mất mạng / không tới được Gateway. */
  get isNetwork() {
    return this.status === 0
  }
}

interface ProblemBody {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]>
}

export function toApiError(error: unknown): ApiError {
  if (error instanceof ApiError) return error
  if (axios.isAxiosError(error)) {
    if (!error.response) {
      return new ApiError({ status: 0, code: 'network', title: 'Không kết nối được máy chủ' })
    }
    const body = (error.response.data ?? {}) as ProblemBody
    const code = body.type?.split('/').pop() ?? `http-${error.response.status}`
    return new ApiError({
      status: error.response.status,
      code,
      title: body.title ?? 'Đã có lỗi xảy ra',
      detail: body.detail,
      traceId: body.traceId ?? error.response.headers['x-correlation-id'],
      fieldErrors: body.errors,
    })
  }
  return new ApiError({ status: -1, code: 'unknown', title: 'Đã có lỗi xảy ra' })
}

type RetriableConfig = InternalAxiosRequestConfig & { _retried?: boolean }

/**
 * Tạo axios gắn JWT, tự refresh khi 401 (một lần refresh cho mọi request đang chờ), đổi lỗi thành {@link ApiError}.
 * Màn hình không gọi fetch/axios trực tiếp mà dùng `api` qua React Query (AGENTS.md mục 4).
 */
export function createApiClient(baseURL: string = env.apiBaseUrl): AxiosInstance {
  const client = axios.create({ baseURL, headers: { 'Content-Type': 'application/json' } })
  let refreshing: Promise<string | null> | null = null

  const refresh = async (): Promise<string | null> => {
    const refreshToken = tokenStore.getRefreshToken()
    if (!refreshToken) return null
    try {
      // Gọi bằng axios gốc để không đi lại vào interceptor này.
      const { data } = await axios.post<TokenResponse>(`${baseURL}/api/auth/refresh`, { refreshToken })
      tokenStore.save(data)
      return data.accessToken
    } catch {
      tokenStore.clear()
      return null
    }
  }

  client.interceptors.request.use((config) => {
    const token = tokenStore.getAccessToken()
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
  })

  client.interceptors.response.use(
    (response) => response,
    async (error: AxiosError) => {
      const config = error.config as RetriableConfig | undefined
      const isAuthCall = config?.url?.includes('/api/auth/')
      if (error.response?.status === 401 && config && !config._retried && !isAuthCall) {
        config._retried = true
        refreshing ??= refresh().finally(() => {
          refreshing = null
        })
        const newToken = await refreshing
        if (newToken) {
          config.headers.Authorization = `Bearer ${newToken}`
          return client(config)
        }
      }
      throw toApiError(error)
    },
  )

  return client
}

export const api = createApiClient()
