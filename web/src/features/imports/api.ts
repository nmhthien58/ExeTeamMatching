import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { CourseCode } from '@/shared/phase/usePhase'
import type { Paged } from '@/shared/components/DataTable'

/** academic.yaml, tag Imports. */
export interface ImportRow {
  rowNumber: number
  studentCode: string
  fullName: string
  email: string
  specializationCode?: string | null
  classCode: string
  phone: string | null
}

export interface ImportRowError {
  field: string
  message: string
}

export interface ImportErrorRow extends ImportRow {
  errors: ImportRowError[]
}

export interface ImportPreview {
  previewId: string
  fileName: string
  totalRows: number
  validRows: ImportRow[]
  errorRows: ImportErrorRow[]
}

export interface ImportBatch {
  id: string
  semesterId: string
  fileName: string
  totalRows: number
  successRows: number
  failedRows: number
  status: 'PROCESSING' | 'COMPLETED' | 'FAILED'
  importedByName: string | null
  createdAt: string
}

export interface SemesterOption {
  id: string
  code: string
  name: string
  isCurrent: boolean
}

/** team.yaml, `POST /api/teams/baseline-import`. */
export interface BaselineResult {
  teamCount: number
  studentCount: number
  linkedProjectCount: number
  unlinkedTeamCount: number
  errors: { row: number; message: string }[]
}

/** `POST /api/teams/auto-inherit` (BE-92, FE-56). */
export interface InheritedTeamMemberSummary {
  studentId: string
  studentCode: string
  fullName: string
  specializationId: number | null
  isLeader: boolean
}

export interface InheritedTeamSummary {
  teamId: string | null
  teamName: string
  oldTeamId: string | null
  projectId: string | null
  projectName: string | null
  leaderStudentCode: string
  leaderName: string
  memberCount: number
  members: InheritedTeamMemberSummary[]
  missingOldMemberStudentCodes: string[]
  status: 'DRAFT' | 'NEEDS_REVIEW' | string
}

export interface AutoInheritResponse {
  inheritedTeamCount: number
  inheritedStudentCount: number
  freeStudentCount: number
  needsReviewTeamCount: number
  teams: InheritedTeamSummary[]
}

/** Giới hạn của server (`ImportPreviewInputValidator`), kiểm tra trước để khỏi tải lên vô ích. */
export const MAX_FILE_MB = 5

/** Trả câu lỗi nếu file không nhận được; null nếu hợp lệ. */
export function fileProblem(file: File): string | null {
  if (!file.name.toLowerCase().endsWith('.xlsx')) return 'Chỉ nhận file Excel .xlsx.'
  if (file.size > MAX_FILE_MB * 1024 * 1024) return `File tối đa ${MAX_FILE_MB} MB.`
  return null
}

export function useSemesters() {
  return useQuery({
    queryKey: ['semesters'],
    queryFn: async () => (await api.get<SemesterOption[]>('/api/semesters')).data,
  })
}

/** Tải file mẫu kèm JWT nên không dùng được thẻ link thường. */
export async function downloadTemplate() {
  const { data } = await api.get<Blob>('/api/imports/students/template', { responseType: 'blob' })
  const url = URL.createObjectURL(data)
  const link = document.createElement('a')
  link.href = url
  link.download = 'mau-import-sinh-vien.xlsx'
  link.click()
  URL.revokeObjectURL(url)
}

export function usePreviewImport() {
  return useMutation({
    mutationFn: async ({ file, semesterId, courseCode }: { file: File; semesterId: string; courseCode: CourseCode }) => {
      const form = new FormData()
      form.append('file', file)
      form.append('semesterId', semesterId)
      form.append('courseCode', courseCode)
      return (
        await api.post<ImportPreview>('/api/imports/students/preview', form, {
          headers: { 'Content-Type': 'multipart/form-data' },
        })
      ).data
    },
  })
}

export function useConfirmImport() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async (previewId: string) => (await api.post<ImportBatch>(`/api/imports/students/${previewId}/confirm`)).data,
    onSuccess: () => client.invalidateQueries({ queryKey: ['imports'] }),
  })
}

/** `dryRun = true` chỉ kiểm tra, chưa tạo nhóm. */
export function useBaselineImport() {
  return useMutation({
    mutationFn: async ({ file, semesterId, dryRun }: { file: File; semesterId: string; dryRun: boolean }) => {
      const form = new FormData()
      form.append('file', file)
      form.append('semesterId', semesterId)
      form.append('dryRun', String(dryRun))
      return (
        await api.post<BaselineResult>('/api/teams/baseline-import', form, {
          headers: { 'Content-Type': 'multipart/form-data' },
        })
      ).data
    },
  })
}

/** Tự động đối chiếu kế thừa nhóm EXE201 từ EXE101 kỳ trước (BE-92, FE-56). */
export function useAutoInherit() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: async ({ semesterId, dryRun }: { semesterId: string; dryRun: boolean }) => {
      return (
        await api.post<AutoInheritResponse>('/api/teams/auto-inherit', {
          semesterId,
          dryRun,
        })
      ).data
    },
    onSuccess: (_, variables) => {
      if (!variables.dryRun) {
        client.invalidateQueries({ queryKey: ['teams'] })
        client.invalidateQueries({ queryKey: ['admin-teams'] })
      }
    },
  })
}

/** W08 – lịch sử import, mới nhất trước. Khóa nằm dưới `['imports']` nên tự làm mới sau khi xác nhận import. */
export function useImportHistory(semesterId: string | undefined, page: number) {
  return useQuery({
    queryKey: ['imports', 'history', semesterId ?? 'all', page],
    queryFn: async () => (await api.get<Paged<ImportBatch>>('/api/imports', { params: { semesterId, page, pageSize: 10 } })).data,
  })
}
