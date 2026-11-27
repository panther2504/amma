import type { AuthResponse } from './types'

const REFRESH_KEY = 'amma.refresh'
let accessToken: string | null = null
let refreshing: Promise<boolean> | null = null
const listeners = new Set<() => void>()

export class ApiError extends Error {
  status: number
  body?: unknown
  constructor(status: number, message: string, body?: unknown) { super(message); this.status = status; this.body = body }
}

/** Notified when the session ends (refresh failed / logged out). */
export const onSignedOut = (fn: () => void) => { listeners.add(fn); return () => { listeners.delete(fn) } }

export function setSession(auth: AuthResponse) {
  accessToken = auth.accessToken
  try { localStorage.setItem(REFRESH_KEY, auth.refreshToken) } catch { /* private mode */ }
}

export function clearSession() {
  accessToken = null
  try { localStorage.removeItem(REFRESH_KEY) } catch { /* ignore */ }
  listeners.forEach(l => l())
}

export const hasStoredSession = () => { try { return !!localStorage.getItem(REFRESH_KEY) } catch { return false } }

/** Exchanges the stored refresh token for a new access token (tokens rotate on every use). */
export async function refreshSession(): Promise<AuthResponse | null> {
  let token: string | null = null
  try { token = localStorage.getItem(REFRESH_KEY) } catch { /* ignore */ }
  if (!token) return null
  const res = await fetch('/api/v1/auth/refresh', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }) })
  if (!res.ok) { if (res.status === 401) clearSession(); return null }
  const auth = (await res.json()) as AuthResponse
  setSession(auth)
  return auth
}

async function ensureFresh(): Promise<boolean> {
  refreshing ??= refreshSession().then(a => !!a).finally(() => { refreshing = null })
  return refreshing
}

export async function logout() {
  let token: string | null = null
  try { token = localStorage.getItem(REFRESH_KEY) } catch { /* ignore */ }
  if (token) await fetch('/api/v1/auth/logout', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }) }).catch(() => {})
  clearSession()
}

type Body = object | FormData | undefined

export async function api<T>(path: string, init: { method?: string; body?: Body; auth?: boolean } = {}): Promise<T> {
  const { method = 'GET', body, auth = true } = init
  const doFetch = () => {
    const headers: Record<string, string> = { Accept: 'application/json' }
    if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json'
    if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`
    return fetch(`/api/v1${path}`, { method, headers, body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined })
  }
  let res = await doFetch()
  if (res.status === 401 && auth && (await ensureFresh())) res = await doFetch()
  if (res.status === 401 && auth) clearSession()
  if (!res.ok) {
    let detail = res.statusText || 'Something went wrong'
    let parsed: unknown
    try {
      parsed = await res.json()
      const p = parsed as { detail?: string; title?: string; errors?: Record<string, string[]> }
      detail = p.detail ?? Object.values(p.errors ?? {})[0]?.[0] ?? p.title ?? detail
    } catch { /* not json */ }
    throw new ApiError(res.status, detail, parsed)
  }
  if (res.status === 204) return undefined as T
  const type = res.headers.get('content-type') ?? ''
  return (type.includes('json') ? await res.json() : await res.blob()) as T
}

export const get = <T>(p: string) => api<T>(p)
export const post = <T>(p: string, body?: Body) => api<T>(p, { method: 'POST', body })
export const put = <T>(p: string, body?: Body) => api<T>(p, { method: 'PUT', body })
export const patch = <T>(p: string, body?: Body) => api<T>(p, { method: 'PATCH', body })
export const del = <T>(p: string) => api<T>(p, { method: 'DELETE' })
