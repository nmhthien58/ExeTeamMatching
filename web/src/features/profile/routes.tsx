import type { RouteObject } from 'react-router-dom'
import { LecturerProfilePage } from './LecturerProfilePage'
import { CompleteProfilePage, EditProfilePage, MyProfilePage, ProfileGate } from './ProfilePages'
import { SettingsPage } from './SettingsPage'
import { StudentProfilePage } from './StudentProfilePage'

/** Route của Nam (FE-35), tương đối với khung: A05, S02–S04 ở `/sv`, L06 ở `/gv`, A07 ở cả ba khung. */
export const studentProfileRoutes: RouteObject[] = [
  { path: 'ho-so', element: <MyProfilePage /> },
  { path: 'ho-so/sua', element: <EditProfilePage /> },
  { path: 'sinh-vien/:id', element: <StudentProfilePage /> },
  { path: 'cai-dat', element: <SettingsPage /> },
]

/** A05 nằm ngoài ProfileGate, nếu không sẽ chuyển hướng vòng tròn. */
export const completeProfileRoute: RouteObject = { path: 'ho-so/hoan-thien', element: <CompleteProfilePage /> }

export const lecturerProfileRoutes: RouteObject[] = [
  { path: 'ho-so', element: <LecturerProfilePage /> },
  { path: 'cai-dat', element: <SettingsPage /> },
]

export const mentorProfileRoutes: RouteObject[] = [{ path: 'cai-dat', element: <SettingsPage /> }]

/** Bọc route sinh viên: chưa hoàn thiện hồ sơ thì sang A05 (rule 16). Giữ nguyên path để App.tsx nhận ra màn đã làm. */
export function requireCompletedProfile(routes: RouteObject[]): RouteObject[] {
  return routes.map((route) => ({ ...route, element: <ProfileGate>{route.element}</ProfileGate> }) as RouteObject)
}
