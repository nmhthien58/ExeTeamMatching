import type { RouteObject } from 'react-router-dom'
import { LecturerSchedulePage, TeamSchedulePage } from './MeetingSchedulePages'

/** C09 của sinh viên, tương đối với `/sv` – mở từ S15. */
export const studentScheduleRoutes: RouteObject[] = [{ path: 'nhom/lich-hop', element: <TeamSchedulePage /> }]

/** C09 của giảng viên, tương đối với `/gv` – lối vào từ L01, C07 do màn đó thêm. */
export const lecturerScheduleRoutes: RouteObject[] = [{ path: 'lich-hop', element: <LecturerSchedulePage /> }]
