import { useEffect, useState } from 'react'
import { api, del, put } from '../api/client'
import { useAction, useConsents, useNotificationPrefs, usePregnancy } from '../api/hooks'
import type { ConsentType, Diet, NotificationPrefs, Pregnancy, SavePregnancy, User } from '../api/types'
import { Choice, Info, SecHead, Skeleton, Switch } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { MenuItem } from '../tabs/Profile'
import { checkForUpdate, currentVersion } from '../lib/updates'
import { UpdateSheet } from '../ui/UpdateSheet'
import { PageFrame, useApp, useSheet, useToast } from '../ui/shell'
import { fmt, gestationFromLmp, iso, today } from '../lib/util'

const dietLabel = (d: Diet) => (d === 'vegetarian' ? 'Vegetarian' : d === 'eggetarian' ? 'Eggetarian' : 'Non-vegetarian')

export function PregnancyPage() {
  const q = usePregnancy()
  const sheet = useSheet()
  const p = q.data
  return (
    <PageFrame title="Pregnancy profile">
      {!p ? <Skeleton h={400} mt={0} /> : <>
        <div className="card">
          {([
            ['Last menstrual period', fmt(p.lmp)], ['Estimated due date', fmt(p.edd)], ['Gestational age', `${p.gestation.weeks} weeks ${p.gestation.days} days`],
            ['Pregnancy type', p.pregnancyType === 'single' ? 'Single' : p.pregnancyType === 'twins' ? 'Twins' : 'Multiple'],
            ['Dating method', p.datingMethod === 'lmp' ? `LMP${p.doctorConfirmed ? ' · confirmed by doctor' : ' (doctor can confirm by scan)'}` : p.datingMethod === 'ultrasound' ? 'Ultrasound' : 'Due date from doctor'],
            ['Food preference', dietLabel(p.diet)], ['Food allergies', p.foodAllergies.join(', ') || 'None recorded'], ['Medical conditions', p.medicalConditions.join(', ') || 'None recorded'],
            ['Blood group', p.bloodGroup ? `${p.bloodGroup} ${p.rhFactor ?? ''}` : 'Add from report'],
            ['Doctor', p.doctorName ? `${p.doctorName}${p.doctorPhone ? ` · ${p.doctorPhone}` : ''}` : '—'], ['Hospital', p.hospitalName ?? '—'], ['Daily water goal', `${p.waterGoalGlasses} glasses`],
          ] as [string, string][]).map(([k, v]) => <div className="rowi" key={k}><div className="grow"><small style={{ margin: 0 }}>{k}</small><b style={{ marginTop: 2 }}>{v}</b></div></div>)}
        </div>
        <button className="btn block" style={{ marginTop: 14 }} onClick={() => sheet.open(<EditPregnancy p={p} />)}><Icon name="edit" size={18} /> Update details</button>
      </>}
    </PageFrame>
  )
}

