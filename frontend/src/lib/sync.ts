import type { QueryClient } from '@tanstack/react-query'
import { ApiError, OfflineError, api, outboxChanged } from '../api/client'
import { localDb } from './localdb'

let running: Promise<number> | null = null

/**
 * Sends changes saved while offline, oldest first. Each keeps its original Idempotency-Key, so a change
 * that already reached the server is not applied twice. Stops at the first network failure.
 * Returns how many changes were sent.
 */
export function flushOutbox(qc?: QueryClient): Promise<number> {
  running ??= (async () => {
    const db = localDb()
    let sent = 0
    for (const item of await db.outboxList()) {
      try {
        await api(item.path, { method: item.method, body: item.body ? JSON.parse(item.body) : undefined, idempotencyKey: item.id })
        await db.outboxRemove(item.id)
        sent++
      } catch (e) {
        if (e instanceof OfflineError) break
        // The server rejected it (e.g. invalid after other changes): keep a few retries for server errors, then drop.
        if (e instanceof ApiError && e.status < 500) await db.outboxRemove(item.id)
        else if (item.attempts >= 5) await db.outboxRemove(item.id)
        else { await db.outboxBump(item.id); break }
      }
    }
    outboxChanged()
    if (sent && qc) await qc.invalidateQueries()
    return sent
  })().finally(() => { running = null })
  return running
}

export const pendingCount = async () => (await localDb().outboxList()).length
