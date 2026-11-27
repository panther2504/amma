import { useState } from 'react'
import { del, post, put } from '../api/client'
import { useAction, useDoses, useMedications } from '../api/hooks'
import type { Dose, Medication, SaveMedication } from '../api/types'
import { Info, Ring, SecHead, Skeleton, colorClass } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, usePages, useSheet, useToast } from '../ui/shell'
import { useDoseToggle } from '../tabs/Home'
import { fmt, hhmm, t12, today } from '../lib/util'

export function MedsPage() {
  const doses = useDoses()
  const meds = useMedications()
  const sheet = useSheet()
  const { open } = usePages()
  const toggle = useDoseToggle()
  const list = doses.data ?? []
  const taken = list.filter(d => d.status === 'taken').length
  const pct = list.length ? Math.round((taken / list.length) * 100) : 0
  const groups = (['Morning', 'Afternoon', 'Evening', 'Bedtime'] as const).map(s => [s, list.filter(d => d.slot === s)] as const).filter(([, l]) => l.length)

  const set = (d: Dose, status: 'taken' | 'skipped') => toggle.mutate({ dose: d, status: d.status === status ? null : status })

  return (
    <PageFrame title="Medicines" right={<button className="icon-btn" onClick={() => sheet.open(<MedForm />)} aria-label="Add medicine"><Icon name="plus" size={20} /></button>}>
      <div className="card" style={{ display: 'flex', alignItems: 'center', gap: 16, background: 'linear-gradient(135deg,#fff,var(--amber))' }}>
        <Ring pct={pct} size={76} stroke="var(--amber-ink)" track="#F3E0BE"><b style={{ fontFamily: 'inherit', fontSize: 20, color: 'var(--amber-ink)' }}>{pct}%</b></Ring>
        <div><b style={{ fontSize: 16 }}>{taken} of {list.length} taken today</b><p className="small muted" style={{ marginTop: 4 }}>{fmt(today(), { weekday: 'long', day: 'numeric', month: 'short' })}</p></div>
      </div>
      <Info icon="shield" style={{ marginTop: 12 }}>Only add medicines prescribed by your doctor. Amma never suggests, changes or stops medicines.</Info>
      {doses.isLoading && <Skeleton h={160} />}
      {groups.map(([g, ds]) => (
        <div key={g}>
          <SecHead title={g} />
          <div className="card">
            {ds.map(d => (
              <div className="rowi" key={d.medicationId + d.time}>
                <div className={`ico ${colorClass(d.colorKey)}`}><Icon name="pill" size={20} /></div>
                <div className="grow">
                  <b>{d.name}</b><small>{d.dosage} · as prescribed</small><small>{t12(d.time)}{d.instructions ? ` · ${d.instructions}` : ''}</small>
                  <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
                    <button className={`btn sm ${d.status === 'taken' ? '' : 'ghost'}`} style={d.status === 'taken' ? { background: 'var(--sage-ink)', boxShadow: 'none' } : undefined} onClick={() => set(d, 'taken')}>
                      <Icon name="check" size={15} stroke={2.6} /> {d.status === 'taken' ? 'Taken' : 'Take'}
                    </button>
                    <button className="btn sm soft" style={d.status === 'skipped' ? { background: 'var(--amber)', color: 'var(--amber-ink)' } : undefined} onClick={() => set(d, 'skipped')}>{d.status === 'skipped' ? 'Skipped' : 'Skip'}</button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      ))}
      {!doses.isLoading && !list.length && (
        <div className="card empty" style={{ marginTop: 14 }}><div>💊</div><p className="small muted" style={{ marginTop: 6 }}>No medicines scheduled today.</p>
          <button className="btn sm ghost" style={{ marginTop: 10 }} onClick={() => sheet.open(<MedForm />)}><Icon name="plus" size={14} /> Add from prescription</button></div>
      )}
      {(meds.data?.length ?? 0) > 0 && <>
        <SecHead title="My prescriptions" />
        <div className="card">
          {meds.data!.map(m => (
            <button className="rowi" key={m.id} style={{ width: '100%', textAlign: 'left' }} onClick={() => sheet.open(<MedForm med={m} />)}>
              <div className={`ico ${colorClass(m.colorKey)}`}><Icon name="pill" size={20} /></div>
              <div className="grow"><b>{m.name}</b><small>{m.dosage} · {m.reminderTimes.map(t12).join(', ')}{m.endDate ? ` · until ${fmt(m.endDate, { day: 'numeric', month: 'short' })}` : ''}</small></div>
              <Icon name="edit" size={16} />
            </button>
          ))}
        </div>
      </>}
      <SecHead title="Refills & prescriptions" />
      <button className="card tap" style={{ display: 'flex', gap: 14, alignItems: 'center', width: '100%', textAlign: 'left' }} onClick={() => open('reports')}>
        <div className="ico c-sky"><Icon name="upload" size={20} /></div>
        <div className="grow"><b style={{ fontSize: 14.5 }}>Upload prescription</b><small className="muted" style={{ display: 'block' }}>Keep it safe in your report vault</small></div>
        <Icon name="chev" size={20} />
      </button>
    </PageFrame>
  )
}

function MedForm({ med }: { med?: Medication }) {
  const sheet = useSheet()
  const toast = useToast()
  const [f, setF] = useState({
    name: med?.name ?? '', dosage: med?.dosage ?? '', instructions: med?.instructions ?? '',
    times: med?.reminderTimes.map(hhmm) ?? ['09:00'], endDate: med?.endDate ?? '',
  })
  const save = useAction((b: SaveMedication) => (med ? put(`/medications/${med.id}`, b) : post('/medications', b)), [['meds'], ['doses'], ['dashboard']],
    () => { sheet.close(); toast(med ? '✅ Medicine updated' : '🔔 Reminder added') })
  const remove = useAction(() => del(`/medications/${med!.id}`), [['meds'], ['doses'], ['dashboard']], () => { sheet.close(); toast('Reminders stopped') })

  const submit = () => {
    if (!f.name.trim()) return toast('Please enter the medicine name')
    if (!f.dosage.trim()) return toast('Please enter the dosage from your prescription')
    if (!f.times.length) return toast('Add at least one reminder time')
    save.mutate({ name: f.name.trim(), dosage: f.dosage.trim(), instructions: f.instructions.trim() || undefined, reminderTimes: f.times, endDate: f.endDate || null })
  }

  return (
    <>
      <h3>{med ? 'Edit medicine' : 'Add prescribed medicine'}</h3>
      <p className="small muted" style={{ marginTop: 4 }}>Enter exactly as written on your prescription.</p>
      <label className="label" htmlFor="mName">Medicine name</label>
      <input className="field" id="mName" placeholder="e.g. Iron + Folic acid" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} />
      <label className="label" htmlFor="mDose">Dosage (as prescribed)</label>
      <input className="field" id="mDose" placeholder="e.g. 1 tablet" value={f.dosage} onChange={e => setF({ ...f, dosage: e.target.value })} />
      <span className="label">Reminder times</span>
      {f.times.map((t, i) => (
        <div key={i} style={{ display: 'flex', gap: 8, marginBottom: 8 }}>
          <input className="field" type="time" value={t} aria-label={`Reminder time ${i + 1}`} onChange={e => setF({ ...f, times: f.times.map((x, j) => (j === i ? e.target.value : x)) })} />
          {f.times.length > 1 && <button className="btn sm soft" style={{ height: 'auto' }} onClick={() => setF({ ...f, times: f.times.filter((_, j) => j !== i) })} aria-label="Remove time"><Icon name="x" size={16} /></button>}
        </div>
      ))}
      {f.times.length < 6 && <button className="btn sm ghost" onClick={() => setF({ ...f, times: [...f.times, '20:00'] })}><Icon name="plus" size={14} /> Add time</button>}
      <div className="row2">
        <div><label className="label" htmlFor="mEnd">Ends on (optional)</label><input className="field" type="date" id="mEnd" value={f.endDate} onChange={e => setF({ ...f, endDate: e.target.value })} /></div>
        <div><label className="label" htmlFor="mNote">Instructions</label><input className="field" id="mNote" placeholder="e.g. After food" value={f.instructions} onChange={e => setF({ ...f, instructions: e.target.value })} /></div>
      </div>
      <button className="btn block" style={{ marginTop: 20 }} disabled={save.isPending} onClick={submit}>{save.isPending ? 'Saving…' : 'Save reminder'}</button>
      {med && <button className="btn ghost block" style={{ marginTop: 10 }} onClick={() => remove.mutate()}>Stop reminders (as advised by doctor)</button>}
    </>
  )
}

