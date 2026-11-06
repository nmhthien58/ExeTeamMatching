import { toApiError } from '@/shared/api/apiClient'

// Quy tắc form giảng viên (W11), mentor (W27): cùng quy tắc với validator của Academic để báo ngay khi chưa gửi.

// Phần tên miền tách theo dấu chấm (không lồng `+` chồng nhau) để regex chạy tuyến tính (SonarCloud S5852).
export const EMAIL_PATTERN = /^[^@\s]+@[^@\s.]+(?:\.[^@\s.]+)+$/
export const MAX_DOMAINS = 18

/** Số nguyên trong [min, max]; chuỗi rỗng hợp lệ khi `optional`. */
export function isWholeIn(value: string, min: number, max: number, optional = false) {
  if (value.trim() === '') return optional
  const n = Number(value)
  return Number.isInteger(n) && n >= min && n <= max
}

/** Lỗi ô từ kiểm tra tại chỗ, rồi lỗi theo field của API (camelCase như contract). */
export function useFieldErrors(local: Record<string, string>, error: unknown, codeToField: Record<string, string>) {
  const api = error ? toApiError(error) : null
  const byCode = api && codeToField[api.code] ? { [codeToField[api.code]]: api.detail ?? api.title } : {}
  const fieldError = (key: string) => local[key] ?? byCode[key] ?? api?.fieldErrors[key]?.[0]
  const banner = api && Object.keys(api.fieldErrors).length === 0 && !codeToField[api.code] ? api : null
  return { fieldError, banner }
}

export interface LecturerValues {
  fullName: string
  email: string
  phone: string
  domainIds: number[]
  maxTeamsExe101: string
  maxTeamsExe201: string
  isActive: boolean
}

/** Cùng quy tắc với `LecturerInputValidator` để báo ngay khi chưa gửi. */
export function validateLecturer(v: LecturerValues) {
  const errors: Record<string, string> = {}
  if (!v.fullName.trim()) errors.fullName = 'Nhập họ tên.'
  else if (v.fullName.trim().length > 150) errors.fullName = 'Tối đa 150 ký tự.'
  if (!EMAIL_PATTERN.test(v.email.trim())) errors.email = 'Email không đúng định dạng.'
  if (v.phone.trim() && !/^\+?[0-9]{9,14}$/.test(v.phone.replace(/[\s.\-()]/g, ''))) errors.phone = 'Gồm 9–14 chữ số, có thể bắt đầu bằng +.'
  if (!isWholeIn(v.maxTeamsExe101, 0, 200, true)) errors.maxTeamsExe101 = 'Từ 0 đến 200, để trống là không giới hạn.'
  if (!isWholeIn(v.maxTeamsExe201, 0, 200, true)) errors.maxTeamsExe201 = 'Từ 0 đến 200, để trống là không giới hạn.'
  if (v.domainIds.length > MAX_DOMAINS) errors.domainIds = `Tối đa ${MAX_DOMAINS} domain.`
  return errors
}

export interface MentorValues {
  fullName: string
  email: string
  organization: string
  isInternal: boolean
  lecturerId: string
  domainIds: number[]
  maxTeams: string
  isActive: boolean
}

/** Số nhóm tối đa mặc định khi tạo mentor (task BE-66). */
export const DEFAULT_MAX_TEAMS = '3'

/** Cùng quy tắc với `MentorInputValidator` và `MentorRules.RequireEmail`: mentor gắn giảng viên không cần email. */
export function validateMentor(v: MentorValues) {
  const errors: Record<string, string> = {}
  if (!v.fullName.trim()) errors.fullName = 'Nhập họ tên.'
  else if (v.fullName.trim().length > 150) errors.fullName = 'Tối đa 150 ký tự.'
  if (!(v.isInternal && v.lecturerId) && !EMAIL_PATTERN.test(v.email.trim())) errors.email = v.email.trim() ? 'Email không đúng định dạng.' : 'Nhập email.'
  if (v.organization.trim().length > 150) errors.organization = 'Tối đa 150 ký tự.'
  if (!isWholeIn(v.maxTeams, 1, 50)) errors.maxTeams = 'Từ 1 đến 50.'
  if (v.domainIds.length > MAX_DOMAINS) errors.domainIds = `Tối đa ${MAX_DOMAINS} domain.`
  return errors
}
