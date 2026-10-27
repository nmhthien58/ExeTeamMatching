import type { RouteObject } from 'react-router-dom'
import { AdvisorMessagesPage } from './MessagesPage'

/** Tab Tin nhắn của giảng viên / mentor (FE-38), tương đối với `/gv`, `/mentor`. Sinh viên mở từ dự án (`/sv/du-an/tin-nhan`). */
export const advisorMessageRoutes: RouteObject[] = [{ path: 'tin-nhan', element: <AdvisorMessagesPage /> }]
