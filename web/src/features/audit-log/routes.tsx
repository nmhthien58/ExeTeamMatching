import type { RouteObject } from 'react-router-dom'
import { AuditLogPage } from './AuditLogPage'

/** Route quản trị của Cường (FE-30), tương đối với `/admin`: W24 ở mục "Nhật ký". */
export const adminAuditLogRoutes: RouteObject[] = [{ path: 'nhat-ky', element: <AuditLogPage /> }]
