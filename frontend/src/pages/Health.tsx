import { useEffect, useState } from 'react'
import { isQueued, queuePost, queuePut } from '../api/client'
import { patchCache, useAction, useHealth } from '../api/hooks'
import type { Advisory, BpReading, HealthSummary, Saved, WeightReading } from '../api/types'
import { Info, SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, usePages, useSheet, useToast } from '../ui/shell'
import { fmt, fmtDateTime, iso, today } from '../lib/util'

const SYMPTOMS = ['Nausea', 'Headache', 'Back pain', 'Swelling', 'Fatigue', 'Heartburn', 'Leg cramps', 'Poor sleep']

export function HealthPage() {
  const q = useHealth()
  const sheet = useSheet()
  const toast = useToast()
  const h = q.data
  const [symptoms, setSymptoms] = useState<string[]>([])
  useEffect(() => { if (h) setSymptoms(h.today.symptoms) }, [h])
  const saveDaily = useAction((b: { mood?: number; symptoms?: string[] }) => queuePut(`/tracking/daily/${iso(today())}`, b), [['health']], undefined,
    (qc, b) => patchCache<HealthSummary>(qc, ['health'], v => ({ ...v, today: { ...v.today, ...b } })))

  return (
    <PageFrame title="Health tracker" right={<button className="icon-btn" onClick={() => sheet.open(<LogSheet />)} aria-label="Log reading"><Icon name="plus" size={20} /></button>}>
      {!h ? <Skeleton h={180} mt={0} /> : <>
        <div className="mini">
          <div><small><Icon name="scale" size={14} /> Weight</small><b>{h.latestWeight?.kg ?? '—'} <span>kg</span></b></div>
          <div><small><Icon name="heart" size={14} /> Blood pressure</small><b>{h.latestBloodPressure ? `${h.latestBloodPressure.systolic}/${h.latestBloodPressure.diastolic}` : '—'} <span>mmHg</span></b></div>
          <div><small><Icon name="moon" size={14} /> Sleep</small><b>{h.today.sleepHours ?? '—'} <span>hrs</span></b></div>
          <div><small><Icon name="drop" size={14} /> Water</small><b>{h.waterGlasses} <span>glasses</span></b></div>
        </div>
        <SecHead title="Weight journey" right={<span className="small muted">kg by week</span>} />
        <div className="card"><WeightChart w={h.weights} /></div>

        <SecHead title="How are you feeling?" />
        <div className="card"><div className="moods">
          {['😣', '😕', '😐', '🙂', '😊'].map((m, i) => (
            <button key={m} className={h.today.mood === i ? 'on' : ''} aria-label={`Mood ${i + 1} of 5`} onClick={() => { saveDaily.mutate({ mood: i }); toast('Mood saved') }}>{m}</button>
          ))}
        </div></div>

        <SecHead title="Symptoms today" />
        <div className="card">
          <div className="choice" style={{ gap: 8 }}>
            {SYMPTOMS.map(s => (
              <button key={s} style={{ flex: 'none', minWidth: 0, padding: '9px 13px' }} className={symptoms.includes(s) ? 'on' : ''} onClick={() => {
                const next = symptoms.includes(s) ? symptoms.filter(x => x !== s) : [...symptoms, s]
                setSymptoms(next); saveDaily.mutate({ symptoms: next })
              }}>{s}</button>
            ))}
          </div>
          <Info warn style={{ marginTop: 14 }}>A <b>severe</b> headache, vision changes or <b>sudden</b> swelling can be warning signs. Contact your doctor right away.</Info>
        </div>

        <SecHead title="Blood pressure log" />
        <div className="card">
          {h.bloodPressure.length ? h.bloodPressure.slice(0, 10).map((b: BpReading) => (
            <div className="rowi" key={b.id}><div className="ico c-blush"><Icon name="heart" size={19} /></div><div className="grow"><b>{b.systolic}/{b.diastolic} mmHg</b><small>{fmtDateTime(b.measuredAt)}</small></div></div>
          )) : <p className="small muted">No readings yet.</p>}
        </div>
        {h.bloodSugar.length > 0 && <>
          <SecHead title="Blood sugar log" />
          <div className="card">{h.bloodSugar.slice(0, 10).map(s => (
            <div className="rowi" key={s.id}><div className="ico c-amber"><Icon name="drop" size={19} /></div><div className="grow"><b>{s.mgDl} mg/dL</b><small>{s.context === 'fasting' ? 'Fasting' : s.context === 'afterMeal' ? 'After meal' : 'Random'} · {fmtDateTime(s.measuredAt)}</small></div></div>
          ))}</div>
        </>}
      </>}
      <p className="small muted" style={{ textAlign: 'center', marginTop: 14, lineHeight: 1.5 }}>Amma records your readings for you and your doctor.<br />It does not diagnose any condition.</p>
    </PageFrame>
  )
}

