import type { RouteObject } from 'react-router-dom'
import { MentorMatchingPage } from './MentorMatchingPage'

/** Route quản trị W28 (FE-43), tương đối với `/admin`. */
export const adminMentorMatchingRoutes: RouteObject[] = [{ path: 'ghep-mentor', element: <MentorMatchingPage /> }]
