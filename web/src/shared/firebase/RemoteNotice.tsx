import { useEffect } from 'react'
import { Banner } from '@/shared/components/ui'
import { startRemoteConfig, useRemoteConfig } from './remoteConfig'

/** Banner thông báo toàn web từ Remote Config `web_notice` (FE-46); rỗng thì không hiện gì. */
export function RemoteNotice() {
  const { webNotice } = useRemoteConfig()
  useEffect(() => {
    void startRemoteConfig()
  }, [])
  if (!webNotice.trim()) return null
  return (
    <div className="pt-4">
      <Banner title="Thông báo">{webNotice}</Banner>
    </div>
  )
}
