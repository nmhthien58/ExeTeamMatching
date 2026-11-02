import { toApiError } from '@/shared/api/apiClient'

/**
 * Câu báo lỗi cho các mã nghiệp vụ của W19, W28 (team.yaml tag AdvisorMatching); mã khác dùng title/detail của ProblemDetails.
 * `fullCode` là mã "đã đủ số nhóm" của loại cố vấn.
 */
function advisorMatchingErrorMessage(error: unknown, fullCode: string, label: string, noun: string): { title: string; detail?: string } {
  const e = toApiError(error)
  switch (e.code) {
    case 'run-not-preview':
      return { title: 'Kết quả xem trước không còn dùng được', detail: 'Lần ghép này đã được chốt hoặc đã bỏ. Bấm "Ghép sớm" để xem kết quả mới.' }
    case fullCode:
      return { title: `${label} đã đủ số nhóm`, detail: e.detail ?? `Chọn ${noun} khác còn chỗ.` }
    case 'matching-unavailable':
      return { title: 'Chưa tính được kết quả ghép', detail: 'Dịch vụ ghép đang không phản hồi. Thử lại sau ít phút.' }
    case 'academic-unavailable':
      return { title: `Chưa lấy được danh sách ${noun}`, detail: 'Dịch vụ học vụ đang không phản hồi. Thử lại sau ít phút.' }
    default:
      if (e.isNetwork) return { title: 'Mất kết nối. Kiểm tra mạng rồi thử lại.' }
      return { title: e.title, detail: e.detail }
  }
}

export const matchingErrorMessage = (error: unknown) => advisorMatchingErrorMessage(error, 'lecturer-full', 'Giảng viên', 'giảng viên')

export const mentorMatchingErrorMessage = (error: unknown) => advisorMatchingErrorMessage(error, 'mentor-full', 'Mentor', 'mentor')
