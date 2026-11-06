import type { RouteObject } from 'react-router-dom'
import { ProjectsPage } from './ProjectsPage'

/** Admin route W29, relative to `/admin`; registered in shared featureRoutes. */
export const projectRoutes: RouteObject[] = [
  { path: 'de-tai', element: <ProjectsPage /> },
]
