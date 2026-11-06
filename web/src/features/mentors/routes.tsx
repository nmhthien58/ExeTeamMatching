import type { RouteObject } from 'react-router-dom'
import { MentorsPage } from './MentorsPage'

/** W27 (FE-34), tương đối với `/admin`. */
export const adminMentorRoutes: RouteObject[] = [{ path: 'mentor', element: <MentorsPage /> }]
