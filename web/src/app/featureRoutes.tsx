import type { RouteObject } from 'react-router-dom'
import type { Shell } from '@/shared/auth/AuthProvider'
import { workspaceRoutes } from '@/features/workspace/routes'

/**
 * Route của màn hình đã làm, theo khung (đường dẫn tương đối với `/admin`, `/sv`, `/gv`, `/mentor`).
 * Route trùng với mục điều hướng sẽ thay ScreenPlaceholder của mục đó; mỗi mảng thêm route của mình vào đây.
 */
export const featureRoutes: Record<Shell, RouteObject[]> = {
  admin: [],
  student: [workspaceRoutes('/sv')],
  lecturer: [workspaceRoutes('/gv')],
  mentor: [workspaceRoutes('/mentor')],
}
