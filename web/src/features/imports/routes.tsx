import type { RouteObject } from 'react-router-dom'
import { ImportPage } from './ImportPage'

/** Route quản trị của Nam (FE-12, FE-13), tương đối với `/admin`: W06, W07 ở mục "Import sinh viên". */
export const adminImportRoutes: RouteObject[] = [{ path: 'import', element: <ImportPage /> }]
