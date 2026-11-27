import { useEffect, useState } from 'react'
import { post, put } from '../api/client'
import { useAction, useDashboard } from '../api/hooks'
import type { Appointment, Dashboard, Dose, User } from '../api/types'
import { Check, Ring, SecHead, Skeleton, colorClass } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { useApp, usePages, useSheet, useToast } from '../ui/shell'
import { NotificationsSheet } from '../pages/Notifications'
import { daysUntil, fmt, hhmm, ordinal, parseDate, relDays, t12, vibrate } from '../lib/util'

export function useDoseToggle() {
  const toast = useToast()
  return useAction(
    (d: { dose: Dose; status: Dose['status'] }) => post(`/medications/${d.dose.medicationId}/log`, { time: hhmm(d.dose.time), status: d.status }),
    [['dashboard'], ['doses']],
    (_, d) => { if (d.status === 'taken') toast('✅ Marked as taken'); else if (d.status === 'skipped') toast('Marked as skipped') },
  )
}

export function MedRow({ dose }: { dose: Dose }) {
  const toggle = useDoseToggle()
  const next = dose.status === 'taken' ? null : 'taken'
  return (
    <div className="rowi">
      <div className={`ico ${colorClass(dose.colorKey)}`}><Icon name="pill" size={20} /></div>
      <div className="grow"><b>{dose.name}</b><small>{dose.instructions || dose.dosage}</small></div>
      <span className="timepill">{t12(dose.time)}</span>
      <Check on={dose.status === 'taken'} skip={dose.status === 'skipped'} label={`Mark ${dose.name} as taken`} onClick={() => toggle.mutate({ dose, status: next })} />
    </div>
  )
}

export function ApptCard({ a }: { a: Appointment }) {
  const { open } = usePages()
  const d = parseDate(a.date)
  return (
    <button className="card appt tap" style={{ width: '100%', textAlign: 'left' }} onClick={() => open('appts')}>
      <div className="datebox"><b>{d.getDate()}</b><small>{fmt(d, { month: 'short' })}</small></div>
      <div className="grow">
        <b style={{ fontSize: 15 }}>{a.title}</b>
        <small className="muted" style={{ display: 'block', marginTop: 3 }}>{a.doctorName ?? '—'} · {t12(a.time)}</small>
        <span className="tag c-lav" style={{ marginTop: 8 }}><Icon name="clock" size={12} /> {relDays(daysUntil(a.date))}</span>
      </div>
      <Icon name="chev" size={20} />
    </button>
  )
}

function WaterCard({ water }: { water: Dashboard['water'] }) {
  const toast = useToast()
  const [n, setN] = useState(water.glasses)
  useEffect(() => setN(water.glasses), [water.glasses])
  const save = useAction((g: number) => put('/tracking/water', { glasses: g }), [['dashboard'], ['health']])
  const change = (d: number) => {
    const v = Math.max(0, Math.min(20, n + d))
    setN(v); save.mutate(v); vibrate(8)
    if (d > 0 && v === water.goal) toast('💧 Daily water goal reached!')
  }
  const [h, setH] = useState(0)
  useEffect(() => { const r = requestAnimationFrame(() => setH(Math.min(100, (n / water.goal) * 100))); return () => cancelAnimationFrame(r) }, [n, water.goal])
  return (
    <div className="card water-card">
      <div className="glass"><div className="water" style={{ height: `${h}%` }} /></div>
      <div className="grow">
        <div className="big-num">{n}<small> / {water.goal} glasses</small></div>
        <p className="small muted">{(n * 0.25).toFixed(2)} L of {(water.goal * 0.25).toFixed(1)} L today</p>
        <div className="wctl">
          <button className="wbtn" onClick={() => change(-1)} aria-label="Remove glass"><Icon name="minus" size={18} stroke={2.4} /></button>
          <button className="wbtn" style={{ background: 'var(--sky-ink)', color: '#fff', flex: 1, width: 'auto', gap: 6, display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 700, fontSize: 13 }} onClick={() => change(1)}>
            <Icon name="plus" size={18} stroke={2.4} /> Add glass
          </button>
        </div>
      </div>
    </div>
  )
}

