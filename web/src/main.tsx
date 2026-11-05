import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import '@fontsource/be-vietnam-pro/400.css'
import '@fontsource/be-vietnam-pro/500.css'
import '@fontsource/be-vietnam-pro/600.css'
import 'material-symbols/rounded.css'
import './index.css'
import { App } from '@/app/App'
import { analytics } from '@/shared/firebase/analytics'

// FE-46: bật Firebase Analytics ngay khi mở web (no-op khi chưa cấu hình / khi test).
void analytics.init()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
