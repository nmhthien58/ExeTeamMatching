import type { RouteObject } from 'react-router-dom'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { FinalizePage } from '@/features/finalize/FinalizePage'
import { UnmatchedPage } from '@/features/unmatched/UnmatchedPage'
import { AutoAssignResultPage } from './AutoAssignResultPage'

/** Route quản trị (FE-08, FE-26 → FE-28), tương đối với `/admin`: W02 ở trang đầu, W20 → W22 dưới mục "Chưa có nhóm". */
export const adminAutoAssignRoutes: RouteObject[] = [
  { index: true, element: <DashboardPage /> },
  { path: 'chua-co-nhom', element: <UnmatchedPage /> },
  { path: 'chua-co-nhom/ket-qua', element: <AutoAssignResultPage /> },
  { path: 'chua-co-nhom/chot', element: <FinalizePage /> },
]
