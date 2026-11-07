import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

export interface TeamRules {
  minSize: number
  maxSize: number
  minSpecializations: number
}

/** Mặc định khi chưa đọc được cấu hình, khớp DEFAULT của academic.rule_settings. */
export const DEFAULT_TEAM_RULES: TeamRules = { minSize: 4, maxSize: 6, minSpecializations: 2 }

interface RuleSettingsDto {
  minTeamSize?: number
  maxTeamSize?: number
  minSpecializations?: number
}

/** Quy tắc lập nhóm Bộ môn đặt ở W05 (BE-90) để chữ hiển thị khớp cấu hình. Đang tải hoặc lỗi thì dùng mặc định. */
export function useTeamRules(): TeamRules {
  const { data } = useQuery({
    queryKey: ['rule-settings'],
    queryFn: async () => (await api.get<RuleSettingsDto>('/api/rule-settings')).data,
    staleTime: 60_000,
  })
  return {
    minSize: data?.minTeamSize ?? DEFAULT_TEAM_RULES.minSize,
    maxSize: data?.maxTeamSize ?? DEFAULT_TEAM_RULES.maxSize,
    minSpecializations: data?.minSpecializations ?? DEFAULT_TEAM_RULES.minSpecializations,
  }
}
