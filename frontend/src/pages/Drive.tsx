import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { del, get, post, put } from '../api/client'
import { Info, SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useToast } from '../ui/shell'
import { isNative } from '../lib/localdb'

export interface DriveStatus { configured: boolean; connected: boolean; email?: string | null; clientId?: string | null; photosInDrive: number; photosOnServer: number; redirectUri: string }
export const useDrive = () => useQuery({ queryKey: ['drive'], queryFn: () => get<DriveStatus>('/drive'), refetchOnWindowFocus: 'always' })

const STEPS: [string, React.ReactNode][] = [
  ['Create a project', <>Open <b>console.cloud.google.com</b> (sign in with the Google account whose Drive you want to use). Click the project picker → <b>New project</b> → name it <b>Amma</b> → Create.</>],
  ['Turn on Google Drive', <>Search for <b>Google Drive API</b> at the top → open it → <b>Enable</b>.</>],
  ['Set up sign-in', <>Go to <b>Google Auth Platform</b> (or "OAuth consent screen") → <b>Get started</b>: app name <b>Amma</b>, your e-mail, audience <b>External</b> → Create. Then open <b>Audience</b> and click <b>Publish app</b> (so the connection doesn't expire after 7 days).</>],
  ['Create the app ID', <>Open <b>Clients</b> → <b>Create client</b> → type <b>Web application</b>. Under <b>Authorised redirect URIs</b> add the address below, then <b>Create</b>. Copy the <b>Client ID</b> and <b>Client secret</b>.</>],
]

export function DrivePage() {
  const q = useDrive()
  const qc = useQueryClient()
  const toast = useToast()
  const [id, setId] = useState('')
  const [secret, setSecret] = useState('')
  const [busy, setBusy] = useState(false)
  const [moving, setMoving] = useState<string | null>(null)
  const s = q.data

  const connect = async () => {
    setBusy(true)
    try {
      if (id.trim() || secret.trim() || !s?.configured) await put('/drive/config', { clientId: id.trim(), clientSecret: secret.trim() })
      const { url } = await get<{ url: string }>('/drive/connect-url')
      toast('Opening Google sign-in…')
      // On the phone this opens the browser (Google doesn't allow signing in inside apps); come back to Amma afterwards.
      if (isNative()) window.location.href = url
      else window.open(url, '_blank')
      qc.invalidateQueries({ queryKey: ['drive'] })
    } catch (e) { toast((e as Error).message) } finally { setBusy(false) }
  }

  const moveAll = async () => {
    setMoving('Starting…')
    try {
      for (let i = 0; i < 400; i++) {
        const r = await post<{ moved: number; remaining: number }>('/drive/move-existing')
        setMoving(`${r.remaining} left…`)
        if (r.remaining === 0 || r.moved === 0) break
      }
      toast('📁 Photos moved to Google Drive')
    } catch (e) { toast((e as Error).message) } finally { setMoving(null); qc.invalidateQueries({ queryKey: ['drive'] }) }
  }

  const copy = async (text: string) => { try { await navigator.clipboard.writeText(text); toast('Copied') } catch { toast(text) } }

  return (
    <PageFrame title="Google Drive">
      {!s ? <Skeleton h={200} mt={0} /> : s.connected ? (
        <>
          <div className="card" style={{ background: 'linear-gradient(135deg,var(--sage),#F2FAF5)' }}>
            <b style={{ fontSize: 16 }}>✓ Connected</b>
            <p className="small" style={{ marginTop: 6, color: 'var(--ink2)', lineHeight: 1.55 }}>New memory photos are saved in the <b>Amma Memories</b> folder{s.email ? <> of <b>{s.email}</b></> : null}. You can also see them in the Google Drive app.</p>
            <p className="small muted" style={{ marginTop: 8 }}>{s.photosInDrive} photos in Drive · {s.photosOnServer} on the server</p>
          </div>
          {s.photosOnServer > 0 && (
            <button className="btn block" style={{ marginTop: 12 }} disabled={!!moving} onClick={moveAll}>
              <Icon name="upload" size={18} /> {moving ? `Moving… ${moving}` : `Move ${s.photosOnServer} older photo${s.photosOnServer > 1 ? 's' : ''} to Drive`}
            </button>
          )}
          <Info style={{ marginTop: 12 }}>Amma can only see the files it puts in Drive — nothing else in your Drive.</Info>
          <button className="btn ghost block" style={{ marginTop: 12 }} onClick={async () => { await del('/drive'); qc.invalidateQueries({ queryKey: ['drive'] }); toast('Google Drive disconnected') }}>Disconnect</button>
        </>
      ) : (
        <>
          <Info>Keep full-size photos in your own Google Drive (15 GB free). Do this once, on one phone — it works for both of you.</Info>
          {STEPS.map(([t, body], i) => (
            <div className="card" key={t} style={{ marginTop: 12 }}>
              <b style={{ fontSize: 14.5 }}>{i + 1}. {t}</b>
              <p className="small" style={{ marginTop: 6, lineHeight: 1.6, color: 'var(--ink2)' }}>{body}</p>
              {i === 3 && (
                <button className="small" style={{ marginTop: 8, display: 'block', textAlign: 'left', wordBreak: 'break-all', background: 'var(--bg)', borderRadius: 10, padding: '8px 10px', fontWeight: 700 }} onClick={() => copy(s.redirectUri)}>
                  {s.redirectUri} <span style={{ color: 'var(--primary)' }}>· tap to copy</span>
                </button>
              )}
            </div>
          ))}
          <SecHead title="5. Paste and connect" />
          <div className="card">
            <label className="label" style={{ marginTop: 0 }} htmlFor="gid">Client ID</label>
            <input className="field" id="gid" placeholder={s.clientId ?? '…apps.googleusercontent.com'} value={id} onChange={e => setId(e.target.value)} autoComplete="off" />
            <label className="label" htmlFor="gsec">Client secret</label>
            <input className="field" id="gsec" type="password" placeholder={s.configured ? 'Saved — enter again only to change' : 'GOCSPX-…'} value={secret} onChange={e => setSecret(e.target.value)} autoComplete="off" />
            <button className="btn block" style={{ marginTop: 16 }} disabled={busy || (!s.configured && (!id.trim() || !secret.trim()))} onClick={connect}>
              {busy ? 'Please wait…' : 'Connect Google Drive'}
            </button>
            <p className="small muted" style={{ marginTop: 10, lineHeight: 1.5 }}>Google opens in your browser. Choose your account and tap <b>Continue</b>. If it says "Google hasn't verified this app", tap <b>Advanced → Go to Amma</b> — it's your own app. Then come back to Amma.</p>
          </div>
        </>
      )}
    </PageFrame>
  )
}
