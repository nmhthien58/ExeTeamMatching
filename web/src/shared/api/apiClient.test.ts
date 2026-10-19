import axios, { type AxiosAdapter, type InternalAxiosRequestConfig } from 'axios'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, createApiClient } from './apiClient'
import { tokenStore, type TokenResponse } from './tokenStore'

const user = { id: 'u1', email: 'a@fpt.edu.vn', fullName: 'Nguyễn Văn A', roles: ['Student' as const] }
const tokens = (access: string): TokenResponse => ({
  accessToken: access,
  accessTokenExpiresAt: '',
  refreshToken: `refresh-of-${access}`,
  refreshTokenExpiresAt: '',
  user,
})

/** Adapter giả: trả 401 khi token không phải `fresh`, ngược lại trả lại header Authorization. */
function fakeServer(calls: string[]): AxiosAdapter {
  return async (config: InternalAxiosRequestConfig) => {
    const auth = String(config.headers.Authorization ?? '')
    calls.push(`${config.url} ${auth}`)
    if (config.url === '/problem') {
      return Promise.reject(
        new axios.AxiosError('409', 'ERR_BAD_REQUEST', config, null, {
          status: 409,
          statusText: 'Conflict',
          headers: {},
          config,
          data: {
            type: 'https://exe-team-matching/errors/round-closed',
            title: 'Đợt đăng ký đã khóa',
            status: 409,
            traceId: 't-1',
            errors: { name: ['Bắt buộc'] },
          },
        }),
      )
    }
    if (auth !== 'Bearer fresh') {
      return Promise.reject(
        new axios.AxiosError('401', 'ERR_BAD_REQUEST', config, null, { status: 401, statusText: '', headers: {}, config, data: {} }),
      )
    }
    return { data: { ok: true }, status: 200, statusText: 'OK', headers: {}, config }
  }
}

afterEach(() => {
  tokenStore.clear()
  vi.restoreAllMocks()
})

describe('createApiClient', () => {
  it('refreshes once on 401 and retries all waiting requests with the new token', async () => {
    tokenStore.save(tokens('expired'))
    const refresh = vi.spyOn(axios, 'post').mockResolvedValue({ data: tokens('fresh') })
    const calls: string[] = []
    const client = createApiClient('http://gw')
    client.defaults.adapter = fakeServer(calls)

    const [a, b] = await Promise.all([client.get('/a'), client.get('/b')])

    expect(a.data).toEqual({ ok: true })
    expect(b.data).toEqual({ ok: true })
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(refresh).toHaveBeenCalledWith('http://gw/api/auth/refresh', { refreshToken: 'refresh-of-expired' })
    expect(tokenStore.getAccessToken()).toBe('fresh')
  })

  it('clears the session when refresh fails and surfaces 401 as ApiError', async () => {
    tokenStore.save(tokens('expired'))
    vi.spyOn(axios, 'post').mockRejectedValue(new Error('revoked'))
    const client = createApiClient('http://gw')
    client.defaults.adapter = fakeServer([])

    await expect(client.get('/a')).rejects.toMatchObject({ status: 401 })
    expect(tokenStore.getUser()).toBeNull()
  })

  it('maps ProblemDetails to ApiError with code, traceId and field errors', async () => {
    tokenStore.save(tokens('fresh'))
    const client = createApiClient('http://gw')
    client.defaults.adapter = fakeServer([])

    const error = await client.get('/problem').catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error).toMatchObject({ status: 409, code: 'round-closed', traceId: 't-1', fieldErrors: { name: ['Bắt buộc'] } })
  })

  it('reports network failures as status 0', async () => {
    const client = createApiClient('http://gw')
    client.defaults.adapter = async (config) => Promise.reject(new axios.AxiosError('Network Error', 'ERR_NETWORK', config))

    const error = (await client.get('/a').catch((e: unknown) => e)) as ApiError
    expect(error.isNetwork).toBe(true)
  })
})