function EditPregnancy({ p }: { p: Pregnancy }) {
  const sheet = useSheet()
  const toast = useToast()
  const [f, setF] = useState({
    lmp: p.lmp, diet: p.diet, type: p.pregnancyType, confirmed: p.doctorConfirmed, allergies: p.foodAllergies.join(', '), conditions: p.medicalConditions.join(', '),
    bloodGroup: p.bloodGroup ?? '', rh: p.rhFactor ?? '', doctorName: p.doctorName ?? '', doctorPhone: p.doctorPhone ?? '', hospitalName: p.hospitalName ?? '', hospitalPhone: p.hospitalPhone ?? '', water: String(p.waterGoalGlasses),
  })
  const save = useAction((b: SavePregnancy) => put<Pregnancy>('/pregnancy', b), [['pregnancy'], ['dashboard']], () => { sheet.close(); toast('✅ Pregnancy profile updated') })
  const split = (s: string) => s.split(',').map(x => x.trim()).filter(Boolean)
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => setF({ ...f, [k]: e.target.value })
  const submit = () => {
    if (!gestationFromLmp(f.lmp).plausible) return toast('Please check the date')
    save.mutate({
      datingMethod: 'lmp', lmp: f.lmp, doctorConfirmed: f.confirmed, diet: f.diet, pregnancyType: f.type, foodAllergies: split(f.allergies), medicalConditions: split(f.conditions),
      bloodGroup: f.bloodGroup || null, rhFactor: f.rh || null, doctorName: f.doctorName || null, doctorPhone: f.doctorPhone || null,
      hospitalName: f.hospitalName || null, hospitalPhone: f.hospitalPhone || null, waterGoalGlasses: Math.min(20, Math.max(4, Number(f.water) || 10)),
    })
  }
  return (
    <>
      <h3>Update pregnancy details</h3>
      <p className="small muted" style={{ marginTop: 4 }}>Use the date confirmed by your doctor, if available.</p>
      <label className="label" htmlFor="eL">First day of last period</label><input className="field" type="date" id="eL" value={f.lmp} max={iso(today())} onChange={set('lmp')} />
      <label className="check-item" style={{ padding: '10px 0 0' }}><input type="checkbox" checked={f.confirmed} onChange={e => setF({ ...f, confirmed: e.target.checked })} style={{ width: 20, height: 20, accentColor: 'var(--primary)' }} /><span className="small">My doctor has confirmed these dates</span></label>
      <span className="label">Food preference</span>
      <Choice value={f.diet} onChange={diet => setF({ ...f, diet })} options={[['vegetarian', 'Veg'], ['eggetarian', 'Egg'], ['nonVegetarian', 'Non-veg']]} />
      <span className="label">Pregnancy type</span>
      <Choice value={f.type} onChange={type => setF({ ...f, type })} options={[['single', 'Single'], ['twins', 'Twins'], ['multiple', 'Multiple']]} />
      <label className="label" htmlFor="eA">Food allergies (comma separated)</label><input className="field" id="eA" placeholder="e.g. peanut, prawns" value={f.allergies} onChange={set('allergies')} />
      <label className="label" htmlFor="eC">Existing medical conditions</label><input className="field" id="eC" placeholder="e.g. thyroid" value={f.conditions} onChange={set('conditions')} />
      <div className="row2">
        <div><label className="label" htmlFor="eB">Blood group</label><select className="field" id="eB" value={f.bloodGroup} onChange={set('bloodGroup')}><option value="">—</option>{['A', 'B', 'AB', 'O'].map(x => <option key={x}>{x}</option>)}</select></div>
        <div><label className="label" htmlFor="eRh">Rh factor</label><select className="field" id="eRh" value={f.rh} onChange={set('rh')}><option value="">—</option><option>Positive</option><option>Negative</option></select></div>
      </div>
      <div className="row2">
        <div><label className="label" htmlFor="eDn">Doctor</label><input className="field" id="eDn" value={f.doctorName} onChange={set('doctorName')} /></div>
        <div><label className="label" htmlFor="eDp">Doctor phone</label><input className="field" id="eDp" inputMode="tel" value={f.doctorPhone} onChange={set('doctorPhone')} /></div>
      </div>
      <div className="row2">
        <div><label className="label" htmlFor="eHn">Hospital</label><input className="field" id="eHn" value={f.hospitalName} onChange={set('hospitalName')} /></div>
        <div><label className="label" htmlFor="eHp">Hospital phone</label><input className="field" id="eHp" inputMode="tel" value={f.hospitalPhone} onChange={set('hospitalPhone')} /></div>
      </div>
      <label className="label" htmlFor="eW">Daily water goal (glasses)</label><input className="field" type="number" id="eW" min={4} max={20} value={f.water} onChange={set('water')} />
      <button className="btn block" style={{ marginTop: 20 }} disabled={save.isPending} onClick={submit}>Save</button>
    </>
  )
}

export function SettingsPage() {
  const { lang, setUser } = useApp()
  const q = useNotificationPrefs()
  const toast = useToast()
  const save = useAction((b: NotificationPrefs) => put('/notifications/preferences', b), [['prefs']])
  const lng = useAction((l: 'en' | 'ta') => put<User>('/me', { language: l }), [], u => { setUser(u); toast(u.language === 'ta' ? 'மொழி: தமிழ்' : 'Language: English') })
  const p = q.data
  return (
    <PageFrame title="Settings">
      <SecHead title="Notifications" />
      {!p ? <Skeleton h={250} mt={0} /> : <div className="menu">
        {([['medicines', 'Medicine reminders'], ['appointments', 'Appointment reminders'], ['water', 'Water reminders'], ['weekly', 'Weekly pregnancy update'], ['baby', 'Baby development updates']] as [keyof NotificationPrefs, string][]).map(([k, l]) => (
          <div className="mi" key={k}><b>{l}</b><Switch on={!!p[k]} label={l} onChange={v => save.mutate({ ...p, [k]: v })} /></div>
        ))}
      </div>}
      <PinSection />
      <AppVersionSection />
      <SecHead title="Language" />
      <Choice value={lang} onChange={v => v !== lang && lng.mutate(v)} options={[['en', 'English'], ['ta', <span className="ta" key="ta">தமிழ்</span>]]} />
      <p className="small muted" style={{ marginTop: 10, lineHeight: 1.5 }}>Tamil translations of all medical content are being reviewed; untranslated content is shown in English.</p>
    </PageFrame>
  )
}

const CONSENTS: [ConsentType, string, string][] = [
  ['healthData', 'Store my health data', 'Needed to track your pregnancy, medicines and readings.'],
  ['familySharing', 'Family sharing', 'Lets the people you invite see what you choose to share.'],
  ['aiProcessing', 'Ask Amma assistant', 'Your questions are processed to answer them; history can be deleted anytime.'],
  ['locationSharing', 'Location in emergencies', 'Only sent when you tap "Alert family with my location".'],
  ['notifications', 'Reminders & notifications', 'Medicine, appointment and weekly updates.'],
]

