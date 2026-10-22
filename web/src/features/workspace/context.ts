import { createContext, useContext } from 'react'
import type { WorkspaceOverview } from './api'

export interface WorkspaceContextValue {
  workspace: WorkspaceOverview
  /** Đường dẫn gốc của dự án: `/sv/du-an` (sinh viên) hoặc `/gv/du-an/{id}`, `/mentor/du-an/{id}`. */
  basePath: string
  /** Sinh viên của nhóm – được tạo việc, tải file (rule 23, 24). */
  canEdit: boolean
}

export const WorkspaceContext = createContext<WorkspaceContextValue | null>(null)

export function useWorkspaceContext() {
  const ctx = useContext(WorkspaceContext)
  if (!ctx) throw new Error('useWorkspaceContext phải nằm trong WorkspaceLayout')
  return ctx
}
