import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { expect, it, vi } from 'vitest'

// File riêng, API giả là hàm thường không bao giờ trả lời: để chung file với mock vi.fn thì lần chạy vitest không kết thúc.
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: () => new Promise(() => {}) },
}))

const { ClientErrorsPage } = await import('./ClientErrorsPage')

it('W30 đang tải → skeleton ở số liệu và bảng', () => {
  render(
    <QueryClientProvider client={new QueryClient()}>
      <MemoryRouter>
        <ClientErrorsPage />
      </MemoryRouter>
    </QueryClientProvider>,
  )

  expect(screen.getByRole('status', { name: 'Đang tải số liệu' })).toBeInTheDocument()
  expect(screen.getByRole('status', { name: 'Đang tải' })).toBeInTheDocument()
})