function WeightChart({ w }: { w: WeightReading[] }) {
  const pts0 = w.filter(x => x.week > 0)
  if (pts0.length < 2) return <p className="small muted">Add at least two weekly readings to see your chart.</p>
  const W = 320, H = 140, px = 24, py = 18
  const xs = pts0.map(p => p.week), ys = pts0.map(p => Number(p.kg))
  const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys) - 1, y1 = Math.max(...ys) + 1
  const X = (v: number) => px + ((v - x0) / (x1 - x0 || 1)) * (W - px * 2)
  const Y = (v: number) => H - py - ((v - y0) / (y1 - y0)) * (H - py * 2)
  const pts = pts0.map(p => `${X(p.week).toFixed(1)},${Y(Number(p.kg)).toFixed(1)}`)
  const last = pts0[pts0.length - 1]
  return (
    <svg className="chart" viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`Weight chart, latest ${last.kg} kg at week ${last.week}`}>
      <defs><linearGradient id="wg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#C2497A" stopOpacity=".28" /><stop offset="1" stopColor="#C2497A" stopOpacity="0" /></linearGradient></defs>
      {[0, 1, 2].map(i => <line key={i} x1={px} x2={W - px} y1={py + (i * (H - py * 2)) / 2} y2={py + (i * (H - py * 2)) / 2} stroke="#F1E6E9" strokeDasharray="3 4" />)}
      <path className="area" d={`M${pts.join(' L')} L${X(x1)},${H - py} L${X(x0)},${H - py}Z`} fill="url(#wg)" />
      <polyline className="draw" points={pts.join(' ')} fill="none" stroke="#C2497A" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      {pts0.map((p, i) => (
        <g key={p.id}>
          <circle cx={X(p.week)} cy={Y(Number(p.kg))} r={i === pts0.length - 1 ? 5 : 3.5} fill="#fff" stroke="#C2497A" strokeWidth="2.5" style={{ opacity: 0, animation: `fadeIn .3s ${0.3 + i * 0.18}s forwards` }} />
          <text x={X(p.week)} y={H - 2} fontSize="9.5" fill="#9A8893" textAnchor="middle">{p.week}w</text>
        </g>
      ))}
      <text x={X(x1)} y={Y(Number(last.kg)) - 10} fontSize="11" fontWeight="700" fill="#5E1F47" textAnchor="end">{last.kg} kg</text>
    </svg>
  )
}

