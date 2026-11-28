import { useState } from 'react'
import { del, post, put } from '../api/client'
import { useAction, useReminders } from '../api/hooks'
import type { Reminder, ReminderKind, SaveReminder } from '../api/types'
import { Choice, Info, SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useSheet, useToast } from '../ui/shell'
import { KIND_ICON, KIND_LABEL, testAlarm } from '../lib/reminders'
import { hhmm, t12 } from '../lib/util'

const ORDER: ReminderKind[] = ['tablet', 'water', 'meal', 'snack', 'juice', 'other']

export function RemindersPage() {
  const q = useReminders()
  const sheet = useSheet()
  const toast = useToast()
  return (
    <PageFrame title="Reminders" right={<button className="icon-btn" onClick={() => sheet.open(<ReminderForm />)} aria-label="Add reminder"><Icon name="plus" size={20} /></button>}>
      <Info>Every reminder rings on both phones with sound. Tap a reminder to change its times.</Info>
      {q.isLoading && <Skeleton h={300} />}
      {ORDER.map(k => {
        const list = (q.data ?? []).filter(r => r.kind === k)
        if (!list.length) return null
        return (
          <div key={k}>
            <SecHead title={`${KIND_ICON[k]} ${KIND_LABEL[k]}${k === 'tablet' ? 's' : ''}`} />
            <div className="card">
              {list.map(r => (
                <button key={r.id} className="rowi" style={{ width: '100%', textAlign: 'left' }} onClick={() => sheet.open(<ReminderForm r={r} />)}>
                  <div className="grow">
                    <b>{r.title}</b>
                    {r.note && <small>{r.note}</small>}
                    <small style={{ color: 'var(--primary)', fontWeight: 600 }}>{r.times.map(t12).join(' · ')}</small>
                  </div>
                  <Icon name="edit" size={16} />
                </button>
              ))}
            </div>
          </div>
        )
      })}
      <button className="btn block" style={{ marginTop: 16 }} onClick={() => sheet.open(<ReminderForm />)}><Icon name="plus" size={18} /> Add a reminder</button>
      <button className="btn ghost block" style={{ marginTop: 10 }} onClick={async () => toast((await testAlarm(10)) ? '🔔 Test alarm in 10 seconds — lock your phone to check' : 'Test alarms work in the Android app')}>
        <Icon name="bell" size={18} /> Test alarm (rings in 10 seconds)
      </button>
    </PageFrame>
  )
}

function ReminderForm({ r }: { r?: Reminder }) {
  const sheet = useSheet()
  const toast = useToast()
  const [kind, setKind] = useState<ReminderKind>(r?.kind ?? 'tablet')
  const [title, setTitle] = useState(r?.title ?? '')
  const [note, setNote] = useState(r?.note ?? '')
  const [times, setTimes] = useState<string[]>(r?.times.map(hhmm) ?? ['09:00'])
  const save = useAction((b: SaveReminder) => (r ? put(`/reminders/${r.id}`, b) : post('/reminders', b)), [['reminders'], ['day']],
    () => { sheet.close(); toast(r ? '✅ Reminder updated' : '🔔 Reminder added — both phones will ring') })
  const remove = useAction(() => del(`/reminders/${r!.id}`), [['reminders'], ['day']], () => { sheet.close(); toast('Reminder removed') })

  const defaultTitle: Record<ReminderKind, string> = { water: 'Drink a glass of water', meal: 'Lunch', snack: 'Snack', juice: 'Juice', tablet: '', other: '' }
  const submit = () => {
    const t = title.trim() || defaultTitle[kind]
    if (!t) return toast(kind === 'tablet' ? 'Enter the tablet name' : 'Enter a name')
    if (!times.length) return toast('Add at least one time')
    save.mutate({ kind, title: t, note: note.trim() || null, times: [...new Set(times)].sort() })
  }
  const addTime = () => {
    const last = times[times.length - 1] ?? '08:00'
    const [h, m] = last.split(':').map(Number)
    setTimes([...times, `${String(Math.min(23, h + 2)).padStart(2, '0')}:${String(m).padStart(2, '0')}`])
  }

  return (
    <>
      <h3>{r ? 'Edit reminder' : 'New reminder'}</h3>
      <span className="label">Type</span>
      <Choice value={kind} onChange={setKind} options={(['tablet', 'water', 'meal', 'snack', 'juice', 'other'] as ReminderKind[]).map(k => [k, `${KIND_ICON[k]} ${KIND_LABEL[k]}`])} />
      <label className="label" htmlFor="rT">{kind === 'tablet' ? 'Tablet name' : 'Name'}</label>
      <input className="field" id="rT" placeholder={kind === 'tablet' ? 'e.g. Iron + Folic acid' : defaultTitle[kind] || 'e.g. Walk'} value={title} onChange={e => setTitle(e.target.value)} />
      <label className="label" htmlFor="rN">{kind === 'tablet' ? 'Dose (as prescribed)' : 'Note (optional)'}</label>
      <input className="field" id="rN" placeholder={kind === 'tablet' ? 'e.g. 1 tablet after food' : 'e.g. ragi dosa'} value={note} onChange={e => setNote(e.target.value)} />
      <span className="label">Times</span>
      {times.map((t, i) => (
        <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
          <input className="field" type="time" value={t} aria-label={`Time ${i + 1}`} onChange={e => setTimes(times.map((x, j) => (j === i ? e.target.value : x)))} />
          {times.length > 1 && <button className="btn sm soft" style={{ height: 'auto' }} onClick={() => setTimes(times.filter((_, j) => j !== i))} aria-label="Remove time"><Icon name="x" size={16} /></button>}
        </div>
      ))}
      <button className="btn sm ghost" onClick={addTime}><Icon name="plus" size={14} /> Add time</button>
      <button className="btn block" style={{ marginTop: 20 }} disabled={save.isPending} onClick={submit}>{save.isPending ? 'Saving…' : 'Save'}</button>
      {r && <button className="btn ghost block" style={{ marginTop: 10 }} onClick={() => remove.mutate()}><Icon name="trash" size={16} /> Remove this reminder</button>}
    </>
  )
}
