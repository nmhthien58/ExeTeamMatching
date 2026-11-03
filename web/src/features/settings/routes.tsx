import type { RouteObject } from 'react-router-dom'
import { RuleSettingsPage } from './RuleSettingsPage'

/** W05 (FE-11), tương đối với `/admin`. */
export const adminRuleSettingsRoutes: RouteObject[] = [{ path: 'cau-hinh', element: <RuleSettingsPage /> }]
