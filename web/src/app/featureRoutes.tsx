import type { RouteObject } from 'react-router-dom'
import type { Shell } from '@/shared/auth/AuthProvider'
import { adminAnnouncementRoutes } from '@/features/announcements/routes'
import { adminAuditLogRoutes } from '@/features/audit-log/routes'
import { adminClientErrorRoutes } from '@/features/client-errors/routes'
import { adminTeamRoutes } from '@/features/teams/routes'
import { adminAutoAssignRoutes } from '@/features/auto-assign/routes'
import { adminLecturerMatchingRoutes } from '@/features/lecturer-matching/routes'
import { adminMentorMatchingRoutes } from '@/features/mentor-matching/routes'
import { adminImportRoutes } from '@/features/imports/routes'
import { adminLecturerRoutes } from '@/features/lecturers/routes'
import { adminMentorRoutes } from '@/features/mentors/routes'
import { adminStudentRoutes } from '@/features/students/routes'
import { adminCatalogRoutes } from '@/features/catalogs/routes'
import { adminRuleSettingsRoutes } from '@/features/settings/routes'
import { adminSemesterRoutes } from '@/features/semesters/routes'
import { changeRequestRoutes, studentChangeRequestRoutes } from '@/features/change-requests/routes'
import { projectRoutes } from '@/features/projects/routes'
import { completeProfileRoute, lecturerProfileRoutes, mentorProfileRoutes, requireCompletedLecturerProfile, requireCompletedProfile, studentProfileRoutes } from '@/features/profile/routes'
import { lecturerScheduleRoutes, studentScheduleRoutes } from '@/features/meeting-schedule/routes'
import { advisorMessageRoutes } from '@/features/messages/routes'
import { lecturerCalendarRoutes } from '@/features/meetings/routes'
import { lecturerRoutes, mentorRoutes } from '@/features/advisor/routes'
import { studentTeamRoutes } from '@/features/my_team/routes'
import { suggestionRoutes } from '@/features/suggestions/routes'
import { studentMessagesRoute, workspaceRoutes } from '@/features/workspace/routes'
import { studentAdvisorRoutes } from '@/features/advisor-preferences/routes'
import { studentNotificationRoutes, lecturerNotificationRoutes, mentorNotificationRoutes } from '@/features/notifications/routes'

/**
 * Route của màn hình đã làm, theo khung (đường dẫn tương đối với `/admin`, `/sv`, `/gv`, `/mentor`).
 * Route trùng với mục điều hướng sẽ thay ScreenPlaceholder của mục đó; mỗi mảng thêm route của mình vào đây.
 */
export const featureRoutes: Record<Shell, RouteObject[]> = {
  admin: [...adminSemesterRoutes, ...adminImportRoutes, ...adminStudentRoutes, ...adminLecturerRoutes, ...adminMentorRoutes, ...adminCatalogRoutes, ...adminRuleSettingsRoutes, ...adminAutoAssignRoutes, ...adminTeamRoutes, ...adminAuditLogRoutes, ...adminClientErrorRoutes, ...adminLecturerMatchingRoutes, ...adminMentorMatchingRoutes, ...changeRequestRoutes, ...projectRoutes, ...adminAnnouncementRoutes],
  student: [completeProfileRoute, ...requireCompletedProfile([workspaceRoutes('/sv'), studentMessagesRoute, ...suggestionRoutes, ...studentTeamRoutes, ...studentScheduleRoutes, ...studentProfileRoutes, ...studentAdvisorRoutes, ...studentChangeRequestRoutes, ...studentNotificationRoutes])],
  lecturer: [
    ...lecturerProfileRoutes,
    ...requireCompletedLecturerProfile([workspaceRoutes('/gv'), ...advisorMessageRoutes, ...lecturerScheduleRoutes, ...lecturerCalendarRoutes, ...lecturerRoutes, ...lecturerNotificationRoutes]),
  ],
  mentor: [workspaceRoutes('/mentor'), ...advisorMessageRoutes, ...mentorProfileRoutes, ...mentorRoutes, ...mentorNotificationRoutes],
}
