import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '@/shared/api/apiClient'
import { adminNav } from '@/shared/layout/navigation'
import { PageHeaderProvider, usePageHeaderState } from '@/shared/layout/PageHeader'

const get = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args) },
}))

const { ClientErrorsPage } = await import('./ClientErrorsPage')

const LIST = '/api/admin/client-errors'
const SUMMARY = '/api/admin/client-errors/summary'

const fatal = {
  id: 'e1',
  kind: 'fatal',
  message: 'TestCrashError: crash thử từ Cài đặt',
  exceptionType: 'TestCrashError',
  stackTrace: '#0 FirebaseCrashReporter.testCrash (package:app/crash_reporter.dart:70:5)',
  platform: 'android',
  appVersion: '1.0.0+1',
  deviceModel: 'Google Pixel 8',
  osVersion: 'Android 15',
  userId: 'u1',
  role: 'Student',
  screen: '/sv/cai-dat',
  occurredAt: '2026-11-05T03:05:00Z',
  receivedAt: '2026-11-05T03:06:00Z',
}
const days = (fatalToday: number, nonFatalToday: number) =>
  ['10-30', '10-31', '11-01', '11-02', '11-03', '11-04', '11-05'].map((d, i) => ({
    date: `2026-${d}`,
    fatalCount: i === 6 ? fatalToday : 0,
    nonFatalCount: i === 6 ? nonFatalToday : 0,
  }))
const summary = {
  fatalCount: 3,
  nonFatalCount: 12,
  affectedUsers: 2,
  daily: days(3, 12),
  topErrors: [{ exceptionType: 'StateError', firstStackLine: '#0 main (package:app/main.dart:1:1)', count: 9, fatalCount: 1, lastOccurredAt: '2026-11-05T03:05:00Z' }],
}
const emptySummary = { fatalCount: 0, nonFatalCount: 0, affectedUsers: 0, daily: days(0, 0), topErrors: [] }
const page = (items: unknown[], extra = {}) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1, ...extra })

function mockApi(list: unknown, sum: unknown = summary) {
  get.mockImplementation(async (url: string) => ({ data: url === SUMMARY ? sum : list }))
}

/** Khung trang vẽ nút hành động ở header; test dựng lại phần đó. */
function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

