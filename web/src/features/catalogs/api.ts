import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'

// academic.yaml tag Catalogs (W12, W13, W14).

export interface CatalogItem {
  id: number
  code: string
  name: string
  description?: string | null
  isActive: boolean
  majorId?: number | null
}

export interface CatalogInput {
  code: string
  name: string
  description?: string | null
  isActive?: boolean
  majorId?: number | null
}

export type CatalogKind = 'class-codes' | 'domains' | 'specializations' | 'majors'

/** Khác nhau giữa 4 danh mục, khớp kiểm tra ở Academic (CatalogRules.Validate). */
export const catalogKinds: Record<CatalogKind, {
  tab: string
  screen: string
  noun: string
  codePattern: RegExp
  codeHint: string
  /** Mã lớp chỉ có mã: tên chính là mã. */
  hasName: boolean
  maxName: number
  /** Chỉ domain có mô tả (lĩnh vực con). */
  hasDescription: boolean
}> = {
  'class-codes': { tab: 'Mã lớp', screen: 'W12', noun: 'mã lớp', codePattern: /^[A-Z0-9]{3,20}$/, codeHint: '3–20 ký tự: chữ in hoa hoặc số', hasName: false, maxName: 0, hasDescription: false },
  domains: { tab: 'Domain', screen: 'W13', noun: 'domain', codePattern: /^D(0[1-9]|1[0-8])$/, codeHint: 'D01 đến D18', hasName: true, maxName: 150, hasDescription: true },
  majors: { tab: 'Ngành', screen: 'W27', noun: 'ngành', codePattern: /^[A-Z]{2,10}$/, codeHint: '2–10 ký tự: chữ in hoa', hasName: true, maxName: 100, hasDescription: false },
  specializations: { tab: 'Chuyên ngành', screen: 'W14', noun: 'chuyên ngành', codePattern: /^[A-Z0-9_]{2,20}$/, codeHint: '2–20 ký tự: chữ in hoa, số hoặc _', hasName: true, maxName: 100, hasDescription: false },
}

export const catalogKey = (kind: CatalogKind) => ['catalogs', kind] as const

/** Cả mục đã ngừng, để Bộ môn mở lại được. */
export function useCatalog(kind: CatalogKind) {
  return useQuery({ queryKey: catalogKey(kind), queryFn: async () => (await api.get<CatalogItem[]>(`/api/${kind}`)).data })
}

export function useSaveCatalogItem(kind: CatalogKind) {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ id, input }: { id?: number; input: CatalogInput }) =>
      (id ? await api.put<CatalogItem>(`/api/${kind}/${id}`, input) : await api.post<CatalogItem>(`/api/${kind}`, input)).data,
    onSuccess: () => void client.invalidateQueries({ queryKey: catalogKey(kind) }),
  })
}

export interface CatalogValues {
  code: string
  name: string
  description: string
  isActive: boolean
  majorId?: number | null
}

export function validateCatalog(kind: CatalogKind, values: CatalogValues): Record<string, string> {
  const rules = catalogKinds[kind]
  const errors: Record<string, string> = {}
  const code = values.code.trim().toUpperCase()
  if (!code) errors.code = 'Nhập mã.'
  else if (!rules.codePattern.test(code)) errors.code = `Mã không hợp lệ (${rules.codeHint}).`
  if (rules.hasName) {
    const name = values.name.trim()
    if (!name) errors.name = 'Nhập tên.'
    else if (name.length > rules.maxName) errors.name = `Tên tối đa ${rules.maxName} ký tự.`
  }
  if (rules.hasDescription && values.description.trim().length > 500) errors.description = 'Mô tả tối đa 500 ký tự.'
  if (kind === 'specializations' && !values.majorId) {
    errors.majorId = 'Chọn ngành.'
  }
  return errors
}
