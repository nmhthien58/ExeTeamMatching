import type { RouteObject } from 'react-router-dom'
import type { Shell } from '@/shared/auth/AuthProvider'
import { adminAuditLogRoutes } from '@/features/audit-log/routes'
import { adminAutoAssignRoutes } from '@/features/auto-assign/routes'
import { advisorMessageRoutes } from '@/features/messages/routes'
import { studentTeamRoutes } from '@/features/my_team/routes'
import { suggestionRoutes } from '@/features/suggestions/routes'
import { workspaceRoutes } from '@/features/workspace/routes'

/**
 * Route của màn hình đã làm, theo khung (đường dẫn tương đối với `/admin`, `/sv`, `/gv`, `/mentor`).
 * Route trùng với mục điều hướng sẽ thay ScreenPlaceholder của mục đó; mỗi mảng thêm route của mình vào đây.
 */
export const featureRoutes: Record<Shell, RouteObject[]> = {
  admin: [...adminAutoAssignRoutes, ...adminAuditLogRoutes],
  student: [workspaceRoutes('/sv'), ...suggestionRoutes, ...studentTeamRoutes],
  lecturer: [workspaceRoutes('/gv'), ...advisorMessageRoutes],
  mentor: [workspaceRoutes('/mentor'), ...advisorMessageRoutes],
}
