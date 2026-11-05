import type { RouteObject } from 'react-router-dom'
import { ClientErrorsPage } from './ClientErrorsPage'

/** Route quản trị của Nguyên (FE-50), tương đối với `/admin`: W30 ở mục "Theo dõi lỗi ứng dụng". */
export const adminClientErrorRoutes: RouteObject[] = [{ path: 'loi-ung-dung', element: <ClientErrorsPage /> }]
