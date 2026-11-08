import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: {
    get: (...args: unknown[]) => get(...args),
    post: (...args: unknown[]) => post(...args),
    put: (...args: unknown[]) => put(...args),
  },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminCatalogRoutes } = await import('./routes')
const { PageHeaderProvider, usePageHeaderState } = await import('@/shared/layout/PageHeader')

async function type(element: HTMLElement, text: string) {
  await userEvent.click(element)
  await userEvent.paste(text)
}

function HeaderAction() {
  return <>{usePageHeaderState().action}</>
}

const data: Record<string, unknown[]> = {
  '/api/class-codes': [{ id: 1, code: 'SE1801', name: 'SE1801', description: null, isActive: true }],
  '/api/domains': [
    { id: 1, code: 'D01', name: 'AI & Data Innovation', description: 'Học máy, dữ liệu', isActive: true },
    { id: 2, code: 'D02', name: 'Robotics', description: null, isActive: false },
  ],
  '/api/specializations': [],
  '/api/majors': [{ id: 1, code: 'SE', name: 'Kỹ thuật phần mềm', description: null, isActive: true }],
}

function renderAt(path: string) {
  get.mockImplementation(async (url: string) => {
    if (url in data) return { data: data[url] }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  const router = createMemoryRouter([{ path: '/admin', children: adminCatalogRoutes }], { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <PageHeaderProvider>
        <HeaderAction />
        <RouterProvider router={router} />
      </PageHeaderProvider>
    </QueryClientProvider>,
  )
}

const drawer = () => screen.getByRole('dialog', { name: /Thêm|Sửa/ })

describe('W12–W14 – Danh mục (FE-18, FE-19, FE-20)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
    put.mockReset()
  })

  it('W12: lists class codes and adds one with the code as its name', async () => {
    post.mockResolvedValue({ data: { id: 2, code: 'AI1802', name: 'AI1802', isActive: true } })
    renderAt('/admin/danh-muc')
    expect(await screen.findByText('SE1801')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Thêm mã lớp' }))
    expect(within(drawer()).queryByRole('textbox', { name: 'Tên' })).not.toBeInTheDocument()
    await type(within(drawer()).getByRole('textbox', { name: /^Mã/ }), 'ai1802')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/api/class-codes', { code: 'AI1802', name: 'AI1802' }))
    expect(await screen.findByText('Đã thêm mã lớp')).toBeInTheDocument()
  })

  it('W13: shows domains with sub-fields and status, edits and stops one', async () => {
    put.mockResolvedValue({ data: {} })
    renderAt('/admin/danh-muc?tab=domains')
    const row = (await screen.findByText('AI & Data Innovation')).closest('tr')!
    expect(within(row).getByText('Học máy, dữ liệu')).toBeInTheDocument()
    expect(within(screen.getByText('Robotics').closest('tr')!).getByText('Ngừng dùng')).toBeInTheDocument()
    await userEvent.click(within(row).getByRole('button', { name: 'Sửa D01' }))
    await userEvent.click(within(drawer()).getByRole('switch', { name: 'Đang dùng' }))
    expect(within(drawer()).getByText(/dữ liệu đã gắn vẫn giữ nguyên/)).toBeInTheDocument()
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    await waitFor(() =>
      expect(put).toHaveBeenCalledWith('/api/domains/1', { code: 'D01', name: 'AI & Data Innovation', description: 'Học máy, dữ liệu', isActive: false }),
    )
  })

  it('W13: validates the D01–D18 code and the name before sending', async () => {
    renderAt('/admin/danh-muc?tab=domains')
    await screen.findByText('AI & Data Innovation')
    await userEvent.click(screen.getByRole('button', { name: 'Thêm domain' }))
    await type(within(drawer()).getByRole('textbox', { name: /^Mã/ }), 'D19')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    expect(within(drawer()).getByText('Mã không hợp lệ (D01 đến D18).')).toBeInTheDocument()
    expect(within(drawer()).getByText('Nhập tên.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('W14: empty specializations, duplicate code shown at the code field', async () => {
    post.mockRejectedValue(new ApiError({ status: 400, code: 'validation', title: 'Dữ liệu không hợp lệ', fieldErrors: { code: ['Mã SE đã tồn tại.'] } }))
    renderAt('/admin/danh-muc?tab=specializations')
    expect(await screen.findByText('Chưa có chuyên ngành')).toBeInTheDocument()
    await userEvent.click(screen.getAllByRole('button', { name: 'Thêm chuyên ngành' })[0])
    await type(within(drawer()).getByRole('textbox', { name: /^Mã/ }), 'SE')
    await type(within(drawer()).getByRole('textbox', { name: 'Tên' }), 'Kỹ thuật phần mềm')
    await userEvent.selectOptions(within(drawer()).getByRole('combobox'), '1')
    await userEvent.click(within(drawer()).getByRole('button', { name: 'Lưu' }))
    expect(await within(drawer()).findByText('Mã SE đã tồn tại.')).toBeInTheDocument()
    expect(post).toHaveBeenCalledWith('/api/specializations', { code: 'SE', name: 'Kỹ thuật phần mềm', majorId: 1 })
  })
})
