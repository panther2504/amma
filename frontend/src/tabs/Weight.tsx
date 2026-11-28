import { useState } from 'react'
import { isQueued, queuePost } from '../api/client'
import { patchCache, useAction, useHealth } from '../api/hooks'
import type { HealthSummary, WeightReading } from '../api/types'
import { SecHead, Skeleton } from '../ui/bits'
import { useToast } from '../ui/shell'
import { fmt, iso, today } from '../lib/util'

export function WeightTab() {
  const q = useHealth()
  const toast = useToast()
  const weights = [...(q.data?.weights ?? [])].sort((a, b) => a.date.localeCompare(b.date))
  const todays = weights.find(w => w.date === iso(today()))
  const [kg, setKg] = useState('')
  const save = useAction((v: number) => queuePost('/health/weights', { kg: v }), [['health']],
    r => { setKg(''); toast(isQueued(r) ? '⚖️ Saved · will sync when online' : '⚖️ Weight saved') },
    (qc, v) => patchCache<HealthSummary>(qc, ['health'], h => {
      const d = iso(today())
      const rest = h.weights.filter(w => w.date !== d)
      const w: WeightReading = { id: `local-${Date.now()}`, date: d, week: 0, kg: v }
      return { ...h, weights: [...rest, w], latestWeight: w }
    }))

  const submit = () => {
    const v = parseFloat(kg)
    if (!(v > 25 && v < 200)) return toast('Enter the weight in kg, e.g. 58.4')
    save.mutate(Math.round(v * 10) / 10)
  }
  const first = weights[0], last = weights[weights.length - 1]

  return (
    <>
      <header className="top"><div><small>Every day</small><h1>Weight</h1></div></header>
      <div className="card">
        <b style={{ fontSize: 15 }}>{todays ? `Today: ${todays.kg} kg` : "Today's weight"}</b>
        <div style={{ display: 'flex', gap: 10, marginTop: 12 }}>
          <input className="field" type="number" step="0.1" inputMode="decimal" placeholder={todays ? 'Update today' : 'e.g. 58.4'} value={kg}
            onChange={e => setKg(e.target.value)} onKeyDown={e => e.key === 'Enter' && submit()} aria-label="Weight in kg" />
          <button className="btn" onClick={submit} disabled={save.isPending}>Save</button>
        </div>
        {first && last && first !== last && (
          <p className="small muted" style={{ marginTop: 12 }}>
            {(Number(last.kg) - Number(first.kg) >= 0 ? '+' : '')}{(Number(last.kg) - Number(first.kg)).toFixed(1)} kg since {fmt(first.date, { day: 'numeric', month: 'short' })}
          </p>
        )}
      </div>
      <SecHead title="Weight journey" right={<span className="small muted">kg</span>} />
      <div className="card">{q.isLoading ? <Skeleton h={150} mt={0} /> : <WeightChart w={weights.slice(-30)} />}</div>
      <SecHead title="History" />
      <div className="card">
        {[...weights].reverse().map((w, i, arr) => {
          const prev = arr[i + 1]
          const diff = prev ? Number(w.kg) - Number(prev.kg) : 0
          return (
            <div className="rowi" key={w.id}>
              <div className="ico c-blush" style={{ fontSize: 20 }}>⚖️</div>
              <div className="grow"><b>{w.kg} kg</b><small>{fmt(w.date, { weekday: 'short', day: 'numeric', month: 'short' })}</small></div>
              {prev && <span className="timepill" style={{ color: diff > 0 ? 'var(--amber-ink)' : diff < 0 ? 'var(--sky-ink)' : undefined }}>{diff > 0 ? '+' : ''}{diff.toFixed(1)}</span>}
            </div>
          )
        })}
        {!weights.length && <p className="small muted">No readings yet. Weigh at the same time each day (e.g. morning, before breakfast).</p>}
      </div>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 16, lineHeight: 1.5 }}>Show this to your doctor at checkups. Amma does not judge or diagnose weight changes.</p>
    </>
  )
}

function WeightChart({ w }: { w: WeightReading[] }) {
  if (w.length < 2) return <p className="small muted">Add two or more days to see your chart.</p>
  const W = 320, H = 150, px = 26, py = 20
  const ys = w.map(p => Number(p.kg))
  const y0 = Math.min(...ys) - 0.5, y1 = Math.max(...ys) + 0.5
  const X = (i: number) => px + (i / (w.length - 1)) * (W - px * 2)
  const Y = (v: number) => H - py - ((v - y0) / (y1 - y0)) * (H - py * 2)
  const pts = w.map((p, i) => `${X(i).toFixed(1)},${Y(Number(p.kg)).toFixed(1)}`)
  const labelEvery = Math.ceil(w.length / 6)
  return (
    <svg className="chart" viewBox={`0 0 ${W} ${H}`} role="img" aria-label={`Weight chart, latest ${w[w.length - 1].kg} kg`}>
      <defs><linearGradient id="wg" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#C2497A" stopOpacity=".28" /><stop offset="1" stopColor="#C2497A" stopOpacity="0" /></linearGradient></defs>
      {[0, 1, 2].map(i => <line key={i} x1={px} x2={W - px} y1={py + (i * (H - py * 2)) / 2} y2={py + (i * (H - py * 2)) / 2} stroke="#F1E6E9" strokeDasharray="3 4" />)}
      <path className="area" d={`M${pts.join(' L')} L${X(w.length - 1)},${H - py} L${X(0)},${H - py}Z`} fill="url(#wg)" />
      <polyline className="draw" points={pts.join(' ')} fill="none" stroke="#C2497A" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" />
      {w.map((p, i) => (
        <g key={p.id}>
          <circle cx={X(i)} cy={Y(Number(p.kg))} r={i === w.length - 1 ? 5 : 3} fill="#fff" stroke="#C2497A" strokeWidth="2.5" />
          {(i % labelEvery === 0 || i === w.length - 1) && <text x={X(i)} y={H - 3} fontSize="9" fill="#9A8893" textAnchor="middle">{fmt(p.date, { day: 'numeric', month: 'short' })}</text>}
        </g>
      ))}
      <text x={X(w.length - 1)} y={Y(Number(w[w.length - 1].kg)) - 10} fontSize="11" fontWeight="700" fill="#5E1F47" textAnchor="end">{w[w.length - 1].kg} kg</text>
    </svg>
  )
}
