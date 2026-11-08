import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'

const get = vi.fn()
const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), post: (...args: unknown[]) => post(...args) },
}))

const { ApiError } = await import('@/shared/api/apiClient')
const { adminImportRoutes } = await import('./routes')
const { fileProblem } = await import('./api')

const semesters = [
  { id: 'old', code: 'SU26', name: 'Summer 2026', isCurrent: false },
  { id: 'fa', code: 'FA26', name: 'Fall 2026', isCurrent: true },
]
const validRow = { rowNumber: 2, studentCode: 'SE184118', fullName: 'Nguyễn Văn A', email: 'a@example.com', specializationCode: 'SE', classCode: 'EXE101SE01', phone: null }
const errorRow = {
  ...validRow,
  rowNumber: 3,
  studentCode: 'SE12',
  fullName: 'Sai MSSV',
  errors: [{ field: 'studentCode', message: 'MSSV phải gồm 2 chữ cái và 6 chữ số.' }],
}
const preview = { previewId: 'p1', fileName: 'ds.xlsx', totalRows: 2, validRows: [validRow], errorRows: [errorRow] }
const batch = { id: 'b1', semesterId: 'fa', fileName: 'ds.xlsx', totalRows: 2, successRows: 1, failedRows: 1, status: 'COMPLETED', importedByName: 'Admin', createdAt: '2026-10-30T03:00:00Z' }
const xlsx = new File(['x'], 'ds.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })

function renderPage(list: unknown[] = semesters) {
  get.mockImplementation(async (url: string) => {
    if (url === '/api/semesters') return { data: list }
    if (url === '/api/imports') return { data: { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 } }
    throw new Error(`GET ${url} chưa giả lập`)
  })
  const router = createMemoryRouter([{ path: '/admin', children: adminImportRoutes }], { initialEntries: ['/admin/import'] })
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

const fileInput = (name: RegExp) => screen.getByLabelText(name, { selector: 'input' })

async function uploadAndPreview(user: ReturnType<typeof userEvent.setup>) {
  await user.upload(fileInput(/Chọn file danh sách sinh viên/), xlsx)
  await user.click(screen.getByRole('button', { name: 'Tải lên và xem trước' }))
}

describe('W06, W07 – import sinh viên (FE-12, FE-13)', () => {
  beforeEach(() => {
    get.mockReset()
    post.mockReset()
  })

  it('W06: mặc định học kỳ hiện hành, chưa chọn file thì chưa tải lên được, gửi đúng form', async () => {
    const user = userEvent.setup()
    post.mockResolvedValue({ data: preview })
    renderPage()

    expect(await screen.findByLabelText('Học kỳ')).toHaveValue('fa')
    expect(screen.getByRole('button', { name: 'Tải lên và xem trước' })).toBeDisabled()

    await uploadAndPreview(user)
    const [url, form, config] = post.mock.calls[0] as [string, FormData, { headers: Record<string, string> }]
    expect(url).toBe('/api/imports/students/preview')
    expect(form.get('semesterId')).toBe('fa')
    expect(form.get('courseCode')).toBe('EXE101')
    expect((form.get('file') as File).name).toBe('ds.xlsx')
    expect(config.headers['Content-Type']).toBe('multipart/form-data')
  })

  it('W06: file sai định dạng báo lỗi ngay, không gọi API', async () => {
    renderPage()
    await screen.findByLabelText('Học kỳ')
    fireEvent.change(fileInput(/Chọn file danh sách sinh viên/), { target: { files: [new File(['x'], 'ds.csv')] } })

    expect(await screen.findByRole('alert')).toHaveTextContent('Chỉ nhận file Excel .xlsx.')
    expect(screen.getByRole('button', { name: 'Tải lên và xem trước' })).toBeDisabled()
    expect(fileProblem(new File([new Uint8Array(6 * 1024 * 1024)], 'to.xlsx'))).toBe('File tối đa 5 MB.')
  })

  it('W06: kéo thả file vào ô chọn', async () => {
    renderPage()
    await screen.findByLabelText('Học kỳ')
    fireEvent.drop(screen.getByText('Chọn file danh sách sinh viên'), { dataTransfer: { files: [xlsx] } })
    expect(await screen.findByText('ds.xlsx')).toBeInTheDocument()
  })

  it('W06: lỗi file từ server hiện ngay tại ô chọn file', async () => {
    const user = userEvent.setup()
    post.mockRejectedValue(new ApiError({ status: 400, code: 'validation', title: 'Dữ liệu không hợp lệ', fieldErrors: { file: ['Thiếu cột: Chuyên ngành.'] } }))
    renderPage()
    await screen.findByLabelText('Học kỳ')
    await uploadAndPreview(user)
    expect(await screen.findByRole('alert')).toHaveTextContent('Thiếu cột: Chuyên ngành.')
  })

  it('W06: chưa có học kỳ thì dẫn tới tạo học kỳ', async () => {
    renderPage([])
    expect(await screen.findByText('Chưa có học kỳ')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Tạo học kỳ' })).toHaveAttribute('href', '/admin/hoc-ky')
  })

  it('W06: không tải được học kỳ thì có Thử lại', async () => {
    const user = userEvent.setup()
    get.mockRejectedValueOnce(new ApiError({ status: 0, code: 'network', title: 'Không kết nối được máy chủ' }))
    renderPage()
    await user.click(await screen.findByRole('button', { name: 'Thử lại' }))
    expect(await screen.findByLabelText('Học kỳ')).toBeInTheDocument()
  })

  it('W07: hiện dòng lỗi trước, xác nhận bỏ qua dòng lỗi rồi ra tổng kết', async () => {
    const user = userEvent.setup()
    post.mockImplementation(async (url: string) => (url.endsWith('/confirm') ? { data: batch } : { data: preview }))
    renderPage()
    await screen.findByLabelText('Học kỳ')
    await uploadAndPreview(user)

    expect(await screen.findByText('1 dòng lỗi sẽ bị bỏ qua')).toBeInTheDocument()
    expect(within(screen.getByRole('table')).getByText(/MSSV: MSSV phải gồm/)).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: 'Dòng hợp lệ (1)' }))
    expect(within(screen.getByRole('table')).getByText('SE184118')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Xác nhận import 1 sinh viên' }))
    expect(post).toHaveBeenLastCalledWith('/api/imports/students/p1/confirm')
    expect(await screen.findByText('Đã import 1 sinh viên')).toBeInTheDocument()
    expect(screen.getByText(/2 dòng, 1 thành công, 1 bị bỏ qua/)).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Import file khác' }))
    expect(await screen.findByLabelText('Học kỳ')).toBeInTheDocument()
  })

  it('W07: không có dòng hợp lệ thì khóa xác nhận; không lỗi thì mở tab hợp lệ', async () => {
    const user = userEvent.setup()
    post.mockResolvedValueOnce({ data: { ...preview, validRows: [] } })
    renderPage()
    await screen.findByLabelText('Học kỳ')
    await uploadAndPreview(user)
    expect(await screen.findByText('Không có dòng hợp lệ nào')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Xác nhận import 0 sinh viên' })).toBeDisabled()

    await user.click(screen.getByRole('button', { name: 'Chọn file khác' }))
    post.mockResolvedValueOnce({ data: { ...preview, errorRows: [] } })
    await screen.findByLabelText('Học kỳ')
    await uploadAndPreview(user)
    expect(await screen.findByText('Tất cả dòng đều hợp lệ')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Dòng hợp lệ (1)' })).toHaveAttribute('aria-selected', 'true')
  })

  it('W07: bản xem trước hết hạn thì mời tải lại file', async () => {
    const user = userEvent.setup()
    post.mockImplementation(async (url: string) => {
      if (url.endsWith('/confirm')) throw new ApiError({ status: 404, code: 'not-found', title: 'Không tìm thấy' })
      return { data: preview }
    })
    renderPage()
    await screen.findByLabelText('Học kỳ')
    await uploadAndPreview(user)
    await user.click(await screen.findByRole('button', { name: 'Xác nhận import 1 sinh viên' }))

    expect(await screen.findByText('Bản xem trước đã hết hạn')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Xác nhận import 1 sinh viên' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Tải lại file' }))
    expect(await screen.findByLabelText('Học kỳ')).toBeInTheDocument()
  })

  it('W06 EXE201: phải kiểm tra file mốc, có lỗi thì khóa import, hợp lệ mới import được', async () => {
    const user = userEvent.setup()
    const ok = { teamCount: 2, studentCount: 10, linkedProjectCount: 1, unlinkedTeamCount: 1, errors: [] }
    post.mockResolvedValueOnce({ data: { ...ok, errors: [{ row: 4, message: 'Không tìm thấy sinh viên.' }] } })
    renderPage()
    expect(screen.queryByText('File nhóm EXE101 làm mốc')).not.toBeInTheDocument()
    await user.selectOptions(await screen.findByLabelText('Môn'), 'EXE201')

    await user.upload(fileInput(/Chọn file nhóm EXE101/), xlsx)
    expect(screen.getByRole('button', { name: 'Import mốc' })).toBeDisabled()
    await user.click(screen.getByRole('button', { name: 'Kiểm tra file' }))
    expect(await screen.findByText(/Dòng 4: Không tìm thấy sinh viên/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Import mốc' })).toBeDisabled()
    const [url, form] = post.mock.calls[0] as [string, FormData]
    expect(url).toBe('/api/teams/baseline-import')
    expect(form.get('dryRun')).toBe('true')

    post.mockResolvedValueOnce({ data: ok })
    await user.click(screen.getByRole('button', { name: 'Kiểm tra file' }))
    expect(await screen.findByText('File hợp lệ, chưa tạo nhóm nào')).toBeInTheDocument()

    post.mockResolvedValueOnce({ data: ok })
    await user.click(screen.getByRole('button', { name: 'Import mốc' }))
    expect(await screen.findByText('Đã import mốc EXE101')).toBeInTheDocument()
    expect((post.mock.calls[2] as [string, FormData])[1].get('dryRun')).toBe('false')
    expect(screen.getByRole('link', { name: 'Gắn đề tài cho nhóm chưa khớp' })).toHaveAttribute('href', '/admin/de-tai')
  })

  it('W06 EXE201: tự động kế thừa nhóm từ EXE101 kỳ trước và xác nhận tạo nhóm (FE-56)', async () => {
    const user = userEvent.setup()
    const autoInheritData = {
      inheritedTeamCount: 3,
      inheritedStudentCount: 12,
      freeStudentCount: 5,
      needsReviewTeamCount: 1,
      teams: [
        {
          teamId: 't1',
          teamName: 'Nhóm Rồng Vàng',
          oldTeamId: 'ot1',
          projectId: 'p1',
          projectName: 'EduPlatform',
          leaderStudentCode: 'SE160001',
          leaderName: 'Nguyễn Văn A',
          memberCount: 4,
          members: [
            { studentId: 's1', studentCode: 'SE160001', fullName: 'Nguyễn Văn A', specializationId: 1, isLeader: true },
          ],
          missingOldMemberStudentCodes: [],
          status: 'DRAFT',
        },
        {
          teamId: 't2',
          teamName: 'Nhóm Chiến Binh',
          oldTeamId: 'ot2',
          projectId: null,
          projectName: null,
          leaderStudentCode: 'SE160002',
          leaderName: 'Trần Thị B',
          memberCount: 2,
          members: [
            { studentId: 's2', studentCode: 'SE160002', fullName: 'Trần Thị B', specializationId: 1, isLeader: true },
          ],
          missingOldMemberStudentCodes: ['SE160003', 'SE160004'],
          status: 'NEEDS_REVIEW',
        },
      ],
    }

    renderPage()
    await user.selectOptions(await screen.findByLabelText('Môn'), 'EXE201')

    post.mockResolvedValueOnce({ data: autoInheritData })
    await user.click(screen.getByRole('button', { name: 'Tự động kế thừa từ EXE101' }))

    expect(post).toHaveBeenLastCalledWith('/api/teams/auto-inherit', { semesterId: 'fa', dryRun: true })
    expect(await screen.findByText('Xem trước kết quả kế thừa nhóm')).toBeInTheDocument()
    expect(screen.getByText('Nhóm Rồng Vàng')).toBeInTheDocument()
    expect(screen.getByText('Nhóm Chiến Binh')).toBeInTheDocument()
    expect(screen.getByText(/Thiếu 2: SE160003, SE160004/)).toBeInTheDocument()

    post.mockResolvedValueOnce({ data: autoInheritData })
    await user.click(screen.getByRole('button', { name: 'Xác nhận tạo nhóm kế thừa' }))

    expect(post).toHaveBeenLastCalledWith('/api/teams/auto-inherit', { semesterId: 'fa', dryRun: false })
    expect(await screen.findByText('Đã tạo nhóm kế thừa thành công!')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Chuyển tới Quản lý nhóm (W15)' })).toBeInTheDocument()
  })
})
