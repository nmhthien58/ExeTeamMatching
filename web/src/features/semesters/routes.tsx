import type { RouteObject } from 'react-router-dom'
import { SemestersPage } from './SemestersPage'

/** Route quản trị của Nam (FE-09, FE-10), tương đối với `/admin`: W03, W04 ở mục "Học kỳ và đợt". */
export const adminSemesterRoutes: RouteObject[] = [{ path: 'hoc-ky', element: <SemestersPage /> }]