export function PrivacyPage() {
  const { signOut } = useApp()
  const q = useConsents()
  const sheet = useSheet()
  const toast = useToast()
  const set = useAction((b: { type: ConsentType; granted: boolean }) => put('/me/consents', b), [['consents']])

  const exportData = async () => {
    try {
      const data = await api<unknown>('/me/export')
      const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }))
      const a = document.createElement('a'); a.href = url; a.download = 'amma-my-data.json'; a.click()
      setTimeout(() => URL.revokeObjectURL(url), 5000)
      toast('📦 Your data has been downloaded')
    } catch (e) { toast((e as Error).message) }
  }
  const deleteAccount = () => sheet.open(
    <>
      <h3>Delete your account?</h3>
      <p className="small muted" style={{ margin: '8px 0 18px', lineHeight: 1.5 }}>This permanently deletes your pregnancy profile, health records, reports and reminders. This cannot be undone.</p>
      <button className="btn danger block" onClick={async () => { await del('/me'); sheet.close(); signOut(); toast('Your account has been deleted') }}>Delete permanently</button>
      <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Cancel</button>
    </>,
  )

  return (
    <PageFrame title="Privacy & consent">
      <Info icon="shield">Your data is yours. It is never sold, and you can change these choices at any time.</Info>
      <SecHead title="Consent" />
      <div className="menu">
        {CONSENTS.map(([k, l, d]) => {
          const on = q.data?.find(c => c.type === k)?.granted ?? false
          return <div className="mi" key={k} style={{ alignItems: 'flex-start' }}><div className="grow"><b style={{ display: 'block' }}>{l}</b><small className="muted" style={{ fontSize: 12, lineHeight: 1.4 }}>{d}</small></div><Switch on={on} label={l} onChange={v => set.mutate({ type: k, granted: v })} /></div>
        })}
      </div>
      <SecHead title="My data" />
      <div className="menu">
        <MenuItem c="c-sky" i="upload" label="Export my data" onClick={exportData} />
        <MenuItem c="c-red" i="x" label="Delete my account" onClick={deleteAccount} />
      </div>
    </PageFrame>
  )
}

function PinSection() {
  const toast = useToast()
  const sheet = useSheet()
  const [mode, setMode] = useState<string | null>(null)
  useEffect(() => { api<{ mode: string }>('/auth/config').then(c => setMode(c.mode)).catch(() => {}) }, [])
  if (mode !== 'pin') return null
  const Form = () => {
    const [f, setF] = useState({ cur: '', next: '', again: '' })
    const d = (v: string) => v.replace(/\D/g, '').slice(0, 8)
    const save = async () => {
      if (!/^\d{4,8}$/.test(f.next)) return toast('Choose a PIN of 4 to 8 digits')
      if (f.next !== f.again) return toast('The two PINs do not match')
      try { await put('/me/pin', { currentPin: f.cur, newPin: f.next }); sheet.close(); toast('🔒 PIN changed') } catch (e) { toast((e as Error).message) }
    }
    return (
      <>
        <h3>Change PIN</h3>
        <label className="label" htmlFor="pc">Current PIN</label><input className="field" id="pc" type="password" inputMode="numeric" value={f.cur} onChange={e => setF({ ...f, cur: d(e.target.value) })} />
        <label className="label" htmlFor="pn">New PIN</label><input className="field" id="pn" type="password" inputMode="numeric" value={f.next} onChange={e => setF({ ...f, next: d(e.target.value) })} />
        <label className="label" htmlFor="pa">Repeat new PIN</label><input className="field" id="pa" type="password" inputMode="numeric" value={f.again} onChange={e => setF({ ...f, again: d(e.target.value) })} />
        <button className="btn block" style={{ marginTop: 20 }} onClick={save}>Save PIN</button>
      </>
    )
  }
  return (
    <>
      <SecHead title="Security" />
      <div className="menu"><MenuItem c="c-lav" i="lock" label="Change PIN" onClick={() => sheet.open(<Form />)} /></div>
    </>
  )
}

function AppVersionSection() {
  const toast = useToast()
  const sheet = useSheet()
  const [ver, setVer] = useState<{ name: string; code: number } | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => { currentVersion().then(setVer).catch(() => {}) }, [])
  if (!ver) return null
  const check = async () => {
    setBusy(true)
    try {
      const r = await checkForUpdate()
      if (r) sheet.open(<UpdateSheet release={r} />)
      else toast('✅ You have the latest version')
    } catch { toast("Couldn't check for updates. Try again when you're online.") } finally { setBusy(false) }
  }
  return (
    <>
      <SecHead title="App" />
      <div className="menu">
        <div className="mi"><b>Version {ver.name} ({ver.code})</b><button className="btn sm ghost" disabled={busy} onClick={check}>{busy ? 'Checking…' : 'Check for updates'}</button></div>
      </div>
    </>
  )
}
