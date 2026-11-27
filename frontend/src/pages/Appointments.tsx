import { useState } from 'react'
import { post } from '../api/client'
import { useAction, useAppointments, useContent } from '../api/hooks'
import type { SaveAppointment } from '../api/types'
import { Empty, SecHead, Segment } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useApp, usePages, useSheet, useToast } from '../ui/shell'
import { addDays, fmt, iso, parseDate, t12, today } from '../lib/util'

export function AppointmentsPage() {
  const { lang } = useApp()
  const q = useAppointments()
  const content = useContent(lang)
  const sheet = useSheet()
  const toast = useToast()
  const { open } = usePages()
  const [tab, setTab] = useState(0)
  const done = useAction((id: string) => post(`/appointments/${id}/status?status=completed`), [['appts'], ['dashboard']], () => toast('✅ Appointment completed'))
  const all = q.data ?? []
  const up = all.filter(a => a.status === 'scheduled').sort((a, b) => (a.date + a.time).localeCompare(b.date + b.time))
  const past = all.filter(a => a.status === 'completed').sort((a, b) => (b.date + b.time).localeCompare(a.date + a.time))
  const list = tab ? past : up
  const checkups = (content.data?.snippets ?? []).filter(s => s.kind === 'CheckupType')

  return (
    <PageFrame title="Checkups" right={<button className="icon-btn" onClick={() => sheet.open(<ApptForm />)} aria-label="Add appointment"><Icon name="plus" size={20} /></button>}>
      <div style={{ marginTop: -18 }}><Segment items={[`Upcoming (${up.length})`, `Completed (${past.length})`]} value={tab} onChange={setTab} /></div>
      {list.map(a => {
        const d = parseDate(a.date)
        return (
          <div className="card" key={a.id}>
            <div className="appt">
              <div className="datebox" style={a.status === 'completed' ? { background: 'linear-gradient(160deg,#8EA89B,#B8CCC1)', boxShadow: 'none' } : undefined}><b>{d.getDate()}</b><small>{fmt(d, { month: 'short' })}</small></div>
              <div className="grow">
                <b style={{ fontSize: 15 }}>{a.title}</b>
                <small className="muted" style={{ display: 'block', marginTop: 3 }}>{a.doctorName ?? '—'}</small>
                <small className="muted" style={{ display: 'block' }}><Icon name="pin" size={11} /> {a.location ?? '—'} · {t12(a.time)}</small>
              </div>
            </div>
            {a.purpose && <p className="small" style={{ marginTop: 12, padding: '10px 12px', borderRadius: 12, background: 'var(--bg)', color: 'var(--ink2)' }}>{a.purpose}</p>}
            {a.status === 'completed'
              ? <button className="btn sm soft" style={{ marginTop: 10 }} onClick={() => open('reports')}><Icon name="file" size={15} /> View reports</button>
              : <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                  <button className="btn sm ghost" onClick={() => done.mutate(a.id)}><Icon name="check" size={15} stroke={2.6} /> Mark done</button>
                  <span className="btn sm soft" style={{ pointerEvents: 'none' }}><Icon name="bell" size={15} /> {a.reminderMinutesBefore >= 1440 ? `${Math.round(a.reminderMinutesBefore / 1440)} day before` : `${Math.round(a.reminderMinutesBefore / 60)} h before`}</span>
                </div>}
          </div>
        )
      })}
      {!q.isLoading && !list.length && <div style={{ marginTop: 12 }}><Empty emoji="📅" text="Nothing here yet." /></div>}
      <SecHead title="Typical checkups" />
      <div className="card">
        {checkups.map(x => <div className="rowi" key={x.key}><div className="ico c-lav"><Icon name="clip" size={19} /></div><div className="grow"><b>{x.title}</b><small>{x.body}</small></div></div>)}
        <p className="small muted" style={{ marginTop: 10, lineHeight: 1.5 }}>Your schedule depends on your health and your doctor's advice.</p>
      </div>
    </PageFrame>
  )
}

function ApptForm() {
  const sheet = useSheet()
  const toast = useToast()
  const [f, setF] = useState({ title: '', doctorName: '', location: '', date: iso(addDays(today(), 7)), time: '10:00', purpose: '', remind: '1440' })
  const save = useAction((b: SaveAppointment) => post('/appointments', b), [['appts'], ['dashboard']], () => { sheet.close(); toast('📅 Appointment saved') })
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value })
  return (
    <>
      <h3>New appointment</h3>
      <label className="label" htmlFor="aT">Title</label><input className="field" id="aT" placeholder="e.g. Growth scan" value={f.title} onChange={set('title')} />
      <label className="label" htmlFor="aD">Doctor / Lab</label><input className="field" id="aD" placeholder="Doctor name" value={f.doctorName} onChange={set('doctorName')} />
      <label className="label" htmlFor="aP">Hospital</label><input className="field" id="aP" placeholder="Hospital name" value={f.location} onChange={set('location')} />
      <div className="row2">
        <div><label className="label" htmlFor="aDt">Date</label><input className="field" type="date" id="aDt" min={iso(today())} value={f.date} onChange={set('date')} /></div>
        <div><label className="label" htmlFor="aTm">Time</label><input className="field" type="time" id="aTm" value={f.time} onChange={set('time')} /></div>
      </div>
      <label className="label" htmlFor="aPu">Purpose</label><input className="field" id="aPu" placeholder="What is this visit for?" value={f.purpose} onChange={set('purpose')} />
      <label className="label" htmlFor="aR">Remind me</label>
      <select className="field" id="aR" value={f.remind} onChange={set('remind')}><option value="120">2 hours before</option><option value="1440">1 day before</option><option value="2880">2 days before</option></select>
      <button className="btn block" style={{ marginTop: 20 }} disabled={save.isPending} onClick={() => {
        if (!f.title.trim() || !f.date) return toast('Please add a title and date')
        save.mutate({ title: f.title.trim(), doctorName: f.doctorName || undefined, location: f.location || undefined, date: f.date, time: f.time || '10:00', purpose: f.purpose || 'Antenatal visit', reminderMinutesBefore: Number(f.remind) })
      }}>Save appointment</button>
    </>
  )
}
