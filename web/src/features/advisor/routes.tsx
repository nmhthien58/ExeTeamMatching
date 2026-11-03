import type { RouteObject } from 'react-router-dom'
import { AdvisedTeamDetailPage, AdvisedTeamsPage } from './AdvisedTeamsPage'
import { LecturerHomePage, MentorHomePage } from './AdvisorHomePages'
import { TopicDetailPage, TopicsPage } from './TopicsPage'

export const lecturerRoutes: RouteObject[] = [
  { index: true, element: <LecturerHomePage /> },
  { path: 'de-tai', element: <TopicsPage kind="LECTURER" /> },
  { path: 'de-tai/:teamId', element: <TopicDetailPage kind="LECTURER" /> },
  { path: 'nhom', element: <AdvisedTeamsPage kind="LECTURER" /> },
  { path: 'nhom/:teamId', element: <AdvisedTeamDetailPage kind="LECTURER" /> },
]

export const mentorRoutes: RouteObject[] = [
  { index: true, element: <MentorHomePage /> },
  { path: 'de-tai', element: <TopicsPage kind="MENTOR" /> },
  { path: 'de-tai/:teamId', element: <TopicDetailPage kind="MENTOR" /> },
  { path: 'nhom', element: <AdvisedTeamsPage kind="MENTOR" /> },
  { path: 'nhom/:teamId', element: <AdvisedTeamDetailPage kind="MENTOR" /> },
]
