import { useEffect, useState } from 'react'
import { isQueued, queuePost } from '../api/client'
import { patchCache, useAction, useAppointments, useDay, usePerson, usePregnancy } from '../api/hooks'
import type { Day, Occurrence, ReminderStatus } from '../api/types'
import { Ring, SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { usePages, useToast } from '../ui/shell'
import { KIND_ICON, clearOccurrence, notificationStatus, openExactAlarmSettings, requestNotificationPermission } from '../lib/reminders'
import { daysUntil, fmt, hhmm, ordinal, parseDate, relDays, t12, vibrate } from '../lib/util'

const KIND_COLOR: Record<string, string> = { water: 'c-sky', meal: 'c-peach', snack: 'c-sage', juice: 'c-amber', tablet: 'c-lav', other: 'c-blush' }
const minutes = (t: string) => { const [h, m] = t.split(':').map(Number); return h * 60 + m }

export function useMarkReminder() {
  const toast = useToast()
  return useAction(
    (a: { o: Occurrence; status: ReminderStatus | null }) => queuePost(`/reminders/${a.o.reminderId}/log`, { time: hhmm(a.o.time), status: a.status }),
    [['day']],
    (r, a) => {
      if (a.status) clearOccurrence(a.o.reminderId, hhmm(a.o.time))
      if (a.status === 'done') toast(isQueued(r) ? '✅ Done · will sync when online' : '✅ Done — the other phone will see it')
    },
    (qc, a) => patchCache<Day>(qc, ['day'], d => {
      const items = d.items.map(i => (i.reminderId === a.o.reminderId && i.time === a.o.time ? { ...i, status: a.status, byName: a.status ? 'You' : null, at: new Date().toISOString() } : i))
      return { ...d, items, done: items.filter(i => i.status === 'done').length }
    }),
  )
}

function Row({ o, now }: { o: Occurrence; now: number }) {
  const mark = useMarkReminder()
  const due = minutes(o.time) <= now
  const late = !o.status && now - minutes(o.time) >= 15 && now - minutes(o.time) <= 120
  return (
    <div className="rowi" style={late ? { background: 'linear-gradient(90deg,#FFF5F5,transparent)', margin: '0 -16px', padding: '12px 16px' } : undefined}>
      <div className={`ico ${KIND_COLOR[o.kind]}`} style={{ fontSize: 22 }}>{KIND_ICON[o.kind]}</div>
      <div className="grow">
        <b style={o.status ? { color: 'var(--muted)' } : undefined}>{o.kind === 'water' ? 'Water' : o.title}</b>
        {o.status
          ? <small style={{ color: o.status === 'done' ? 'var(--sage-ink)' : 'var(--amber-ink)', fontWeight: 600 }}>{o.status === 'done' ? '✓ Done' : 'Skipped'} by {o.byName ?? '—'}{o.at ? ` · ${new Date(o.at).toLocaleTimeString('en-IN', { hour: 'numeric', minute: '2-digit' })}` : ''}</small>
          : <small>{o.note || (late ? 'Not done yet — please remind her' : due ? 'Due now' : 'Coming up')}</small>}
      </div>
      <span className="timepill" style={late ? { background: 'var(--red)', color: 'var(--red-ink)' } : undefined}>{t12(o.time)}</span>
      {o.status
        ? <button className="chk on" style={o.status === 'skipped' ? { background: 'var(--amber)', borderColor: '#EBCB8F', color: 'var(--amber-ink)' } : undefined}
            aria-label="Undo" onClick={() => mark.mutate({ o, status: null })}><Icon name={o.status === 'done' ? 'check' : 'x'} size={15} stroke={3} /></button>
        : <button className="btn sm" style={{ height: 38, padding: '0 14px' }} onClick={() => { vibrate(10); mark.mutate({ o, status: 'done' }) }}>Done</button>}
    </div>
  )
}

function SetupBanner() {
  const [st, setSt] = useState<{ permission: boolean; exact: boolean } | null>(null)
  useEffect(() => { notificationStatus().then(setSt).catch(() => {}) }, [])
  if (!st || (st.permission && st.exact)) return null
  return (
    <div className="info warn" style={{ marginBottom: 14 }}>
      <Icon name="bell" size={18} />
      <span>
        {!st.permission ? <>Reminders can't ring until you allow notifications. </> : <>Allow <b>Alarms &amp; reminders</b> so reminders ring exactly on time. </>}
        <button style={{ color: 'inherit', fontWeight: 800, textDecoration: 'underline' }}
          onClick={async () => { if (!st.permission) await requestNotificationPermission(); else await openExactAlarmSettings(); setSt(await notificationStatus()) }}>
          {st.permission ? 'Open setting' : 'Allow'}
        </button>
      </span>
    </div>
  )
}

export function TodayTab({ go }: { go: (tab: string) => void }) {
  const { open } = usePages()
  const day = useDay()
  const person = usePerson()
  const preg = usePregnancy()
  const appts = useAppointments()
  const [now, setNow] = useState(() => new Date().getHours() * 60 + new Date().getMinutes())
  useEffect(() => { const t = setInterval(() => setNow(new Date().getHours() * 60 + new Date().getMinutes()), 30_000); return () => clearInterval(t) }, [])

  const d = day.data
  const items = d?.items ?? []
  const pending = items.filter(i => !i.status && minutes(i.time) <= now && now - minutes(i.time) <= 120)
  const missed = items.filter(i => !i.status && now - minutes(i.time) > 120)
  const [showMissed, setShowMissed] = useState(false)
  const upcoming = items.filter(i => !i.status && minutes(i.time) > now)
  const finished = items.filter(i => i.status)
  const water = items.filter(i => i.kind === 'water')
  const waterDone = water.filter(i => i.status === 'done').length
  const tablets = items.filter(i => i.kind === 'tablet')
  const next = (appts.data ?? []).filter(a => a.status === 'scheduled' && daysUntil(a.date) >= 0).sort((a, b) => (a.date + a.time).localeCompare(b.date + b.time))[0]
  const g = preg.data?.gestation
  const hr = new Date().getHours()

  return (
    <>
      <header className="top">
        <div className="hello">
          <div className="avatar">{(person.data?.name ?? 'A')[0]?.toUpperCase()}</div>
          <div><small>{hr < 12 ? 'Good morning' : hr < 17 ? 'Good afternoon' : 'Good evening'} 👋</small><h1>{person.data?.name ?? ' '}</h1></div>
        </div>
        <button className="icon-btn" onClick={() => open('reminders')} aria-label="Edit reminders"><Icon name="edit" size={19} /></button>
      </header>
      <SetupBanner />

      {!d ? <Skeleton h={170} mt={0} /> : (
        <section className="hero">
          <span className="blob b1" /><span className="blob b2" />
          <div className="hero-top">
            <Ring pct={d.total ? Math.round((d.done / d.total) * 100) : 0}><b>{d.done}</b><span>of {d.total}</span></Ring>
            <div className="hero-info">
              {g && <span className="chip-glass">Week {g.weeks} · {ordinal(g.trimester)} trimester</span>}
              <h2>{pending.length ? `${pending.length} reminder${pending.length > 1 ? 's' : ''} due now` : upcoming[0] ? `Next: ${upcoming[0].kind === 'water' ? 'water' : upcoming[0].title} at ${t12(upcoming[0].time)}` : 'All done for today 💗'}</h2>
              <p>{fmt(parseDate(d.date), { weekday: 'long', day: 'numeric', month: 'long' })}</p>
            </div>
          </div>
          <div className="hero-stats">
            <div><small>Water</small><b>{waterDone}/{water.length} 💧</b></div>
            <div><small>Tablets</small><b>{tablets.filter(i => i.status === 'done').length}/{tablets.length}</b></div>
            <div><small>{g ? 'Days to go' : 'Done'}</small><b>{g ? g.daysToGo : d.done}</b></div>
          </div>
        </section>
      )}

      {pending.length > 0 && <>
        <SecHead title="Now" right={<span className="small" style={{ color: 'var(--red-ink)', fontWeight: 700 }}>{pending.length} to do</span>} />
        <div className="card">{pending.map(o => <Row key={o.reminderId + o.time} o={o} now={now} />)}</div>
      </>}

      {next && <>
        <SecHead title="Next checkup" action="See all" onAction={() => go('checkups')} />
        <button className="card appt tap" style={{ width: '100%', textAlign: 'left' }} onClick={() => go('checkups')}>
          <div className="datebox"><b>{parseDate(next.date).getDate()}</b><small>{fmt(next.date, { month: 'short' })}</small></div>
          <div className="grow"><b style={{ fontSize: 15 }}>{next.title}</b><small className="muted" style={{ display: 'block', marginTop: 3 }}>{next.doctorName ?? next.location ?? ''} · {t12(next.time)}</small>
            <span className="tag c-lav" style={{ marginTop: 8 }}><Icon name="clock" size={12} /> {relDays(daysUntil(next.date))}</span></div>
          <Icon name="chev" size={20} />
        </button>
      </>}

      {missed.length > 0 && <>
        <SecHead title="Missed earlier" right={<button className="small" style={{ color: 'var(--primary)', fontWeight: 700 }} onClick={() => setShowMissed(!showMissed)}>{showMissed ? 'Hide' : `Show ${missed.length}`}</button>} />
        {showMissed && <div className="card">{missed.map(o => <Row key={o.reminderId + o.time} o={o} now={now} />)}</div>}
      </>}

      {upcoming.length > 0 && <>
        <SecHead title="Later today" />
        <div className="card">{upcoming.map(o => <Row key={o.reminderId + o.time} o={o} now={now} />)}</div>
      </>}

      {finished.length > 0 && <>
        <SecHead title="Done today" right={<span className="small muted">{finished.length}</span>} />
        <div className="card">{finished.map(o => <Row key={o.reminderId + o.time} o={o} now={now} />)}</div>
      </>}

      {d && !items.length && <div className="card empty"><div>⏰</div><p className="small muted" style={{ marginTop: 6 }}>No reminders yet.</p>
        <button className="btn sm ghost" style={{ marginTop: 10 }} onClick={() => open('reminders')}>Add reminders</button></div>}

      <p className="small muted" style={{ textAlign: 'center', marginTop: 22, lineHeight: 1.5 }}>Both phones ring for every reminder. If nobody taps Done, they ring again after 15 minutes.</p>
    </>
  )
}
