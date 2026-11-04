import type { RouteObject } from 'react-router-dom'
import { AdvisorCalendarPage } from './AdvisorCalendarPage'

/** Tab Lịch của giảng viên – C07 gom mọi nhóm (FE-36, Nam). */
export const lecturerCalendarRoutes: RouteObject[] = [{ path: 'lich', element: <AdvisorCalendarPage /> }]
