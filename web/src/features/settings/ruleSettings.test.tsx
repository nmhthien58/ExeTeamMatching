import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { fieldRules, validateRules, type RuleValues } from './api'

const get = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), put: (...args: unknown[]) => put(...args) },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminRuleSettingsRoutes } = await import('./routes')
const { adminImportRoutes } = await import('@/features/imports/routes')
const { PageHeaderProvider } = await import('@/shared/layout/PageHeader')

const settings = {
  minTeamSize: 4, maxTeamSize: 6, minSpecializations: 2, invitationExpiryHours: 72, reminderHours: [72, 24],
  maxIdeasPerJoinRequest: 3, interestExtraSlots: 2, maxInterestsUnlimited: 10, maxLecturerPreferences: 3, maxFileSizeMb: 25, maxTeamRatio: 0.2,
}
const batches = [
  { id: 'b1', semesterId: 'fa', fileName: 'ds-exe101.xlsx', totalRows: 120, successRows: 118, failedRows: 2, status: 'COMPLETED', importedByName: 'Dev Admin', createdAt: '2026-10-30T03:00:00Z' },
  { id: 'b2', semesterId: 'fa', fileName: 'loi.xlsx', totalRows: 3, successRows: 0, failedRows: 3, status: 'FAILED', importedByName: null, createdAt: '2026-10-29T03:00:00Z' },
]

function renderAt(path: string, overrides: Record<string, unknown> = {}) {
  get.mockImplementation(async (url: string) => {
    if (url in overrides) {
      const value = overrides[url]
      if (value instanceof Error) throw value
      return { data: value }
    }
    if (url === '/api/rule-settings') return { data: settings }
    if (url === '/api/semesters') return { data: [{ id: 'fa', code: 'FA26', name: 'Fall 2026', isCurrent: true }] }
    if (url === '/api/imports') return { data: { items: batches, page: 1, pageSize: 10, totalCount: 2, totalPages: 1 } }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  const router = createMemoryRouter([{ path: '/admin', children: [...adminRuleSettingsRoutes, ...adminImportRoutes] }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
}

const input = (name: RegExp) => screen.getByRole('spinbutton', { name })

async function replace(element: HTMLElement, text: string) {
  await userEvent.clear(element)
  await userEvent.click(element)
  await userEvent.paste(text)
}

describe('W05 – Cấu hình quy tắc (FE-11)', () => {
  beforeEach(() => {
    get.mockReset()
    put.mockReset()
  })

  it('accepts a decimal team ratio and rejects one out of range', () => {
    const values = { ...Object.fromEntries(Object.entries(fieldRules).map(([k, r]) => [k, String(r.min)])), maxTeamSize: '6', reminderHours: '72' } as RuleValues
    expect(validateRules({ ...values, maxTeamRatio: '0.2' }).maxTeamRatio).toBeUndefined()
    expect(validateRules({ ...values, maxTeamRatio: '1.5' }).maxTeamRatio).toBe('Từ 0.01 đến 1.')
  })

  it('loads the current settings and saves a change with reminders sorted', async () => {
    put.mockImplementation(async (_url: string, body: unknown) => ({ data: body }))
    renderAt('/admin/cau-hinh')
    expect(await screen.findByDisplayValue('72, 24')).toBeInTheDocument()
    const save = screen.getByRole('button', { name: 'Lưu cấu hình' })
    expect(save).toBeDisabled()
    await replace(input(/^Sĩ số tối đa/), '7')
    await replace(screen.getByRole('textbox', { name: /Mốc nhắc/ }), '12, 48')
    await userEvent.click(save)
    await waitFor(() => expect(put).toHaveBeenCalledWith('/api/rule-settings', { ...settings, maxTeamSize: 7, reminderHours: [48, 12] }))
    expect(await screen.findByText('Đã lưu cấu hình')).toBeInTheDocument()
  })

  it('checks ranges and relations before sending', async () => {
    renderAt('/admin/cau-hinh')
    await screen.findByDisplayValue('72, 24')
    await replace(input(/^Sĩ số tối thiểu/), '5')
    await replace(input(/^Sĩ số tối đa/), '4')
    await replace(screen.getByRole('textbox', { name: /Mốc nhắc/ }), '24, 24')
    await replace(input(/^Dung lượng file/), '0')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu cấu hình' }))
    expect(screen.getByText('Phải lớn hơn hoặc bằng sĩ số tối thiểu.')).toBeInTheDocument()
    expect(screen.getByText(/không trùng nhau/)).toBeInTheDocument()
    expect(screen.getByText('Từ 1 đến 100.')).toBeInTheDocument()
    expect(screen.getByText('Còn 3 chỗ cần sửa')).toBeInTheDocument()
    expect(put).not.toHaveBeenCalled()
  })

  it('shows a server field error at its field', async () => {
    put.mockRejectedValue(new ApiError({ status: 400, code: 'validation', title: 'Dữ liệu không hợp lệ', fieldErrors: { minSpecializations: ['Số chuyên ngành tối thiểu không được vượt sĩ số tối đa.'] } }))
    renderAt('/admin/cau-hinh')
    await screen.findByDisplayValue('72, 24')
    await replace(input(/^Số chuyên ngành tối thiểu/), '3')
    await userEvent.click(screen.getByRole('button', { name: 'Lưu cấu hình' }))
    expect(await screen.findByText('Số chuyên ngành tối thiểu không được vượt sĩ số tối đa.')).toBeInTheDocument()
  })

  it('offers retry when the settings cannot be loaded', async () => {
    renderAt('/admin/cau-hinh', { '/api/rule-settings': new ApiError({ status: 500, code: 'x', title: 'Lỗi máy chủ' }) })
    expect(await screen.findByRole('button', { name: /Thử lại/ })).toBeInTheDocument()
  })
})

describe('W08 – Lịch sử import (FE-14)', () => {
  beforeEach(() => {
    get.mockReset()
  })

  it('lists import batches with who, when, success and failed rows', async () => {
    renderAt('/admin/import')
    const row = (await screen.findByText('ds-exe101.xlsx')).closest('tr')!
    expect(within(row).getByText('Dev Admin')).toBeInTheDocument()
    expect(within(row).getByText('118')).toBeInTheDocument()
    expect(within(row).getByText('Hoàn tất')).toBeInTheDocument()
    expect(within(screen.getByText('loi.xlsx').closest('tr')!).getByText('Thất bại')).toBeInTheDocument()
  })

  it('filters by semester and shows the empty state', async () => {
    renderAt('/admin/import', { '/api/imports': { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } })
    expect(await screen.findByText('Chưa có lần import nào')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByRole('combobox', { name: 'Lọc theo kỳ' }), 'fa')
    await waitFor(() => expect(get).toHaveBeenCalledWith('/api/imports', { params: { semesterId: 'fa', page: 1, pageSize: 10 } }))
  })
})
