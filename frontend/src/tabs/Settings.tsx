import { useEffect, useState } from 'react'
import { put } from '../api/client'
import { useAction, usePerson, usePregnancy } from '../api/hooks'
import type { Person } from '../api/types'
import { MenuItem, SecHead } from '../ui/bits'
import { useApp, usePages, useSheet, useToast } from '../ui/shell'
import { AppVersionSection, PinSection } from '../pages/Account'
import { useDrive } from '../pages/Drive'
import { alarmProblems, alarmStatus, openAlarmSettings, setAlarmVolume, testAlarm, type AlarmStatus } from '../lib/reminders'
import { fmt } from '../lib/util'

export function SettingsTab() {
  const { signOut } = useApp()
  const { open } = usePages()
  const sheet = useSheet()
  const toast = useToast()
  const person = usePerson()
  const preg = usePregnancy()
  const drive = useDrive()
  const [st, setSt] = useState<AlarmStatus | null>(null)
  useEffect(() => {
    const load = () => { alarmStatus().then(setSt).catch(() => {}) }
    load()
    const onVis = () => { if (document.visibilityState === 'visible') load() }
    document.addEventListener('visibilitychange', onVis)
    return () => document.removeEventListener('visibilitychange', onVis)
  }, [])

  return (
    <>
      <header className="top"><div><small>Amma</small><h1>Settings</h1></div></header>

      <div className="prof">
        <div className="avatar">{(person.data?.name ?? 'A')[0]?.toUpperCase()}</div>
        <h2>{person.data?.name ?? ' '}</h2>
        <p className="small muted" style={{ marginTop: 4 }}>{person.data?.phone ? `Signed in on this phone as ${person.data.phone}` : ''}</p>
        {person.data?.pinLogin && <button className="btn sm ghost" style={{ marginTop: 12 }} onClick={() => sheet.open(<NameForm p={person.data!} />)}>Change my name</button>}
      </div>

      <SecHead title="Reminders" />
      <div className="menu">
        <MenuItem c="c-sky" i="bell" label="Edit reminders & times" onClick={() => open('reminders')} />
        <MenuItem c="c-lav" i="spark" label="Test alarm (rings in 10 seconds)" onClick={async () => toast((await testAlarm(10)) ? '🔔 Lock your phone now — the alarm rings in 10 seconds' : 'Test alarms work in the Android app')} />
      </div>
      {st && <AlarmCheck st={st} refresh={() => setTimeout(() => alarmStatus().then(setSt), 800)} />}

      <SecHead title="Photos" />
      <div className="menu">
        <MenuItem c="c-sage" i="image" label={drive.data?.connected ? `Google Drive · connected` : 'Save photos to Google Drive'} onClick={() => open('drive')} />
      </div>

      <SecHead title="Pregnancy" />
      <div className="menu">
        <MenuItem c="c-blush" i="heart" label={preg.data ? `Due date · ${fmt(preg.data.edd)}` : 'Pregnancy dates'} onClick={() => open('pregnancy')} />
      </div>

      <PinSection />
      <AppVersionSection />

      <SecHead title="Account" />
      <div className="menu">
        <MenuItem c="c-peach" i="logout" label="Sign out of this phone" onClick={() => sheet.open(
          <>
            <h3>Sign out?</h3>
            <p className="small muted" style={{ margin: '8px 0 18px', lineHeight: 1.5 }}>Reminders stop on this phone until you sign in again. Your data stays safe.</p>
            <button className="btn danger block" onClick={() => { sheet.close(); signOut() }}>Sign out</button>
            <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Cancel</button>
          </>)} />
      </div>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 20 }}>Made for the two of you 💗</p>
    </>
  )
}

