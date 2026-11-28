import { useEffect, useState } from 'react'
import { AppUpdater, type AndroidRelease } from '../lib/updates'
import { Icon } from './Icon'
import { useSheet } from './shell'

/** "Update available" sheet: downloads the new APK and opens Android's installer. */
export function UpdateSheet({ release }: { release: AndroidRelease }) {
  const sheet = useSheet()
  const [state, setState] = useState<'ready' | 'permission' | 'downloading' | 'installing' | 'error'>('ready')
  const [pct, setPct] = useState(0)
  const [err, setErr] = useState('')

  useEffect(() => {
    const sub = AppUpdater.addListener('progress', e => setPct(e.percent))
    return () => { sub.then(h => h.remove()) }
  }, [])

  const start = async () => {
    setErr('')
    const { allowed } = await AppUpdater.canInstall()
    if (!allowed) { setState('permission'); return }
    setState('downloading'); setPct(0)
    try {
      await AppUpdater.downloadAndInstall({ url: release.downloadUrl })
      setState('installing')
    } catch (e) {
      const msg = (e as Error).message
      if (msg.includes('INSTALL_NOT_ALLOWED')) setState('permission')
      else { setErr(msg); setState('error') }
    }
  }

  return (
    <>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: 52 }}>✨</div>
        <h3 style={{ marginTop: 6 }}>Update available</h3>
        <p className="small muted" style={{ marginTop: 4 }}>Amma {release.versionName}</p>
      </div>
      {state === 'ready' && <>
        <p className="small" style={{ margin: '14px 0', lineHeight: 1.6, color: 'var(--ink2)', textAlign: 'center' }}>A new version of Amma is ready. Your data is safe — it stays in your account.</p>
        <button className="btn block" onClick={start}><Icon name="upload" size={18} /> Update now</button>
        <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Later</button>
      </>}
      {state === 'permission' && <>
        <p className="small" style={{ margin: '14px 0', lineHeight: 1.6, color: 'var(--ink2)' }}>
          To update itself, Amma needs permission to install apps. Tap <b>Open settings</b>, turn on <b>Allow from this source</b>, then come back and tap <b>Try again</b>.
        </p>
        <button className="btn block" onClick={() => AppUpdater.openInstallSettings()}>Open settings</button>
        <button className="btn ghost block" style={{ marginTop: 10 }} onClick={start}>Try again</button>
      </>}
      {state === 'downloading' && <>
        <p className="small muted" style={{ margin: '16px 0 10px', textAlign: 'center' }}>Downloading… {pct >= 0 ? `${pct}%` : ''}</p>
        <div className="progress"><i style={{ width: `${Math.max(4, pct)}%` }} /></div>
      </>}
      {state === 'installing' && <>
        <p className="small" style={{ margin: '14px 0', lineHeight: 1.6, color: 'var(--ink2)', textAlign: 'center' }}>Tap <b>Update</b> on the Android screen to finish. Amma will restart with the new version.</p>
        <button className="btn ghost block" onClick={start}>Show the installer again</button>
      </>}
      {state === 'error' && <>
        <p className="small" style={{ margin: '14px 0', lineHeight: 1.6, color: 'var(--red-ink)', textAlign: 'center' }}>Couldn't download the update: {err}</p>
        <button className="btn block" onClick={start}>Try again</button>
        <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Later</button>
      </>}
    </>
  )
}
