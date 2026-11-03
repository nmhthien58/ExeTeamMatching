import type { RouteObject } from 'react-router-dom'
import { CatalogsPage } from './CatalogsPage'

/** W12–W14 (FE-18, FE-19, FE-20), tương đối với `/admin`. */
export const adminCatalogRoutes: RouteObject[] = [{ path: 'danh-muc', element: <CatalogsPage /> }]