function NameForm({ p }: { p: Person }) {
  const sheet = useSheet()
  const toast = useToast()
  const [name, setName] = useState(p.name)
  const save = useAction((n: string) => put<Person>('/me/person', { name: n }), [['person'], ['day']], () => { sheet.close(); toast('Name saved') })
  return (
    <>
      <h3>Your name</h3>
      <p className="small muted" style={{ marginTop: 4 }}>Shown on the other phone, e.g. "Done by {name || 'Ravi'}".</p>
      <input className="field" style={{ marginTop: 14 }} value={name} maxLength={40} onChange={e => setName(e.target.value)} aria-label="Your name" />
      <button className="btn block" style={{ marginTop: 16 }} onClick={() => (name.trim() ? save.mutate(name.trim()) : toast('Enter your name'))}>Save</button>
    </>
  )
}

const BRAND_TIPS: Record<string, string> = {
  vivo: 'Vivo: Settings → Battery → Background power consumption management → Amma → Allow. Also i Manager → App manager → Autostart → Amma on.',
  iqoo: 'iQOO: Settings → Battery → Background power consumption management → Amma → Allow, and turn on Autostart for Amma.',
  oppo: 'Oppo: Settings → Apps → Amma → Battery usage → Allow background activity and Allow auto launch.',
  realme: 'Realme: Settings → Apps → Amma → Battery usage → Allow background activity and Allow auto launch.',
  oneplus: 'OnePlus: Settings → Apps → Amma → Battery → Don\'t optimise / Unrestricted, and allow auto-launch.',
  xiaomi: 'Xiaomi/Redmi/POCO: Settings → Apps → Manage apps → Amma → Autostart on, and Battery saver → No restrictions.',
  redmi: 'Redmi: Settings → Apps → Manage apps → Amma → Autostart on, and Battery saver → No restrictions.',
  poco: 'POCO: Settings → Apps → Manage apps → Amma → Autostart on, and Battery saver → No restrictions.',
  samsung: 'Samsung: Settings → Apps → Amma → Battery → Unrestricted. Also check Amma is not in "Sleeping apps".',
}

function AlarmCheck({ st, refresh }: { st: AlarmStatus; refresh: () => void }) {
  const rows: [string, boolean, (() => Promise<void>) | null][] = [
    ['Notifications allowed', st.notifications, () => openAlarmSettings('notifications')],
    ['Alarms ring on exact time', st.exact, () => openAlarmSettings('exact')],
    ['Shows over the lock screen', st.fullScreen, () => openAlarmSettings('fullscreen')],
    ['Battery: unrestricted', st.batteryUnrestricted, () => openAlarmSettings('battery')],
    [`Alarm volume ${st.alarmVolumeMax ? Math.round((st.alarmVolume / st.alarmVolumeMax) * 100) : 0}%`, st.alarmVolume > 0, () => setAlarmVolume(70)],
  ]
  const tip = BRAND_TIPS[(st.manufacturer || '').toLowerCase()]
  const ok = alarmProblems(st).length === 0
  return (
    <div className="card" style={{ marginTop: 12 }}>
      <b style={{ fontSize: 14.5 }}>{ok ? '✓ Alarms are set up correctly' : 'Alarm check'}</b>
      {rows.map(([label, good, fix]) => (
        <div key={label} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '9px 0', borderTop: '1px solid var(--line)', marginTop: 6 }}>
          <span className="small" style={{ fontWeight: 600, color: good ? 'var(--ink)' : 'var(--red-ink)' }}>{good ? '✓' : '✗'} {label}</span>
          {!good && fix && <button className="btn sm ghost" onClick={async () => { await fix(); refresh() }}>Fix</button>}
        </div>
      ))}
      <p className="small muted" style={{ marginTop: 8, lineHeight: 1.5 }}>
        Reminders ring on the <b>alarm volume</b>, so they sound even when the phone is on silent or vibrate. {st.scheduled} alarms are planned on this phone.
      </p>
      {tip && <p className="small" style={{ marginTop: 8, lineHeight: 1.55, color: 'var(--ink2)', background: 'var(--amber)', borderRadius: 12, padding: '9px 12px' }}>{tip}</p>}
    </div>
  )
}
