import { NavLink, useSearchParams } from 'react-router-dom'
import clsx from 'clsx'

/** Ba bước sau hạn chót (W20 → W21 → W22) dưới một mục sidebar "Chưa có nhóm"; giữ môn đang xem khi chuyển tab. */
export function AutoAssignTabs() {
  const [params] = useSearchParams()
  const query = params.get('mon') ? `?mon=${params.get('mon')}` : ''
  const tabs = [
    { to: '/admin/chua-co-nhom', label: 'Chưa có nhóm', end: true },
    { to: '/admin/chua-co-nhom/ket-qua', label: 'Kết quả ghép', end: false },
    { to: '/admin/chua-co-nhom/chot', label: 'Chốt danh sách', end: false },
  ]
  return (
    <nav aria-label="Các bước sau hạn chót" className="mb-4 flex gap-1 border-b border-border">
      {tabs.map((tab) => (
        <NavLink
          key={tab.to}
          to={tab.to + query}
          end={tab.end}
          className={({ isActive }) =>
            clsx(
              'inline-flex min-h-12 items-center border-b-2 px-4 font-medium',
              isActive ? 'border-secondary text-secondary-dark' : 'border-transparent text-text-secondary hover:text-text-primary',
            )
          }
        >
          {tab.label}
        </NavLink>
      ))}
    </nav>
  )
}
