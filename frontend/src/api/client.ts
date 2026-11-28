import type { AuthResponse } from './types'
import { localDb } from '../lib/localdb'

/** API origin. Empty for the web build (same server); set VITE_API_BASE for the Android app. */
export const API_BASE = (import.meta.env.VITE_API_BASE ?? '').replace(/\/$/, '')
const url = (p: string) => `${API_BASE}/api/v1${p}`

const REFRESH_KEY = 'amma.refresh'
const USER_KEY = 'session.user'
let accessToken: string | null = null
let refreshing: Promise<boolean> | null = null
const listeners = new Set<() => void>()

export class ApiError extends Error {
  status: number
  body?: unknown
  constructor(status: number, message: string, body?: unknown) { super(message); this.status = status; this.body = body }
}

/** A network failure (offline, or the server is unreachable). */
export class OfflineError extends Error {
  constructor() { super("You're offline. This needs an internet connection.") }
}

/** Returned for writes that were saved on the phone and will be sent when back online. */
export const QUEUED = Object.freeze({ __queued: true }) as unknown
export const isQueued = (r: unknown) => r === QUEUED

export const onSignedOut = (fn: () => void) => { listeners.add(fn); return () => { listeners.delete(fn) } }

export function setSession(auth: AuthResponse) {
  accessToken = auth.accessToken
  try { localStorage.setItem(REFRESH_KEY, auth.refreshToken) } catch { /* private mode */ }
  localDb().set(USER_KEY, JSON.stringify(auth.user)).catch(() => {})
}

export async function clearSession() {
  accessToken = null
  try { localStorage.removeItem(REFRESH_KEY) } catch { /* ignore */ }
  await localDb().clear().catch(() => {})
  listeners.forEach(l => l())
}

export const hasStoredSession = () => { try { return !!localStorage.getItem(REFRESH_KEY) } catch { return false } }

/** The last signed-in user, used to open the app offline. */
export async function cachedUser() {
  const raw = await localDb().get(USER_KEY).catch(() => null)
  return raw ? (JSON.parse(raw) as AuthResponse['user']) : null
}

async function fetchWithTimeout(input: string, init: RequestInit, ms: number) {
  const ctrl = new AbortController()
  const t = setTimeout(() => ctrl.abort(), ms)
  try { return await fetch(input, { ...init, signal: ctrl.signal }) }
  catch { throw new OfflineError() }
  finally { clearTimeout(t) }
}

/**
 * Exchanges the stored refresh token for a new access token (tokens rotate on every use).
 * Throws OfflineError when the server can't be reached, so the app can still open offline.
 */
export async function refreshSession(): Promise<AuthResponse | null> {
  let token: string | null = null
  try { token = localStorage.getItem(REFRESH_KEY) } catch { /* ignore */ }
  if (!token) return null
  // The free server sleeps when idle and can take up to a minute to wake up.
  const res = await fetchWithTimeout(url('/auth/refresh'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }) }, 75_000)
  if (!res.ok) { if (res.status === 401) await clearSession(); return null }
  const auth = (await res.json()) as AuthResponse
  setSession(auth)
  return auth
}

async function ensureFresh(): Promise<boolean> {
  refreshing ??= refreshSession().then(a => !!a, () => false).finally(() => { refreshing = null })
  return refreshing
}

export async function logout() {
  let token: string | null = null
  try { token = localStorage.getItem(REFRESH_KEY) } catch { /* ignore */ }
  if (token) await fetchWithTimeout(url('/auth/logout'), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ refreshToken: token }) }, 10_000).catch(() => {})
  await clearSession()
}

type Body = object | FormData | undefined
export interface ApiInit {
  method?: string
  body?: Body
  auth?: boolean
  /** Save the change on the phone if offline, and send it later (daily logging actions). */
  queueable?: boolean
  /** Fixed idempotency key (used when replaying the outbox). */
  idempotencyKey?: string
}

const newKey = () => (crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`)

export async function api<T>(path: string, init: ApiInit = {}): Promise<T> {
  const { method = 'GET', body, auth = true, queueable = false } = init
  const write = method !== 'GET'
  const key = write ? (init.idempotencyKey ?? newKey()) : undefined
  const doFetch = () => {
    const headers: Record<string, string> = { Accept: 'application/json' }
    if (body && !(body instanceof FormData)) headers['Content-Type'] = 'application/json'
    if (auth && accessToken) headers.Authorization = `Bearer ${accessToken}`
    if (key) headers['Idempotency-Key'] = key
    return fetchWithTimeout(url(path), { method, headers, body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined },
      queueable ? 20_000 : 75_000)
  }

  let res: Response
  try {
    if (auth && !accessToken && hasStoredSession()) await ensureFresh()
    res = await doFetch()
    if (res.status === 401 && auth && (await ensureFresh())) res = await doFetch()
  } catch (e) {
    if (e instanceof OfflineError && queueable && key && !(body instanceof FormData)) {
      await localDb().outboxAdd({ id: key, method, path, body: body ? JSON.stringify(body) : null, createdAt: Date.now(), attempts: 0 })
      outboxChanged()
      return QUEUED as T
    }
    throw e
  }
  if (res.status === 401 && auth) await clearSession()
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

// ---- outbox change notifications (for the "waiting to sync" indicator)
const outboxListeners = new Set<() => void>()
export const onOutboxChange = (fn: () => void) => { outboxListeners.add(fn); return () => { outboxListeners.delete(fn) } }
export const outboxChanged = () => outboxListeners.forEach(l => l())

export const get = <T>(p: string) => api<T>(p)
export const post = <T>(p: string, body?: Body) => api<T>(p, { method: 'POST', body })
export const put = <T>(p: string, body?: Body) => api<T>(p, { method: 'PUT', body })
export const patch = <T>(p: string, body?: Body) => api<T>(p, { method: 'PATCH', body })
export const del = <T>(p: string) => api<T>(p, { method: 'DELETE' })

/** Offline-capable writes for daily logging. */
export const queuePost = <T>(p: string, body?: Body) => api<T>(p, { method: 'POST', body, queueable: true })
export const queuePut = <T>(p: string, body?: Body) => api<T>(p, { method: 'PUT', body, queueable: true })
