import { Capacitor } from '@capacitor/core'
import { CapacitorSQLite, SQLiteConnection, type SQLiteDBConnection } from '@capacitor-community/sqlite'

/**
 * On-device storage. On Android it is a real SQLite database (survives restarts and works offline);
 * in a browser it falls back to localStorage. Holds the cached server data and the outbox of
 * changes made while offline.
 */
export interface OutboxItem { id: string; method: string; path: string; body: string | null; createdAt: number; attempts: number }

interface Store {
  get(key: string): Promise<string | null>
  set(key: string, value: string): Promise<void>
  remove(key: string): Promise<void>
  outboxAdd(item: OutboxItem): Promise<void>
  outboxList(): Promise<OutboxItem[]>
  outboxRemove(id: string): Promise<void>
  outboxBump(id: string): Promise<void>
  clear(): Promise<void>
}

class SqliteStore implements Store {
  private db: Promise<SQLiteDBConnection>
  ready: Promise<SQLiteDBConnection>
  constructor() {
    this.ready = this.db = (async () => {
      const sqlite = new SQLiteConnection(CapacitorSQLite)
      const existing = (await sqlite.checkConnectionsConsistency()).result && (await sqlite.isConnection('amma', false)).result
      const db = existing ? await sqlite.retrieveConnection('amma', false) : await sqlite.createConnection('amma', false, 'no-encryption', 1, false)
      await db.open()
      await db.execute(`
        CREATE TABLE IF NOT EXISTS kv (key TEXT PRIMARY KEY NOT NULL, value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS outbox (id TEXT PRIMARY KEY NOT NULL, method TEXT NOT NULL, path TEXT NOT NULL, body TEXT, created_at INTEGER NOT NULL, attempts INTEGER NOT NULL DEFAULT 0);`)
      return db
    })()
  }
  async get(key: string) { const r = await (await this.db).query('SELECT value FROM kv WHERE key = ?', [key]); return (r.values?.[0]?.value as string) ?? null }
  async set(key: string, value: string) { await (await this.db).run('INSERT OR REPLACE INTO kv (key, value) VALUES (?, ?)', [key, value]) }
  async remove(key: string) { await (await this.db).run('DELETE FROM kv WHERE key = ?', [key]) }
  async outboxAdd(i: OutboxItem) { await (await this.db).run('INSERT OR IGNORE INTO outbox (id, method, path, body, created_at, attempts) VALUES (?, ?, ?, ?, ?, 0)', [i.id, i.method, i.path, i.body, i.createdAt]) }
  async outboxList() {
    const r = await (await this.db).query('SELECT id, method, path, body, created_at, attempts FROM outbox ORDER BY created_at')
    return (r.values ?? []).map(v => ({ id: v.id, method: v.method, path: v.path, body: v.body, createdAt: v.created_at, attempts: v.attempts }) as OutboxItem)
  }
  async outboxRemove(id: string) { await (await this.db).run('DELETE FROM outbox WHERE id = ?', [id]) }
  async outboxBump(id: string) { await (await this.db).run('UPDATE outbox SET attempts = attempts + 1 WHERE id = ?', [id]) }
  async clear() { const db = await this.db; await db.execute('DELETE FROM kv; DELETE FROM outbox;') }
}

class WebStore implements Store {
  private k = (key: string) => `amma.db.${key}`
  private read = (): OutboxItem[] => { try { return JSON.parse(localStorage.getItem(this.k('outbox')) ?? '[]') } catch { return [] } }
  private write = (items: OutboxItem[]) => { try { localStorage.setItem(this.k('outbox'), JSON.stringify(items)) } catch { /* quota */ } }
  async get(key: string) { try { return localStorage.getItem(this.k(key)) } catch { return null } }
  async set(key: string, value: string) { try { localStorage.setItem(this.k(key), value) } catch { /* quota */ } }
  async remove(key: string) { try { localStorage.removeItem(this.k(key)) } catch { /* ignore */ } }
  async outboxAdd(i: OutboxItem) { const all = this.read(); if (!all.some(x => x.id === i.id)) this.write([...all, i]) }
  async outboxList() { return this.read() }
  async outboxRemove(id: string) { this.write(this.read().filter(x => x.id !== id)) }
  async outboxBump(id: string) { this.write(this.read().map(x => (x.id === id ? { ...x, attempts: x.attempts + 1 } : x))) }
  async clear() { try { Object.keys(localStorage).filter(k => k.startsWith('amma.db.')).forEach(k => localStorage.removeItem(k)) } catch { /* ignore */ } }
}

/** Uses SQLite on Android, and falls back to browser storage if SQLite is unavailable. */
class SafeStore implements Store {
  private pick: Promise<Store>
  constructor() {
    const web = new WebStore()
    if (!Capacitor.isNativePlatform()) { this.pick = Promise.resolve(web); return }
    this.pick = (async () => {
      try { const s = new SqliteStore(); await s.ready; return s as Store }
      catch (e) { console.warn('SQLite unavailable, using browser storage', e); return web }
    })()
  }
  async get(k: string) { return (await this.pick).get(k) }
  async set(k: string, v: string) { return (await this.pick).set(k, v) }
  async remove(k: string) { return (await this.pick).remove(k) }
  async outboxAdd(i: OutboxItem) { return (await this.pick).outboxAdd(i) }
  async outboxList() { return (await this.pick).outboxList() }
  async outboxRemove(id: string) { return (await this.pick).outboxRemove(id) }
  async outboxBump(id: string) { return (await this.pick).outboxBump(id) }
  async clear() { return (await this.pick).clear() }
}

let store: Store | null = null
export const localDb = (): Store => (store ??= new SafeStore())

export const isNative = () => Capacitor.isNativePlatform()
