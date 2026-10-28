import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AnnouncementPage } from './AnnouncementPage'

const post = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { post: (...args: unknown[]) => post(...args) },
}))

function renderPage() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <AnnouncementPage />
    </QueryClientProvider>,
  )
}

describe('W23 – Gửi thông báo chung (FE-29)', () => {
  beforeEach(() => {
    post.mockReset()
  })

  it('hiển thị đầy đủ form với các giá trị mặc định', () => {
    renderPage()

    expect(document.title).toContain('Gửi thông báo chung')
    expect(screen.getByLabelText('Đối tượng nhận')).toHaveValue('EVERYONE')
    expect(screen.getByLabelText('Khóa học / môn')).toHaveValue('')
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('')
    expect(screen.getByLabelText('Nội dung')).toHaveValue('')
    expect(screen.getByRole('checkbox', { name: /gửi email/i })).toBeChecked()
    expect(screen.getByRole('button', { name: /gửi thông báo/i })).toBeInTheDocument()
  })

  it('báo lỗi nếu chưa nhập tiêu đề hoặc nội dung', async () => {
    renderPage()
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: /gửi thông báo/i }))
    expect(await screen.findByText('Vui lòng nhập tiêu đề thông báo.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()

    await user.type(screen.getByLabelText('Tiêu đề'), 'Thông báo mở đợt đăng ký')
    await user.click(screen.getByRole('button', { name: /gửi thông báo/i }))
    expect(await screen.findByText('Vui lòng nhập nội dung thông báo.')).toBeInTheDocument()
    expect(post).not.toHaveBeenCalled()
  })

  it('gửi thông báo thành công và hiển thị kết quả số lượng người nhận', async () => {
    post.mockResolvedValueOnce({ data: { recipientCount: 42 } })
    renderPage()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Đối tượng nhận'), 'ALL_STUDENTS')
    await user.selectOptions(screen.getByLabelText('Khóa học / môn'), 'EXE101')
    await user.type(screen.getByLabelText('Tiêu đề'), 'Hạn chốt đăng ký nhóm EXE101')
    await user.type(screen.getByLabelText('Nội dung'), 'Các bạn sinh viên chú ý hoàn thành nhóm trước 23:59 ngày 30/10.')

    await user.click(screen.getByRole('button', { name: /gửi thông báo/i }))

    await waitFor(() => {
      expect(post).toHaveBeenCalledWith('/api/announcements', {
        audience: 'ALL_STUDENTS',
        courseCode: 'EXE101',
        title: 'Hạn chốt đăng ký nhóm EXE101',
        content: 'Các bạn sinh viên chú ý hoàn thành nhóm trước 23:59 ngày 30/10.',
        sendEmail: true,
      })
    })

    expect(await screen.findByText('Đã tiếp nhận thông báo')).toBeInTheDocument()
    expect(screen.getByText('42')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Soạn thông báo khác' })).toBeInTheDocument()

    // Bấm soạn thông báo khác để quay lại form trống
    await user.click(screen.getByRole('button', { name: 'Soạn thông báo khác' }))
    expect(screen.getByLabelText('Tiêu đề')).toHaveValue('')
    expect(screen.getByLabelText('Nội dung')).toHaveValue('')
  })

  it('hiển thị banner lỗi khi API trả về lỗi', async () => {
    post.mockRejectedValueOnce({ title: 'Không có quyền thực hiện thao tác' })
    renderPage()
    const user = userEvent.setup()

    await user.type(screen.getByLabelText('Tiêu đề'), 'Tiêu đề thử nghiệm')
    await user.type(screen.getByLabelText('Nội dung'), 'Nội dung thử nghiệm')
    await user.click(screen.getByRole('button', { name: /gửi thông báo/i }))

    expect(await screen.findByText(/lỗi khi gửi thông báo/i)).toBeInTheDocument()
  })
})
