import { useEffect, useState, type ReactNode } from 'react'
import { Icon } from './Icon'
import type { FoodSafety } from '../api/types'

export const SAFETY: Record<FoodSafety, [string, string]> = {
  recommended: ['Recommended', 'b-rec'],
  safeIfPrepared: ['Safe if prepared well', 'b-safe'],
  limit: ['Limit', 'b-limit'],
  avoid: ['Avoid', 'b-avoid'],
  consultDoctor: ['Ask your doctor', 'b-consult'],
}
export const colorClass = (k: string) => `c-${k}`

export function Badge({ safety }: { safety: FoodSafety }) {
  const [l, c] = SAFETY[safety]
  return <span className={`badge ${c}`}>{l}</span>
}

/** Progress ring. Animates from empty to `pct` after mount. */
export function Ring({ pct, size = 104, stroke, track, children }: { pct: number; size?: number; stroke?: string; track?: string; children?: ReactNode }) {
  const [shown, setShown] = useState(0)
  useEffect(() => { const r = requestAnimationFrame(() => requestAnimationFrame(() => setShown(pct))); return () => cancelAnimationFrame(r) }, [pct])
  const C = 326.7
  return (
    <div className="ring" style={{ width: size, height: size }}>
      <svg viewBox="0 0 120 120">
        <circle cx="60" cy="60" r="52" className="ring-bg" style={track ? { stroke: track } : undefined} />
        <circle cx="60" cy="60" r="52" className="ring-fg" strokeDasharray={C} strokeDashoffset={C * (1 - shown / 100)} style={stroke ? { stroke } : undefined} />
      </svg>
      <div className="ring-txt">{children}</div>
    </div>
  )
}

export function Check({ on, skip, onClick, label }: { on?: boolean; skip?: boolean; onClick?: () => void; label: string }) {
  return (
    <button className={`chk ${on ? 'on' : skip ? 'skip' : ''}`} onClick={onClick} aria-label={label} aria-pressed={!!on}>
      <Icon name={skip ? 'x' : 'check'} size={15} stroke={3} />
    </button>
  )
}

export function Switch({ on, onChange, label }: { on: boolean; onChange: (v: boolean) => void; label: string }) {
  return <button className={`switch ${on ? 'on' : ''}`} role="switch" aria-checked={on} aria-label={label} onClick={() => onChange(!on)} />
}

export function Choice<T extends string>({ value, options, onChange, className }: { value: T; options: [T, ReactNode][]; onChange: (v: T) => void; className?: string }) {
  return (
    <div className={`choice ${className ?? ''}`} role="radiogroup">
      {options.map(([v, l]) => (
        <button key={v} type="button" role="radio" aria-checked={v === value} className={v === value ? 'on' : ''} onClick={() => onChange(v)}>{l}</button>
      ))}
    </div>
  )
}

export function Segment({ items, value, onChange }: { items: string[]; value: number; onChange: (i: number) => void }) {
  return (
    <div className="segment" style={{ gridTemplateColumns: `repeat(${items.length},1fr)` }}>
      <i className="seg-ind" style={{ width: `calc((100% - 8px)/${items.length})`, transform: `translateX(${value * 100}%)` }} />
      {items.map((s, i) => <button key={s} className={i === value ? 'on' : ''} onClick={() => onChange(i)}>{s}</button>)}
    </div>
  )
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label style={{ display: 'block' }}><span className="label">{label}</span>{children}</label>
}

export function Info({ warn, icon, children, style }: { warn?: boolean; icon?: 'info' | 'alert' | 'shield'; children: ReactNode; style?: React.CSSProperties }) {
  return <div className={`info ${warn ? 'warn' : ''}`} style={style}><Icon name={icon ?? (warn ? 'alert' : 'info')} size={18} /><span>{children}</span></div>
}

export function ReviewNote({ children }: { children?: ReactNode }) {
  return (
    <div className="review-note" style={{ marginTop: 12 }}>
      <Icon name="info" size={16} />
      <span>{children ?? 'Pending medical review. Content will be approved by qualified reviewers before publishing.'}</span>
    </div>
  )
}

export function Empty({ emoji, text }: { emoji: string; text: string }) {
  return <div className="card empty"><div>{emoji}</div><p className="small muted" style={{ marginTop: 6 }}>{text}</p></div>
}

export function SecHead({ title, action, onAction, right }: { title: string; action?: string; onAction?: () => void; right?: ReactNode }) {
  return <div className="h-sec"><h3>{title}</h3>{action ? <button onClick={onAction}>{action}</button> : right}</div>
}

export function Skeleton({ h = 120, mt = 12 }: { h?: number; mt?: number }) {
  return <div className="skeleton" style={{ height: h, marginTop: mt }} aria-hidden="true" />
}
