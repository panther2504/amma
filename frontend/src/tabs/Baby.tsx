import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import { useContent, useDashboard, useKicks } from '../api/hooks'
import { Segment, SecHead, Skeleton, ReviewNote } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { useApp, usePages } from '../ui/shell'
import { clamp, fmt, ordinal, triOf } from '../lib/util'

export function BabyTab() {
  const { lang } = useApp()
  const { open } = usePages()
  const content = useContent(lang)
  const dash = useDashboard()
  const kicks = useKicks()
  const current = clamp(dash.data?.pregnancy?.gestation.weeks ?? 20, 4, 40)
  const [week, setWeek] = useState<number | null>(null)
  const [seg, setSeg] = useState(0)
  const [pop, setPop] = useState(0)
  const vw = week ?? current
  const scroller = useRef<HTMLDivElement>(null)

  const centre = (smooth: boolean) => {
    const c = scroller.current, el = c?.querySelector<HTMLElement>('.wk.on')
    if (!c || !el) return
    c.scrollTo({ left: el.offsetLeft - c.offsetWidth / 2 + el.offsetWidth / 2, behavior: smooth ? 'smooth' : 'auto' })
  }
  useLayoutEffect(() => centre(false), [current])
  useEffect(() => { if (week !== null) centre(true) }, [week])

  const w = content.data?.weeks.find(x => x.week === vw)
  const guide = content.data?.guides.find(g => vw >= g.fromWeek && vw <= g.toWeek)
  const milestones = (content.data?.snippets ?? []).filter(s => s.kind === 'Milestone')
  const gw = dash.data?.pregnancy?.gestation.weeks ?? 0
  const last = kicks.data?.[0]

  return (
    <>
      <header className="top">
        <div><small>Pregnancy journey</small><h1>Your baby</h1></div>
        <button className="icon-btn" onClick={() => open('kick')} aria-label="Kick counter"><Icon name="foot" size={20} /></button>
      </header>
      <div className="weeks" ref={scroller} role="tablist" aria-label="Pregnancy week">
        {Array.from({ length: 37 }, (_, i) => i + 4).map(n => (
          <button key={n} role="tab" aria-selected={n === vw} className={`wk ${n === vw ? 'on' : ''} ${n === current ? 'now' : ''}`}
            onClick={() => { setWeek(n); setPop(p => p + 1) }}>
            <small>{n === current ? 'Now' : 'Wk'}</small>{n}
          </button>
        ))}
      </div>

      {!w ? <Skeleton h={320} /> : (
        <div key={pop} className={pop ? 'pop' : ''}>
          <div className="fruit-stage">
            <div className="fruit-orb"><span className="orb-ring r2" /><span className="orb-ring r1" /><div className="fruit-big">{w.emoji}</div></div>
            <p className="fruit-cap">Week {vw} · about the size of a</p>
            <h2 className="fruit-name">{w.sizeName}</h2>
            <div className="measure">
              <div><small>Length</small><b>{w.length}</b></div>
              <div><small>Weight</small><b>{w.weight}</b></div>
              <div><small>Trimester</small><b>{ordinal(triOf(vw))}</b></div>
            </div>
          </div>
          <Segment items={['Baby', 'Mother', 'Tips']} value={seg} onChange={setSeg} />
          <div className="card seg-body fade" key={seg}>
            <ul className="bullets">{(guide ? [guide.baby, guide.mother, guide.tips][seg] : []).map(x => <li key={x}>{x}</li>)}</ul>
          </div>
          {guide && !guide.reviewed && <ReviewNote />}
        </div>
      )}

      <button className="card tap" style={{ marginTop: 14, display: 'flex', gap: 14, alignItems: 'center', width: '100%', textAlign: 'left' }} onClick={() => open('kick')}>
        <div className="ico c-blush"><Icon name="foot" /></div>
        <div className="grow"><b style={{ fontSize: 15 }}>Baby movement tracker</b><small className="muted" style={{ display: 'block', marginTop: 2 }}>Last session: {last ? `${last.movements} movements in ${last.durationMinutes} min` : 'none yet'}</small></div>
        <Icon name="chev" size={20} />
      </button>

      <SecHead title="Milestones" />
      <div className="card timeline">
        {milestones.map(m => {
          const mw = m.week ?? 0
          const state = gw > mw ? 'done' : gw >= mw - 1 && gw <= mw + 3 ? 'now' : ''
          return (
            <div key={m.key} className={`tl ${state}`}>
              <span className="node">{gw > mw && <Icon name="check" size={12} stroke={3.5} />}</span>
              <div><b>Week {mw} · {m.title}</b><small>{mw === 40 && dash.data?.pregnancy ? fmt(dash.data.pregnancy.edd) : m.body}</small></div>
            </div>
          )
        })}
      </div>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 16 }}>Sizes are approximate averages. Every baby grows at their own pace.</p>
    </>
  )
}
