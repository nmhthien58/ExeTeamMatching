import type { RouteObject } from 'react-router-dom'
import { AnnouncementPage } from './AnnouncementPage'

/** Route quản trị của Thành (FE-29), tương đối với `/admin`: W23 ở mục "Gửi thông báo chung". */
export const adminAnnouncementRoutes: RouteObject[] = [{ path: 'thong-bao', element: <AnnouncementPage /> }]
