import { useMemo, useState } from 'react'
import { useContent } from '../api/hooks'
import type { Food, FoodSafety } from '../api/types'
import { Badge, Empty, ReviewNote, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useApp, usePages, useSheet } from '../ui/shell'

const FILTERS: [FoodSafety | 'all', string][] = [['all', 'All'], ['recommended', 'Recommended'], ['safeIfPrepared', 'Safe if cooked'], ['limit', 'Limit'], ['avoid', 'Avoid'], ['consultDoctor', 'Ask doctor']]

export function FoodPage() {
  const { lang } = useApp()
  const content = useContent(lang)
  const sheet = useSheet()
  const { open } = usePages()
  const [filter, setFilter] = useState<FoodSafety | 'all'>('all')
  const [q, setQ] = useState('')
  const list = useMemo(() => {
    const s = q.trim().toLowerCase()
    return (content.data?.foods ?? []).filter(f => (filter === 'all' || f.safety === filter) && (!s || `${f.name} ${f.nameTa ?? ''} ${f.category}`.toLowerCase().includes(s)))
  }, [content.data, filter, q])

  return (
    <PageFrame title="Food & nutrition">
      <div className="search"><Icon name="search" size={19} /><input placeholder="Search foods, e.g. papaya, ragi…" value={q} onChange={e => setQ(e.target.value)} aria-label="Search foods" /></div>
      <div className="chips">{FILTERS.map(([k, l]) => <button key={k} className={`chip ${filter === k ? 'on' : ''}`} onClick={() => setFilter(k)}>{l}</button>)}</div>
      <div style={{ marginTop: 12 }}>
        {!content.data ? <Skeleton h={300} /> : list.length ? list.map(f => (
          <button key={f.id} className="food tap" style={{ width: '100%', textAlign: 'left' }} onClick={() => sheet.open(<FoodSheet food={f} />)}>
            <div className="ico">{f.emoji}</div>
            <div className="grow"><b style={{ fontSize: 14.5 }}>{f.name}</b><div className="ta">{f.nameTa}</div><small className="muted" style={{ fontSize: 12 }}>{f.nutrients ?? f.category}</small></div>
            <Badge safety={f.safety} />
          </button>
        )) : (
          <div className="card empty"><div>🔍</div><p className="small muted" style={{ marginTop: 6 }}>No foods found.</p>
            <button className="btn sm ghost" style={{ marginTop: 10 }} onClick={() => open('chat')}><Icon name="spark" size={14} /> Ask Amma</button></div>
        )}
      </div>
      {content.data && list.some(f => !f.reviewed) && <ReviewNote>Starter guidance awaiting medical review. Final food content must be approved by qualified medical/nutrition professionals.</ReviewNote>}
    </PageFrame>
  )
}

export function FoodSheet({ food: f }: { food: Food }) {
  return (
    <>
      <div style={{ textAlign: 'center' }}>
        <div style={{ fontSize: 64, animation: 'popIn .6s var(--spring)' }}>{f.emoji}</div>
        <h3 style={{ marginTop: 6 }}>{f.name}</h3>
        <p className="small muted ta">{f.nameTa} · {f.category}</p>
        <span style={{ display: 'inline-block', marginTop: 10 }}><Badge safety={f.safety} /></span>
      </div>
      {f.nutrients && <div className="card" style={{ marginTop: 18 }}><small className="muted" style={{ fontWeight: 700 }}>NUTRIENTS</small><p style={{ marginTop: 4, fontWeight: 600 }}>{f.nutrients}</p></div>}
      {f.benefits && <div className="card"><small style={{ color: 'var(--sage-ink)', fontWeight: 700 }}>✓ BENEFITS</small><p className="small" style={{ marginTop: 4, lineHeight: 1.55 }}>{f.benefits}</p></div>}
      {f.preparation && <div className="card"><small style={{ color: 'var(--sky-ink)', fontWeight: 700 }}>🍳 HOW TO HAVE IT</small><p className="small" style={{ marginTop: 4, lineHeight: 1.55 }}>{f.preparation}</p></div>}
      <div className="card"><small style={{ color: 'var(--amber-ink)', fontWeight: 700 }}>⚠ PRECAUTIONS</small><p className="small" style={{ marginTop: 4, lineHeight: 1.55 }}>{f.precautions}</p></div>
      <ReviewNote>{f.reviewed ? 'Medically reviewed.' : 'Pending medical review.'} If you have allergies or a medical condition, ask your doctor.</ReviewNote>
    </>
  )
}

export { Empty }
