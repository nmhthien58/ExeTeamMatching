import { createContext, useContext, useEffect, useState, type ReactNode } from 'react'

interface HeaderState {
  title: string
  action?: ReactNode
}

const HeaderContext = createContext<{ state: HeaderState; set: (s: HeaderState) => void } | null>(null)

export function PageHeaderProvider({ children }: { children: ReactNode }) {
  const [state, set] = useState<HeaderState>({ title: '' })
  return <HeaderContext.Provider value={{ state, set }}>{children}</HeaderContext.Provider>
}

export function usePageHeaderState() {
  return useContext(HeaderContext)?.state ?? { title: '' }
}

/**
 * Màn hình khai báo tiêu đề và nút hành động chính; khung trang (W25 / khung vai trò) hiển thị ở header 64 px.
 * <code>&lt;PageHeader title="Sinh viên" action={&lt;Button icon="upload"&gt;Import&lt;/Button&gt;} /&gt;</code>
 */
export function PageHeader({ title, action }: HeaderState) {
  const ctx = useContext(HeaderContext)
  useEffect(() => {
    ctx?.set({ title, action })
    document.title = `${title} · EXE Team Matching`
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [title, action])
  return null
}
