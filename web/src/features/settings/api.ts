import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

// academic.yaml tag Settings (W05, BE-23).

export interface RuleSettings {
  minTeamSize: number
  maxTeamSize: number
  minSpecializations: number
  invitationExpiryHours: number
  reminderHours: number[]
  maxIdeasPerJoinRequest: number
  interestExtraSlots: number
  maxInterestsUnlimited: number
  maxLecturerPreferences: number
  maxFileSizeMb: number
}

export type NumberField = Exclude<keyof RuleSettings, 'reminderHours'>

const key = ['rule-settings'] as const

export function useRuleSettings() {
  return useQuery({ queryKey: key, queryFn: async () => (await api.get<RuleSettings>('/api/rule-settings')).data })
}

export function useSaveRuleSettings() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (input: RuleSettings) => (await api.put<RuleSettings>('/api/rule-settings', input)).data,
    onSuccess: (settings) => client.setQueryData(key, settings),
  })
}

/** Giới hạn từng ô, khớp RuleSettingsInputValidator ở Academic. */
export const fieldRules: Record<NumberField, { label: string; min: number; max: number; unit?: string; hint?: string }> = {
  minTeamSize: { label: 'Sĩ số tối thiểu', min: 2, max: 50, unit: 'người' },
  maxTeamSize: { label: 'Sĩ số tối đa', min: 2, max: 50, unit: 'người' },
  minSpecializations: { label: 'Số chuyên ngành tối thiểu', min: 1, max: 50, hint: 'Nhóm phải có ít nhất số chuyên ngành này.' },
  invitationExpiryHours: { label: 'Hạn hiệu lực lời mời', min: 1, max: 720, unit: 'giờ' },
  maxIdeasPerJoinRequest: { label: 'Số ý tưởng tối đa khi xin vào nhóm', min: 1, max: 10 },
  maxLecturerPreferences: { label: 'Số nguyện vọng giảng viên của nhóm', min: 1, max: 10 },
  interestExtraSlots: { label: 'Suất quan tâm thêm', min: 0, max: 10, hint: 'Giảng viên, mentor được quan tâm tới số nhóm còn nhận cộng thêm số này.' },
  maxInterestsUnlimited: { label: 'Giới hạn quan tâm khi không đặt số nhóm tối đa', min: 1, max: 100 },
  maxFileSizeMb: { label: 'Dung lượng file tối đa', min: 1, max: 100, unit: 'MB' },
}

export const MAX_REMINDERS = 5

export type RuleValues = Record<NumberField, string> & { reminderHours: string }

/** Mốc nhắc nhập dạng "72, 24". Trả null nếu có phần không phải số nguyên. */
export function parseReminders(text: string): number[] | null {
  const parts = text.split(/[,\s]+/).filter(Boolean)
  if (parts.some((p) => !/^\d+$/.test(p))) return null
  return parts.map(Number)
}

export function validateRules(values: RuleValues): Record<string, string> {
  const errors: Record<string, string> = {}
  for (const [field, rule] of Object.entries(fieldRules) as [NumberField, (typeof fieldRules)[NumberField]][]) {
    const text = values[field].trim()
    const n = Number(text)
    if (!/^\d+$/.test(text) || n < rule.min || n > rule.max) errors[field] = `Từ ${rule.min} đến ${rule.max}.`
  }
  if (!errors.minTeamSize && !errors.maxTeamSize && Number(values.maxTeamSize) < Number(values.minTeamSize)) {
    errors.maxTeamSize = 'Phải lớn hơn hoặc bằng sĩ số tối thiểu.'
  }
  if (!errors.minSpecializations && !errors.maxTeamSize && Number(values.minSpecializations) > Number(values.maxTeamSize)) {
    errors.minSpecializations = 'Không được vượt sĩ số tối đa.'
  }
  const reminders = parseReminders(values.reminderHours)
  if (!reminders || reminders.some((h) => h < 1 || h > 720) || new Set(reminders).size !== reminders.length) {
    errors.reminderHours = 'Các số giờ từ 1 đến 720, cách nhau bằng dấu phẩy, không trùng nhau.'
  } else if (reminders.length > MAX_REMINDERS) {
    errors.reminderHours = `Tối đa ${MAX_REMINDERS} mốc nhắc.`
  }
  return errors
}
