import { useEffect, useRef, useState } from 'react'
import { post } from '../api/client'
import { useAction, useKicks } from '../api/hooks'
import { Info, SecHead } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useToast } from '../ui/shell'
import { fmtDateTime, pad, vibrate } from '../lib/util'

const SESSION_KEY = 'amma.kick.session'

/** Kick counter. Shows the mother's own pattern only — no universal target — and never delays care. */
export function KickPage() {
  const kicks = useKicks()
  const toast = useToast()
  const [s, setS] = useState<{ n: number; start: number | null }>(() => {
    try { return JSON.parse(sessionStorage.getItem(SESSION_KEY) ?? '') } catch { return { n: 0, start: null } }
  })
  const [, tick] = useState(0)
  const btn = useRef<HTMLButtonElement>(null)
  const num = useRef<HTMLElement>(null)

  useEffect(() => { try { sessionStorage.setItem(SESSION_KEY, JSON.stringify(s)) } catch { /* ignore */ } }, [s])
  useEffect(() => { if (!s.start) return; const t = setInterval(() => tick(x => x + 1), 1000); return () => clearInterval(t) }, [s.start])

  const save = useAction(() => post('/health/kick-sessions', { startedAt: new Date(s.start!).toISOString(), endedAt: new Date().toISOString(), movements: s.n }), [['kicks']],
    () => { setS({ n: 0, start: null }); toast('👣 Session saved') })

  const elapsed = s.start ? Math.floor((Date.now() - s.start) / 1000) : 0
  const tap = () => {
    setS(p => ({ n: p.n + 1, start: p.start ?? Date.now() }))
    num.current?.animate([{ transform: 'scale(1.35)' }, { transform: 'scale(1)' }], { duration: 400, easing: 'cubic-bezier(.34,1.45,.5,1)' })
    vibrate(15)
    const b = btn.current
    if (!b) return
    ;['💗', '✨', '👣'].forEach((em, i) => {
      const sp = document.createElement('span'); sp.className = 'burst'; sp.textContent = em
      const a = Math.random() * Math.PI * 2, r = 90 + Math.random() * 30
      sp.style.cssText = `left:50%;top:50%;--x:${Math.cos(a) * r}px;--y:${Math.sin(a) * r}px;animation-delay:${i * 60}ms`
      b.appendChild(sp); setTimeout(() => sp.remove(), 1200)
    })
  }
  const last = kicks.data?.[0]

  return (
    <PageFrame title="Baby movements">
      <div className="kick-wrap">
        <button className="kick-btn" ref={btn} onClick={tap} aria-label={`Record a movement. ${s.n} so far`}>
          <span className="wave" /><span className="wave" /><b ref={num}>{s.n}</b><small>Tap for each movement</small>
        </button>
      </div>
      <div className="kstats">
        <div><b>{pad(Math.floor(elapsed / 60))}:{pad(elapsed % 60)}</b><small>Session time</small></div>
        <div><b>{last?.movements ?? '—'}</b><small>Last session</small></div>
      </div>
      <div style={{ display: 'flex', gap: 10, marginTop: 14 }}>
        <button className="btn block" disabled={save.isPending} onClick={() => (s.n ? save.mutate() : toast('Tap the circle when you feel a movement'))}>Save session</button>
        <button className="btn soft" onClick={() => setS({ n: 0, start: null })}>Reset</button>
      </div>
      <Info style={{ marginTop: 16 }}>Every baby has their own pattern — there is no single right number. Get to know what is normal for <b>your</b> baby.</Info>
      <Info warn style={{ marginTop: 10 }}>If movements <b>slow down, change or stop</b>, contact your maternity care team <b>right away</b>. Don't wait or rely on this counter.</Info>
      <SecHead title="Recent sessions" />
      <div className="card">
        {(kicks.data ?? []).slice(0, 8).map(k => (
          <div className="rowi" key={k.id}><div className="ico c-blush"><Icon name="foot" size={19} /></div>
            <div className="grow"><b>{k.movements} movements</b><small>{fmtDateTime(k.startedAt, { weekday: 'short', day: 'numeric', month: 'short', hour: 'numeric', minute: '2-digit' })}</small></div>
            <span className="timepill">{k.durationMinutes} min</span></div>
        ))}
        {!kicks.data?.length && <p className="small muted">No sessions yet.</p>}
      </div>
    </PageFrame>
  )
}