function renderPage(path = '/admin/loi-ung-dung') {
  const router = createMemoryRouter([{ path: '/admin/loi-ung-dung', element: <ClientErrorsPage /> }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <HeaderAction />
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
  return router
}

const lastListParams = () => get.mock.calls.filter(([url]) => url === LIST).at(-1)?.[1].params

describe('W30 – Theo dõi lỗi ứng dụng', () => {
  beforeEach(() => get.mockReset())

  it('nằm trong nhóm Vận hành của sidebar', () => {
    expect(adminNav.find((item) => item.screen === 'W30')).toMatchObject({ to: '/admin/loi-ung-dung', group: 'Vận hành' })
  })

  it('hiện số liệu 7 ngày, lỗi lặp lại và bảng số theo ngày cho trình đọc màn hình', async () => {
    mockApi(page([fatal]))
    renderPage()

    expect(await screen.findByLabelText('12')).toBeInTheDocument()
    expect(screen.getByLabelText('3')).toBeInTheDocument()
    expect(screen.getByText('Người dùng bị ảnh hưởng')).toBeInTheDocument()
    expect(screen.getByText('StateError')).toBeInTheDocument()
    expect(screen.getByText('9 lần')).toBeInTheDocument()

    const chartTable = screen.getByRole('table', { name: 'Số lỗi theo ngày' })
    const today = within(chartTable).getByRole('row', { name: /05\/11/ })
    expect(today).toHaveTextContent('05/11312')
    expect(within(chartTable).getAllByRole('row')).toHaveLength(8)
  })

  it('bấm vào lỗi thì mở ngăn chi tiết có stack trace và thông tin máy', async () => {
    mockApi(page([fatal]))
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: /TestCrashError/ }))

    expect(screen.getByRole('heading', { name: 'Stack trace' })).toBeInTheDocument()
    expect(screen.getByText(fatal.stackTrace)).toBeInTheDocument()
    expect(screen.getByText('Google Pixel 8 · Android 15')).toBeInTheDocument()
  })

  it('báo cáo không có stack và chưa đăng nhập vẫn mở được chi tiết', async () => {
    mockApi(page([{ ...fatal, stackTrace: null, userId: null, deviceModel: null, osVersion: null, screen: null, role: null }]))
    renderPage()

    await userEvent.click(await screen.findByRole('button', { name: /TestCrashError/ }))

    expect(screen.getByText('Báo cáo này không có stack trace.')).toBeInTheDocument()
    expect(screen.getByText('Chưa đăng nhập')).toBeInTheDocument()
  })

  it('lọc theo loại: giữ trên URL và gửi cho API', async () => {
    mockApi(page([fatal]))
    const router = renderPage()

    await userEvent.selectOptions(await screen.findByLabelText('Loại'), 'fatal')
    expect(router.state.location.search).toBe('?kind=fatal')
    await waitFor(() => expect(lastListParams()).toMatchObject({ kind: 'fatal', page: 1 }))
  })

  it('đọc bộ lọc từ URL; "đến ngày" tính hết ngày đó', async () => {
    mockApi(page([fatal]))
    renderPage('/admin/loi-ung-dung?from=2026-11-01&to=2026-11-05&version=1.0.0%2B1')

    await screen.findByRole('button', { name: /TestCrashError/ })
    const params = lastListParams()
    expect(params.appVersion).toBe('1.0.0+1')
    expect(params.from).toBe(new Date('2026-11-01T00:00:00').toISOString())
    expect(params.to).toBe(new Date('2026-11-06T00:00:00').toISOString())
  })

  it('gõ phiên bản thì lọc sau khi dừng gõ', async () => {
    mockApi(page([fatal]))
    const router = renderPage()

    await userEvent.type(await screen.findByLabelText('Phiên bản app'), '1.0')

    await waitFor(() => expect(router.state.location.search).toBe('?version=1.0'))
    await waitFor(() => expect(lastListParams()).toMatchObject({ appVersion: '1.0' }))
  })

  it('phân trang: sang trang sau gọi đúng trang', async () => {
    mockApi(page([fatal], { totalCount: 45, totalPages: 3 }))
    const router = renderPage()

    expect(await screen.findByText('1–20 trên 45')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Trang sau' }))

    expect(router.state.location.search).toBe('?page=2')
    await waitFor(() => expect(lastListParams()).toMatchObject({ page: 2 }))
  })

  it('có nút mở Crashlytics ở tab mới', async () => {
    mockApi(page([]))
    renderPage()

    const link = await screen.findByRole('link', { name: /Mở Crashlytics/ })
    expect(link).toHaveAttribute('href', 'https://console.firebase.google.com/project/exe-team-matching/crashlytics')
    expect(link).toHaveAttribute('target', '_blank')
  })

  it('chưa có lỗi → trạng thái trống', async () => {
    mockApi(page([]), emptySummary)
    renderPage()

    expect(await screen.findByText('Chưa có lỗi nào')).toBeInTheDocument()
    expect(await screen.findByText('Chưa có lỗi nào trong 7 ngày.')).toBeInTheDocument()
  })

  it('có bộ lọc mà không khớp → không có kết quả, xóa bộ lọc được', async () => {
    mockApi(page([]))
    const router = renderPage('/admin/loi-ung-dung?kind=fatal')

    expect(await screen.findByText('Không tìm thấy kết quả')).toBeInTheDocument()
    await userEvent.click(screen.getAllByRole('button', { name: 'Xóa bộ lọc' })[0])

    expect(router.state.location.search).toBe('')
  })

  it('ngày kết thúc trước ngày bắt đầu → báo lỗi', async () => {
    mockApi(page([]))
    renderPage('/admin/loi-ung-dung?from=2026-11-05&to=2026-11-01')

    expect(await screen.findByRole('alert')).toHaveTextContent('"Đến ngày" phải sau "Từ ngày".')
  })

  it('lỗi API → báo lỗi và thử lại được', async () => {
    const failure = new ApiError({ status: 500, code: 'internal', title: 'Lỗi hệ thống' })
    get.mockRejectedValue(failure)
    renderPage()

    await waitFor(() => expect(screen.getAllByRole('button', { name: 'Thử lại' }).length).toBe(2))

    mockApi(page([fatal]))
    for (const retry of screen.getAllByRole('button', { name: 'Thử lại' })) await userEvent.click(retry)

    expect(await screen.findByRole('button', { name: /TestCrashError/ })).toBeInTheDocument()
    expect(await screen.findByLabelText('12')).toBeInTheDocument()
  })
})
