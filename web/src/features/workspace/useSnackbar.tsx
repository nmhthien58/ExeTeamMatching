import { useEffect, useState } from 'react'
import { Button } from '@/shared/components/ui'

/** Snackbar (DESIGN.md 6.1, 6.4): chỉ báo kết quả thành công; có thể kèm "Hoàn tác" trong 5 giây. */
export function useSnackbar() {
  const [state, setState] = useState<{ message: string; undo?: () => void; key: number } | null>(null)
  useEffect(() => {
    if (!state) return
    const timer = window.setTimeout(() => setState(null), state.undo ? 5000 : 4000)
    return () => window.clearTimeout(timer)
  }, [state])
  const show = (message: string, undo?: () => void) => setState({ message, undo, key: Date.now() })
  const element = state && (
    <div
      key={state.key}
      role="status"
      className="fixed bottom-24 left-1/2 z-50 flex w-[min(560px,calc(100vw-32px))] -translate-x-1/2 items-center gap-4 rounded-md bg-text-primary px-4 py-3 text-white shadow-elevation-2 sm:bottom-6"
    >
      <span className="flex-1">{state.message}</span>
      {state.undo && (
        <Button
          variant="text"
          className="text-primary-border hover:bg-white/10"
          onClick={() => {
            state.undo?.()
            setState(null)
          }}
        >
          Hoàn tác
        </Button>
      )}
    </div>
  )
  return { show, element }
}
