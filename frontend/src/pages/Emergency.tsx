import { useState } from 'react'
import { del, post } from '../api/client'
import { useContacts, useDashboard } from '../api/hooks'
import type { EmergencyContact } from '../api/types'
import { SecHead } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, usePages, useToast } from '../ui/shell'
import { EMERGENCY_NUMBERS, WARNING_SIGNS } from '../lib/safety'

const tel = (p: string) => `tel:${p.replace(/[^\d+]/g, '')}`

/** Always available: works offline (bundled warning signs, tel: links) and is never behind a subscription. */
export function EmergencyPage() {
  const toast = useToast()
  const { open } = usePages()
  const dash = useDashboard()
  const contacts = useContacts()
  const [sending, setSending] = useState(false)
  const p = dash.data?.pregnancy
  const doctor = contacts.data?.find(c => /doctor|dr\.?/i.test(c.relation + c.name)) ?? (p?.doctorPhone ? { name: p.doctorName ?? 'Your doctor', phone: p.doctorPhone } as EmergencyContact : undefined)

  const alert = async (share: boolean) => {
    setSending(true)
    try {
      let coords: GeolocationCoordinates | undefined
      if (share && 'geolocation' in navigator) {
        coords = await new Promise(res => navigator.geolocation.getCurrentPosition(pos => res(pos.coords), () => res(undefined), { timeout: 8000 }))
        if (!coords) toast('Location not available — sending alert without it')
      }
      const r = await post<{ notifiedCount: number; notified: string[] }>('/me/emergency-alert', { shareLocation: !!coords, latitude: coords?.latitude, longitude: coords?.longitude })
      toast(r.notifiedCount ? `📣 Alert sent to ${r.notified.join(' & ')}` : 'Add a family member or emergency contact first')
    } catch { toast('Could not send alert — please call directly') } finally { setSending(false) }
  }

  return (
    <PageFrame title="Emergency help">
      <div className="emer-hero"><div className="pulse-dot"><Icon name="alert" size={26} /></div>
        <h3>If something feels wrong, don't wait.</h3><p>Call for help or go to your hospital immediately. Never use the app to delay care.</p></div>
      <div className="call-grid">
        <a className="call tap" href={`tel:${EMERGENCY_NUMBERS.ambulance}`}><span className="ico c-red"><Icon name="phone" size={20} /></span><b>Ambulance 108</b><small>Emergency</small></a>
        <a className="call tap" href={`tel:${EMERGENCY_NUMBERS.maternity}`}><span className="ico c-peach"><Icon name="hosp" size={20} /></span><b>Call 102</b><small>Maternity transport</small></a>
        {doctor
          ? <a className="call tap" href={tel(doctor.phone)}><span className="ico c-lav"><Icon name="phone" size={20} /></span><b>{doctor.name}</b><small>Your doctor</small></a>
          : <button className="call tap" onClick={() => open('contacts')}><span className="ico c-lav"><Icon name="plus" size={20} /></span><b>Add doctor</b><small>Save their number</small></button>}
        <button className="call tap" disabled={sending} onClick={() => alert(false)}><span className="ico c-sage"><Icon name="users" size={20} /></span><b>Alert family</b><small>Trusted contacts</small></button>
      </div>
      <button className="btn soft block" style={{ marginTop: 10 }} disabled={sending} onClick={() => alert(true)}><Icon name="pin" size={18} /> Alert family with my location</button>
      <SecHead title="Get help immediately if you have" />
      <div className="card">{WARNING_SIGNS.map(([e, t]) => <div className="rowi" key={t}><div className="ico c-red">{e}</div><div className="grow"><b>{t}</b></div></div>)}</div>
      {p?.hospitalName && <>
        <SecHead title="Saved hospital" />
        <div className="card" style={{ display: 'flex', gap: 14, alignItems: 'center' }}>
          <div className="ico c-sky"><Icon name="hosp" /></div>
          <div className="grow"><b style={{ fontSize: 14.5 }}>{p.hospitalName}</b><small className="muted" style={{ display: 'block' }}>Your delivery hospital</small></div>
          {p.hospitalPhone && <a className="btn sm ghost" href={tel(p.hospitalPhone)} style={{ textDecoration: 'none' }}>Call</a>}
        </div>
      </>}
      <div className="card" style={{ marginTop: 12 }}>
        <div className="rowi"><div className="ico c-sky">🧠</div><div className="grow"><b>Feeling overwhelmed?</b><small>Tele-MANAS mental health helpline · free, 24×7</small></div><a className="btn sm ghost" href={`tel:${EMERGENCY_NUMBERS.mentalHealth}`} style={{ textDecoration: 'none' }}>14416</a></div>
      </div>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 16, lineHeight: 1.5 }}>This page is always free and available offline.<br />Educational content is not a substitute for emergency care.</p>
    </PageFrame>
  )
}

export function ContactsPage() {
  const contacts = useContacts()
  const toast = useToast()
  const [f, setF] = useState({ name: '', relation: '', phone: '' })
  const [busy, setBusy] = useState(false)
  const add = async () => {
    if (!f.name.trim() || !f.phone.trim()) return toast('Enter a name and phone number')
    setBusy(true)
    try { await post('/me/emergency-contacts', { ...f, isPrimary: !contacts.data?.length, notifyInEmergency: true }); setF({ name: '', relation: '', phone: '' }); await contacts.refetch(); toast('✅ Contact saved') }
    catch (e) { toast((e as Error).message) } finally { setBusy(false) }
  }
  const remove = async (id: string) => { await del(`/me/emergency-contacts/${id}`); contacts.refetch() }
  return (
    <PageFrame title="Emergency contacts">
      <div className="card">
        {(contacts.data ?? []).map(c => (
          <div className="rowi" key={c.id}><div className="ico c-red"><Icon name="phone" size={19} /></div>
            <div className="grow"><b>{c.name}{c.isPrimary ? ' · Primary' : ''}</b><small>{c.relation || 'Contact'} · {c.phone}</small></div>
            <a className="btn sm ghost" href={tel(c.phone)} style={{ textDecoration: 'none' }}>Call</a>
            <button className="btn sm soft" onClick={() => remove(c.id)} aria-label={`Remove ${c.name}`}><Icon name="x" size={14} /></button></div>
        ))}
        {!contacts.data?.length && <p className="small muted">No emergency contacts yet.</p>}
      </div>
      <SecHead title="Add a contact" />
      <div className="card">
        <label className="label" style={{ marginTop: 0 }} htmlFor="cN">Name</label><input className="field" id="cN" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} placeholder="e.g. Arun" />
        <label className="label" htmlFor="cR">Relation</label><input className="field" id="cR" value={f.relation} onChange={e => setF({ ...f, relation: e.target.value })} placeholder="e.g. Husband, Doctor" />
        <label className="label" htmlFor="cP">Phone</label><input className="field" id="cP" inputMode="tel" value={f.phone} onChange={e => setF({ ...f, phone: e.target.value })} placeholder="+91" />
        <button className="btn block" style={{ marginTop: 16 }} disabled={busy} onClick={add}>Save contact</button>
      </div>
    </PageFrame>
  )
}
