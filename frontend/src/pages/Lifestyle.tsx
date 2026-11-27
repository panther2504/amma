import { useEffect, useRef, useState } from 'react'
import { put } from '../api/client'
import { useAction, useBag, useContent } from '../api/hooks'
import { Check, Info, SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useApp, useSheet } from '../ui/shell'

export function ExercisePage() {
  const { lang } = useApp()
  const content = useContent(lang)
  const sheet = useSheet()
  const acts = (content.data?.snippets ?? []).filter(s => s.kind === 'Activity')
  return (
    <PageFrame title="Exercise & lifestyle">
      <button className="card tap" style={{ background: 'linear-gradient(135deg,var(--sage),#F2FAF5)', display: 'flex', gap: 14, alignItems: 'center', width: '100%', textAlign: 'left' }} onClick={() => sheet.open(<Breathing />)}>
        <div className="ico" style={{ background: '#fff', color: 'var(--sage-ink)' }}><Icon name="wind" /></div>
        <div className="grow"><b style={{ fontSize: 15 }}>Calm breathing · 2 min</b><small className="muted" style={{ display: 'block', marginTop: 2 }}>Relax your body and mind</small></div>
        <span className="btn sm" style={{ background: 'var(--sage-ink)', boxShadow: 'none' }}>Start</span>
      </button>
      <SecHead title="Gentle activities" />
      {acts.map(a => (
        <div className="food" key={a.key}><div className={`ico c-${a.group}`}>{a.emoji}</div>
          <div className="grow"><b style={{ fontSize: 14.5 }}>{a.title}</b><small className="muted" style={{ display: 'block', fontSize: 12.5, lineHeight: 1.45, marginTop: 2 }}>{a.body}</small></div></div>
      ))}
      <Info warn style={{ marginTop: 6 }}>Stop and contact your doctor if you feel dizzy, breathless before effort, chest pain, bleeding, fluid leak or painful contractions.</Info>
      <Info icon="shield" style={{ marginTop: 10 }}>If you have any pregnancy complication, check with your doctor before starting any activity.</Info>
    </PageFrame>
  )
}

function Breathing() {
  const [on, setOn] = useState(false)
  const [ph, setPh] = useState(-1)
  const t = useRef<number | undefined>(undefined)
  useEffect(() => {
    if (!on) { setPh(-1); return }
    setPh(0)
    t.current = window.setInterval(() => setPh(p => p + 1), 4000)
    return () => window.clearInterval(t.current)
  }, [on])
  const P: [string, boolean][] = [['Breathe in', true], ['Hold', true], ['Breathe out', false]]
  const cur = ph < 0 ? null : P[ph % 3]
  return (
    <>
      <h3 style={{ textAlign: 'center' }}>Calm breathing</h3>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 4 }}>Follow the circle</p>
      <div className="breath"><div className={`b-circle ${cur?.[1] ? 'in' : ''}`} aria-live="polite">{cur?.[0] ?? 'Ready'}</div></div>
      <button className="btn block" onClick={() => setOn(!on)}>{on ? 'Stop' : 'Begin'}</button>
    </>
  )
}

export function BagPage() {
  const bag = useBag()
  const set = useAction((a: { key: string; packed: boolean }) => put(`/tracking/hospital-bag/${a.key}`, { packed: a.packed }), [['bag']])
  const groups = ['Mother', 'Baby', 'Documents']
  const b = bag.data
  return (
    <PageFrame title="Hospital bag">
      {!b ? <Skeleton h={300} mt={0} /> : <>
        <div className="card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 10 }}><b>{b.percentPacked}% packed</b><span className="small muted">Aim to finish by week 35</span></div>
          <div className="progress"><i style={{ width: `${b.percentPacked}%` }} /></div>
        </div>
        {groups.map(g => {
          const items = b.items.filter(i => i.group === g)
          return (
            <div key={g}>
              <SecHead title={`${g === 'Mother' ? '👩' : g === 'Baby' ? '👶' : '📄'} ${g}`} right={<span className="small muted">{items.filter(i => i.packed).length}/{items.length}</span>} />
              <div className="card">
                {items.map(i => (
                  <div key={i.key} className={`check-item ${i.packed ? 'on' : ''}`} onClick={() => set.mutate({ key: i.key, packed: !i.packed })}>
                    <Check on={i.packed} label={i.title} /><span>{i.title}</span>
                  </div>
                ))}
              </div>
            </div>
          )
        })}
      </>}
    </PageFrame>
  )
}
