import { useEffect, useState } from 'react'
import { put } from '../api/client'
import { useAction, usePerson, usePregnancy } from '../api/hooks'
import type { Person } from '../api/types'
import { Info, MenuItem, SecHead } from '../ui/bits'
import { useApp, usePages, useSheet, useToast } from '../ui/shell'
import { AppVersionSection, PinSection } from '../pages/Account'
import { notificationStatus, openExactAlarmSettings, requestNotificationPermission, testReminder } from '../lib/reminders'
import { fmt } from '../lib/util'

export function SettingsTab() {
  const { signOut } = useApp()
  const { open } = usePages()
  const sheet = useSheet()
  const toast = useToast()
  const person = usePerson()
  const preg = usePregnancy()
  const [st, setSt] = useState<{ permission: boolean; exact: boolean } | null>(null)
  useEffect(() => { notificationStatus().then(setSt).catch(() => {}) }, [])

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
        <MenuItem c="c-lav" i="spark" label="Test the reminder sound" onClick={async () => toast((await testReminder()) ? '🔔 A test reminder will ring in 5 seconds' : 'Test reminders work in the Android app')} />
      </div>
      {st && (!st.permission || !st.exact) && (
        <div className="info warn" style={{ marginTop: 12 }}>
          <span>
            {!st.permission ? 'Notifications are off, so reminders cannot ring. ' : 'For on-time reminders, allow "Alarms & reminders". '}
            <button style={{ color: 'inherit', fontWeight: 800, textDecoration: 'underline' }}
              onClick={async () => { if (!st.permission) await requestNotificationPermission(); else await openExactAlarmSettings(); setSt(await notificationStatus()) }}>Fix now</button>
          </span>
        </div>
      )}
      <Info style={{ marginTop: 12 }}>
        If a reminder ever comes late, open phone <b>Settings → Apps → Amma → Battery</b> and choose <b>Unrestricted</b> (some phones call it "No restrictions" or turn off "Battery optimisation").
      </Info>

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
