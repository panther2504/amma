import { App } from '@capacitor/app'
import { registerPlugin, type PluginListenerHandle } from '@capacitor/core'
import { get } from '../api/client'
import { isNative } from './localdb'

export interface AndroidRelease { versionCode: number; versionName: string; downloadUrl: string; notes?: string | null; publishedAt?: string | null }

interface AppUpdaterPlugin {
  canInstall(): Promise<{ allowed: boolean }>
  openInstallSettings(): Promise<void>
  downloadAndInstall(o: { url: string }): Promise<void>
  addListener(event: 'progress', fn: (e: { percent: number }) => void): Promise<PluginListenerHandle>
}
export const AppUpdater = registerPlugin<AppUpdaterPlugin>('AppUpdater')

export async function currentVersion() {
  if (!isNative()) return null
  const info = await App.getInfo()
  return { name: info.version, code: Number(info.build) || 0 }
}

/** Returns the newer release if one is available for this phone, otherwise null. */
export async function checkForUpdate(): Promise<AndroidRelease | null> {
  const cur = await currentVersion()
  if (!cur) return null
  const latest = await get<AndroidRelease>('/app/android')
  return latest.versionCode > cur.code ? latest : null
}

const LAST_CHECK = 'amma.update.lastCheck'
/** Throttles automatic checks (on start / resume) to once every few hours. */
export function dueForCheck(hours = 6) {
  try {
    const last = Number(localStorage.getItem(LAST_CHECK) ?? 0)
    if (Date.now() - last < hours * 3600_000) return false
    localStorage.setItem(LAST_CHECK, String(Date.now()))
  } catch { /* ignore */ }
  return true
}
