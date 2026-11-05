import type { RouteObject } from 'react-router-dom'
import { FilesPage } from '@/features/files/FilesPage'
import { MeetingsPage } from '@/features/meetings/MeetingsPage'
import { WorkspaceMessagesPage } from '@/features/messages/MessagesPage'
import { TaskBoardPage } from '@/features/tasks/TaskBoardPage'
import { ProjectOverviewPage } from './ProjectOverviewPage'
import { WorkspaceLayout } from './WorkspaceLayout'

/** Trang con của một dự án. Lịch họp C07–C08 (FE-36, Nam). */
const workspaceChildren: RouteObject[] = [
  { index: true, element: <ProjectOverviewPage /> },
  { path: 'viec', element: <TaskBoardPage /> },
  { path: 'viec/:taskId', element: <TaskBoardPage /> },
  { path: 'file', element: <FilesPage /> },
  { path: 'tin-nhan', element: <WorkspaceMessagesPage /> },
  { path: 'lich-hop', element: <MeetingsPage /> },
]

/**
 * Route dự án (C01, C04–C06 – FE-39, FE-40). Sinh viên: `/sv/du-an` (workspace của mình).
 * Giảng viên / mentor: `/gv/du-an/{workspaceId}`, `/mentor/du-an/{workspaceId}` – mở từ L04, L05, M01.
 */
/** Mục Tin nhắn của sinh viên (FE-53): C02 + C03 của nhóm mình ở `/sv/tin-nhan`, cùng khung dự án để có ngữ cảnh workspace. */
export const studentMessagesRoute: RouteObject = {
  path: 'tin-nhan',
  element: <WorkspaceLayout basePath="/sv/du-an" />,
  children: [{ index: true, element: <WorkspaceMessagesPage /> }],
}

export function workspaceRoutes(shellBase: '/sv' | '/gv' | '/mentor'): RouteObject {
  return shellBase === '/sv'
    ? { path: 'du-an', element: <WorkspaceLayout basePath="/sv/du-an" />, children: workspaceChildren }
    : { path: 'du-an/:workspaceId', element: <WorkspaceLayout basePath={`${shellBase}/du-an`} />, children: workspaceChildren }
}
