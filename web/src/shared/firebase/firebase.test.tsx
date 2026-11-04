import { act, render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { analytics } from './analytics'
import { firebaseConfigured, getFirebaseApp } from './firebase'
import { RemoteNotice } from './RemoteNotice'
import { __setRemoteConfigForTest } from './remoteConfig'

// FE-46 – Firebase trên web tắt êm khi chưa cấu hình; banner Remote Config hiện theo `web_notice`.
describe('Firebase web', () => {
  it('chưa có VITE_FIREBASE_*: không khởi tạo, Analytics không ném lỗi', async () => {
    expect(firebaseConfigured).toBe(false)
    await expect(getFirebaseApp()).resolves.toBeNull()
    await expect(analytics.logLogin()).resolves.toBeUndefined()
    await expect(analytics.setUserId('u1')).resolves.toBeUndefined()
  })

  it('web_notice rỗng: không có banner; có nội dung: hiện banner, đổi là cập nhật', () => {
    render(<RemoteNotice />)
    expect(screen.queryByText('Thông báo')).toBeNull()

    act(() => __setRemoteConfigForTest({ webNotice: 'Hệ thống bảo trì lúc 22:00.' }))
    expect(screen.getByText('Thông báo')).toBeTruthy()
    expect(screen.getByText('Hệ thống bảo trì lúc 22:00.')).toBeTruthy()

    act(() => __setRemoteConfigForTest({ webNotice: '' }))
    expect(screen.queryByText('Thông báo')).toBeNull()
  })
})
