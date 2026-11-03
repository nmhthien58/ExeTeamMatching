import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LecturerPreferencesPage } from './LecturerPreferencesPage'

const get = vi.fn()
const put = vi.fn()
vi.mock('@/shared/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/shared/api/apiClient')>()),
  api: { get: (...args: unknown[]) => get(...args), put: (...args: unknown[]) => put(...args) },
}))

const lecturer = (id: string, name: string, rank: number) => ({ lecturerId: id, fullName: name, domains: [], rank, isInterested: false })
const preferences = { maxPreferences: 3, locked: false, lockAt: null, preferences: [lecturer('a', 'Thầy An', 1), lecturer('b', 'Cô Bình', 2)], interestedLecturers: [], previousInstructor: null }

describe('S23 ranked lecturer preferences (FE-42)', () => {
  beforeEach(() => {
    get.mockReset()
    put.mockReset().mockResolvedValue({ data: preferences })
    get.mockImplementation(async (url: string) => {
      if (url === '/api/teams/me') return { data: { id: 'team-1', myRelation: 'LEADER' } }
      if (url === '/api/teams/team-1/lecturer-preferences') return { data: preferences }
      if (url === '/api/lecturers') return { data: { items: [] } }
      throw new Error(`Unexpected GET ${url}`)
    })
  })

  it('sends the reordered IDs in rank order', async () => {
    const user = userEvent.setup()
    render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><MemoryRouter><LecturerPreferencesPage /></MemoryRouter></QueryClientProvider>)
    await screen.findByText('Thầy An')
    await user.click(screen.getByRole('button', { name: 'Đưa Cô Bình lên' }))
    expect(put).toHaveBeenCalledWith('/api/teams/team-1/lecturer-preferences', { lecturerIds: ['b', 'a'] })
  })
})