export function HomeTab({ go }: { go: (tab: string) => void }) {
  const { user, t } = useApp()
  const { open } = usePages()
  const sheet = useSheet()
  const q = useDashboard()
  const toast = useToast()
  const check = useAction((a: { key: string; done: boolean }) => put(`/tracking/checklist/${a.key}`, { done: a.done }), [['dashboard']])
  const [bar, setBar] = useState(0)
  const d = q.data
  const g = d?.pregnancy?.gestation
  useEffect(() => { if (g) { const r = requestAnimationFrame(() => requestAnimationFrame(() => setBar(g.progressPercent))); return () => cancelAnimationFrame(r) } }, [g])

  const hr = new Date().getHours()
  const greet = t(hr < 12 ? 'gm' : hr < 17 ? 'ga' : 'ge')
  const taken = d?.todaysMedicines.filter(m => m.status === 'taken').length ?? 0
  const doneCount = d?.checklist.items.filter(i => i.done).length ?? 0

  return (
    <>
      <header className="top">
        <div className="hello"><div className="avatar">{user.name[0]?.toUpperCase() ?? 'A'}</div><div><small>{greet} 👋</small><h1>{user.name}</h1></div></div>
        <div className="top-actions">
          <LangButton />
          <button className="icon-btn" onClick={() => sheet.open(<NotificationsSheet />)} aria-label="Notifications"><Icon name="bell" size={20} />{(d?.unreadNotifications ?? 0) > 0 && <i className="dot" />}</button>
          <button className="icon-btn sos" onClick={() => open('emergency')} aria-label="Emergency help">SOS</button>
        </div>
      </header>

      {!d || !g ? <Skeleton h={230} mt={0} /> : (
        <section className="hero tap" onClick={() => go('baby')} role="button" aria-label="Open baby development">
          <span className="blob b1" /><span className="blob b2" />
          <div className="hero-top">
            <Ring pct={g.progressPercent}><b>{g.weeks}</b><span>{t('weeks')}</span></Ring>
            <div className="hero-info">
              <span className="chip-glass">{ordinal(g.trimester)} {t('tri')}</span>
              <h2>{g.weeks} weeks, {g.days} day{g.days === 1 ? '' : 's'}</h2>
              {d.week && <p>{t('sizeOf')} <b>{d.week.sizeName}</b></p>}
            </div>
            {d.week && <div className="fruit-float">{d.week.emoji}</div>}
          </div>
          <div className="hero-stats">
            <div><small>{t('dueDate')}</small><b>{fmt(d.pregnancy!.edd, { day: 'numeric', month: 'short' })}</b></div>
            <div><small>{t('daysLeft')}</small><b>{g.daysToGo}</b></div>
            <div><small>{t('progress')}</small><b>{g.progressPercent}%</b></div>
          </div>
          <div className="hero-bar"><i style={{ width: `${bar}%` }} /></div>
        </section>
      )}

      <SecHead title={t('quick')} />
      <div className="quick">
        <button className="q" onClick={() => open('kick')}><span className="c-blush"><Icon name="foot" /></span>{t('kick')}</button>
        <button className="q" onClick={() => open('food')}><span className="c-sage"><Icon name="food" /></span>{t('foodGuide')}</button>
        <button className="q" onClick={() => open('meds')}><span className="c-amber"><Icon name="pill" /></span>{t('meds')}</button>
        <button className="q" onClick={() => open('chat')}><span className="c-lav"><Icon name="spark" /></span>{t('ask')}</button>
      </div>

      <SecHead title={t('todayMeds')} action={d ? `${taken}/${d.todaysMedicines.length} taken` : undefined} onAction={() => open('meds')} />
      <div className="card">
        {!d ? <Skeleton h={60} mt={0} /> : d.todaysMedicines.length
          ? d.todaysMedicines.map(m => <MedRow key={m.medicationId + m.time} dose={m} />)
          : <p className="muted small">No medicines added. <button style={{ color: 'var(--primary)', fontWeight: 700 }} onClick={() => open('meds')}>Add your prescription</button></p>}
      </div>

      <SecHead title={t('hydration')} />
      {d ? <WaterCard water={d.water} /> : <Skeleton h={130} mt={0} />}

      <SecHead title={t('nextAppt')} action={t('seeAll')} onAction={() => open('appts')} />
      {d?.nextAppointment ? <ApptCard a={d.nextAppointment} /> : <div className="card"><p className="muted small">No upcoming appointments.</p></div>}

      <SecHead title={t('meals')} action={t('seeAll')} onAction={() => go('care')} />
      <div className="hscroll">
        {(d?.meals ?? []).map(m => <div className="meal" key={m.slot}><div className="e">{m.emoji}</div><small>{m.slot}</small><p>{m.description}</p></div>)}
      </div>

      <SecHead title={t('checklist')} right={<span className="small muted">{doneCount}/{d?.checklist.items.length ?? 0}</span>} />
      <div className="card">
        {(d?.checklist.items ?? []).map(c => (
          <div key={c.key} className={`check-item ${c.done ? 'on' : ''}`} onClick={() => {
            check.mutate({ key: c.key, done: !c.done })
            if (!c.done && doneCount + 1 === d!.checklist.items.length) toast('🎉 All done for today!')
          }}>
            <Check on={c.done} label={c.title} /><span>{c.title}</span>
          </div>
        ))}
      </div>

      {d?.tipOfTheDay && g && <>
        <SecHead title={t('tip')} />
        <div className="card tip"><b style={{ fontSize: 14.5 }}>Week {g.weeks}</b><p className="small" style={{ color: 'var(--ink2)', marginTop: 6, lineHeight: 1.55, maxWidth: '85%' }}>{d.tipOfTheDay}</p></div>
      </>}

      <button style={{ marginTop: 14, width: '100%', textAlign: 'left' }} className="alert-card tap" onClick={() => open('emergency')}>
        <div className="ico"><Icon name="alert" /></div>
        <div className="grow"><b>{t('warn')}</b><small style={{ display: 'block', marginTop: 2 }}>{t('warnSub')}</small></div>
        <Icon name="chev" size={20} />
      </button>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 22, lineHeight: 1.5 }}>{t('disclaimer')}</p>
    </>
  )
}

export function LangButton() {
  const { setUser, lang } = useApp()
  const toast = useToast()
  const save = useAction((l: 'en' | 'ta') => put<User>('/me', { language: l }), [['dashboard']], u => { setUser(u); toast(u.language === 'ta' ? 'மொழி: தமிழ்' : 'Language: English') })
  return <button className="icon-btn ta" onClick={() => save.mutate(lang === 'en' ? 'ta' : 'en')} aria-label="Change language">{lang === 'en' ? 'அ' : 'A'}</button>
}