function LogSheet() {
  const sheet = useSheet()
  const toast = useToast()
  const { open } = usePages()
  const [f, setF] = useState({ kg: '', s: '', d: '', sleep: '', sugar: '', ctx: 'fasting' })
  const [busy, setBusy] = useState(false)
  const qcRefresh = useAction(async () => {}, [['health'], ['dashboard']])

  const submit = async () => {
    const kg = parseFloat(f.kg), s = parseInt(f.s), d = parseInt(f.d), sl = parseFloat(f.sleep), sg = parseInt(f.sugar)
    const tasks: Promise<Saved<unknown> | unknown>[] = []
    if (kg > 20 && kg < 250) tasks.push(queuePost('/health/weights', { kg }))
    if (s > 50 && d > 30) tasks.push(queuePost('/health/blood-pressure', { systolic: s, diastolic: d }))
    if (sl > 0 && sl < 24) tasks.push(queuePut(`/tracking/daily/${iso(today())}`, { sleepHours: sl }))
    if (sg > 20 && sg < 600) tasks.push(queuePost('/health/blood-sugar', { mgDl: sg, context: f.ctx }))
    if (!tasks.length) return toast('Please enter at least one value')
    setBusy(true)
    try {
      const results = await Promise.all(tasks)
      qcRefresh.mutate()
      // Offline: readings are checked on this phone too, so a high reading is never missed.
      const offlineAdv: Advisory | undefined = results.some(isQueued) && s > 50 && d > 30 && (s >= 140 || d >= 90)
        ? { level: s >= 160 || d >= 110 ? 'urgent' : 'attention', title: 'Please share this reading with your doctor', message: `Your reading of ${s}/${d} is higher than the commonly used reference range. Amma cannot diagnose — please contact your doctor. If you also have a severe headache, vision changes, upper tummy pain or sudden swelling, seek medical care immediately.` }
        : undefined
      const adv = (results.map(r => (r as Saved<unknown>)?.advisory).find(Boolean) as Advisory | undefined) ?? offlineAdv
      if (adv) {
        sheet.open(
          <>
            <div style={{ textAlign: 'center' }}><div style={{ fontSize: 52 }}>🩺</div><h3 style={{ marginTop: 6 }}>{adv.title}</h3></div>
            <p className="small" style={{ margin: '12px 0', lineHeight: 1.6, color: 'var(--ink2)', textAlign: 'center' }}>{adv.message}</p>
            <button className="btn danger block" style={{ marginTop: 16 }} onClick={() => { sheet.close(); open('emergency') }}><Icon name="phone" size={18} /> Emergency options</button>
            <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>OK</button>
          </>,
        )
      } else { sheet.close(); toast(results.some(isQueued) ? '✅ Saved on this phone · will sync' : '✅ Saved') }
    } catch (e) { toast((e as Error).message) } finally { setBusy(false) }
  }

  return (
    <>
      <h3>Log a reading</h3><p className="small muted" style={{ marginTop: 4 }}>{fmt(today())}</p>
      <label className="label" htmlFor="lW">Weight (kg)</label><input className="field" type="number" step="0.1" inputMode="decimal" id="lW" placeholder="e.g. 59.4" value={f.kg} onChange={e => setF({ ...f, kg: e.target.value })} />
      <span className="label">Blood pressure (mmHg)</span>
      <div className="row2">
        <input className="field" type="number" inputMode="numeric" aria-label="Systolic" placeholder="Systolic e.g. 110" value={f.s} onChange={e => setF({ ...f, s: e.target.value })} />
        <input className="field" type="number" inputMode="numeric" aria-label="Diastolic" placeholder="Diastolic e.g. 72" value={f.d} onChange={e => setF({ ...f, d: e.target.value })} />
      </div>
      <span className="label">Blood sugar (mg/dL)</span>
      <div className="row2">
        <input className="field" type="number" inputMode="numeric" aria-label="Blood sugar" placeholder="e.g. 88" value={f.sugar} onChange={e => setF({ ...f, sugar: e.target.value })} />
        <select className="field" aria-label="When measured" value={f.ctx} onChange={e => setF({ ...f, ctx: e.target.value })}><option value="fasting">Fasting</option><option value="afterMeal">After meal</option><option value="random">Random</option></select>
      </div>
      <label className="label" htmlFor="lSl">Sleep last night (hours)</label><input className="field" type="number" step="0.5" id="lSl" placeholder="e.g. 7.5" value={f.sleep} onChange={e => setF({ ...f, sleep: e.target.value })} />
      <button className="btn block" style={{ marginTop: 20 }} disabled={busy} onClick={submit}>{busy ? 'Saving…' : 'Save'}</button>
    </>
  )
}
