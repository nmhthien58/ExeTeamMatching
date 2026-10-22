import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/apiClient'
import type { Comment } from '@/features/tasks/api'

/** Schema Folder (collab.yaml). `isArchive` = "EXE101 (kỳ trước)" và nội dung bên trong – chỉ đọc. */
export interface Folder {
  id: string
  parentId?: string | null
  name: string
  isArchive: boolean
  itemCount: number
}

/** Schema ProjectFile – thông tin bản mới nhất. */
export interface ProjectFile {
  id: string
  folderId?: string | null
  name: string
  currentVersion: number
  sizeBytes: number
  contentType: string
  updatedByName: string
  updatedAt: string
  isArchive: boolean
}

export interface FileVersion {
  id: string
  version: number
  sizeBytes: number
  uploadedByName: string
  createdAt: string
}

export interface ProjectFileDetail extends ProjectFile {
  versions: FileVersion[]
  comments: Comment[]
}

export interface FolderContent {
  folder?: Folder | null
  breadcrumb: Folder[]
  folders: Folder[]
  files: ProjectFile[]
  canUpload: boolean
}

/** Quy tắc 24 – kiểm tra ngay ở client trước khi tải lên (server vẫn kiểm tra lại). */
export const MAX_FILE_BYTES = 25 * 1024 * 1024
export const ALLOWED_EXTENSIONS = ['pdf', 'docx', 'pptx', 'xlsx', 'png', 'jpg', 'jpeg', 'zip']
export const ACCEPT_ATTRIBUTE = ALLOWED_EXTENSIONS.map((e) => `.${e}`).join(',')

export function validateUpload(file: File): string | null {
  const ext = file.name.split('.').pop()?.toLowerCase() ?? ''
  if (!ALLOWED_EXTENSIONS.includes(ext)) return 'Chỉ nhận pdf, docx, pptx, xlsx, png, jpg, zip.'
  if (file.size === 0) return 'File rỗng.'
  if (file.size > MAX_FILE_BYTES) return 'File quá 25 MB. Hãy nén hoặc chia nhỏ rồi tải lại.'
  return null
}

export const fileKeys = {
  folders: (workspaceId: string) => ['files', 'folder', workspaceId] as const,
  folder: (workspaceId: string, folderId?: string) => ['files', 'folder', workspaceId, folderId ?? 'root'] as const,
  detail: (fileId: string) => ['files', 'detail', fileId] as const,
}

export function useFolder(workspaceId: string | undefined, folderId?: string) {
  return useQuery({
    queryKey: fileKeys.folder(workspaceId ?? '', folderId),
    queryFn: async () =>
      (await api.get<FolderContent>(`/api/workspaces/${workspaceId}/files`, { params: { folderId } })).data,
    enabled: !!workspaceId,
  })
}

export function useFileDetail(fileId: string | undefined) {
  return useQuery({
    queryKey: fileKeys.detail(fileId ?? ''),
    queryFn: async () => (await api.get<ProjectFileDetail>(`/api/files/${fileId}`)).data,
    enabled: !!fileId,
  })
}

function useInvalidateFiles(workspaceId: string) {
  const queryClient = useQueryClient()
  return (fileId?: string) => {
    void queryClient.invalidateQueries({ queryKey: fileKeys.folders(workspaceId) })
    if (fileId) void queryClient.invalidateQueries({ queryKey: fileKeys.detail(fileId) })
  }
}

/** Tải lên kèm tiến độ (0–100). Trùng tên trong thư mục → server tạo phiên bản mới. */
export function useUploadFile(workspaceId: string) {
  const invalidate = useInvalidateFiles(workspaceId)
  return useMutation({
    mutationFn: async ({ file, folderId, onProgress }: { file: File; folderId?: string; onProgress?: (percent: number) => void }) => {
      const form = new FormData()
      form.append('file', file)
      if (folderId) form.append('folderId', folderId)
      const { data } = await api.post<{ file: ProjectFile; createdNewVersion: boolean }>(`/api/workspaces/${workspaceId}/files`, form, {
        headers: { 'Content-Type': 'multipart/form-data' },
        onUploadProgress: (e) => e.total && onProgress?.(Math.round((e.loaded / e.total) * 100)),
      })
      return data
    },
    onSuccess: (data) => invalidate(data.file.id),
  })
}

export function useCreateFolder(workspaceId: string) {
  const invalidate = useInvalidateFiles(workspaceId)
  return useMutation({
    mutationFn: async ({ name, parentId }: { name: string; parentId?: string }) =>
      (await api.post<Folder>(`/api/workspaces/${workspaceId}/folders`, { name, parentId })).data,
    onSuccess: () => invalidate(),
  })
}

export function useDeleteFile(workspaceId: string) {
  const invalidate = useInvalidateFiles(workspaceId)
  return useMutation({
    mutationFn: async (fileId: string) => {
      await api.delete(`/api/files/${fileId}`)
    },
    onSuccess: () => invalidate(),
  })
}

export function useAddFileComment(workspaceId: string, fileId: string) {
  const invalidate = useInvalidateFiles(workspaceId)
  return useMutation({
    mutationFn: async ({ body, version }: { body: string; version?: number }) =>
      (await api.post<Comment>(`/api/files/${fileId}/comments`, { body, version })).data,
    onSuccess: () => invalidate(fileId),
  })
}

/** Lấy URL ký sẵn rồi mở tải về (URL hết hạn sau 10 phút nên không lưu cache). */
export async function downloadFile(fileId: string, version?: number) {
  const { data } = await api.get<{ url: string }>(`/api/files/${fileId}/download-url`, { params: { version } })
  window.open(data.url, '_blank', 'noopener')
}

/** Icon Material theo loại file (dòng file – DESIGN.md 6.3). */
export function fileIcon(name: string): string {
  const ext = name.split('.').pop()?.toLowerCase()
  switch (ext) {
    case 'pdf':
      return 'picture_as_pdf'
    case 'docx':
      return 'description'
    case 'pptx':
      return 'slideshow'
    case 'xlsx':
      return 'table_chart'
    case 'png':
    case 'jpg':
    case 'jpeg':
      return 'image'
    case 'zip':
      return 'folder_zip'
    default:
      return 'draft'
  }
}
