import { QueryClient } from '@tanstack/react-query'
import { ApiError } from './apiClient'

/**
 * Cache và trạng thái tải dùng chung (FE-03). Không thử lại lỗi 4xx (lỗi nghiệp vụ, không có quyền);
 * lỗi mạng / 5xx thử lại 2 lần.
 */
export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        retry: (failureCount, error) => {
          if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false
          return failureCount < 2
        },
      },
      mutations: { retry: false },
    },
  })
}
