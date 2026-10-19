import { createBrowserRouter, Navigate, RouterProvider } from 'react-router-dom'
import { env } from '@/shared/config/env'

// FE-01 – khung ban đầu. Khung trang (W25), đăng nhập (W01) và route của từng mảng
// được thêm ở FE-04, FE-05 và các task của từng thành viên (AGENTS.md mục 2).
function Placeholder() {
  return (
    <main style={{ padding: 24 }}>
      <h1>EXE Team Matching – Web quản trị</h1>
      <p>API Gateway: {env.apiBaseUrl}</p>
    </main>
  )
}

const router = createBrowserRouter([
  { path: '/', element: <Placeholder /> },
  { path: '*', element: <Navigate to="/" replace /> },
])

export function App() {
  return <RouterProvider router={router} />
}
