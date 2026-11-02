import { toApiError } from '@/shared/api/apiClient'

/** Câu báo lỗi cho các mã nghiệp vụ của W19 (team.yaml tag AdvisorMatching); mã khác dùng title/detail của ProblemDetails. */
export function matchingErrorMessage(error: unknown): { title: string; detail?: string } {
  const e = toApiError(error)
  switch (e.code) {
    case 'run-not-preview':
      return { title: 'Kết quả xem trước không còn dùng được', detail: 'Lần ghép này đã được chốt hoặc đã bỏ. Bấm "Ghép sớm" để xem kết quả mới.' }
    case 'lecturer-full':
      return { title: 'Giảng viên đã đủ số nhóm', detail: e.detail ?? 'Chọn giảng viên khác còn chỗ.' }
    case 'matching-unavailable':
      return { title: 'Chưa tính được kết quả ghép', detail: 'Dịch vụ ghép đang không phản hồi. Thử lại sau ít phút.' }
    case 'academic-unavailable':
      return { title: 'Chưa lấy được danh sách giảng viên', detail: 'Dịch vụ học vụ đang không phản hồi. Thử lại sau ít phút.' }
    default:
      if (e.isNetwork) return { title: 'Mất kết nối. Kiểm tra mạng rồi thử lại.' }
      return { title: e.title, detail: e.detail }
  }
}
