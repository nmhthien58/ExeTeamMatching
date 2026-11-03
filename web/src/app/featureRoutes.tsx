import type { RouteObject } from 'react-router-dom'
import type { Shell } from '@/shared/auth/AuthProvider'
import { adminAuditLogRoutes } from '@/features/audit-log/routes'
import { adminTeamRoutes } from '@/features/teams/routes'
import { adminAutoAssignRoutes } from '@/features/auto-assign/routes'
import { adminLecturerMatchingRoutes } from '@/features/lecturer-matching/routes'
import { adminMentorMatchingRoutes } from '@/features/mentor-matching/routes'
import { adminImportRoutes } from '@/features/imports/routes'
import { adminSemesterRoutes } from '@/features/semesters/routes'
import { changeRequestRoutes, studentChangeRequestRoutes } from '@/features/change-requests/routes'
import { projectRoutes } from '@/features/projects/routes'
import { completeProfileRoute, lecturerProfileRoutes, mentorProfileRoutes, requireCompletedProfile, studentProfileRoutes } from '@/features/profile/routes'
import { advisorMessageRoutes } from '@/features/messages/routes'
import { studentTeamRoutes } from '@/features/my_team/routes'
import { suggestionRoutes } from '@/features/suggestions/routes'
import { workspaceRoutes } from '@/features/workspace/routes'
import { studentAdvisorRoutes } from '@/features/advisor-preferences/routes'
import { studentNotificationRoutes, lecturerNotificationRoutes, mentorNotificationRoutes } from '@/features/notifications/routes'

/**
 * Route của màn hình đã làm, theo khung (đường dẫn tương đối với `/admin`, `/sv`, `/gv`, `/mentor`).
 * Route trùng với mục điều hướng sẽ thay ScreenPlaceholder của mục đó; mỗi mảng thêm route của mình vào đây.
 */
export const featureRoutes: Record<Shell, RouteObject[]> = {
  admin: [...adminSemesterRoutes, ...adminImportRoutes, ...adminAutoAssignRoutes, ...adminTeamRoutes, ...adminAuditLogRoutes, ...adminLecturerMatchingRoutes, ...adminMentorMatchingRoutes, ...changeRequestRoutes, ...projectRoutes],
  student: [completeProfileRoute, ...requireCompletedProfile([workspaceRoutes('/sv'), ...suggestionRoutes, ...studentTeamRoutes, ...studentProfileRoutes, ...studentAdvisorRoutes, ...studentChangeRequestRoutes, ...studentNotificationRoutes])],
  lecturer: [workspaceRoutes('/gv'), ...advisorMessageRoutes, ...lecturerProfileRoutes, ...lecturerNotificationRoutes],
  mentor: [workspaceRoutes('/mentor'), ...advisorMessageRoutes, ...mentorProfileRoutes, ...mentorNotificationRoutes],
}
