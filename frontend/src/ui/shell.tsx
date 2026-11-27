import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { Icon } from './Icon'
import { translate, type Lang, type TKey } from '../lib/i18n'
import type { User } from '../api/types'

// ---------------------------------------------------------------- App context
export interface AppCtx {
  user: User
  lang: Lang
  t: (k: TKey) => string
  setUser: (u: User) => void
  signOut: () => void
}
export const AppContext = createContext<AppCtx | null>(null)
export const useApp = () => {
  const c = useContext(AppContext)
  if (!c) throw new Error('AppContext missing')
  return c
}
export const useT = () => {
  const c = useContext(AppContext)
  return useCallback((k: TKey) => translate(c?.lang ?? 'en', k), [c?.lang])
}

// ---------------------------------------------------------------- Toast
const ToastCtx = createContext<(msg: string) => void>(() => {})
export const useToast = () => useContext(ToastCtx)

export function ToastProvider({ children }: { children: ReactNode }) {
  const [msg, setMsg] = useState('')
  const [on, setOn] = useState(false)
  const timer = useRef<number | undefined>(undefined)
  const show = useCallback((m: string) => {
    setMsg(m); setOn(true)
    window.clearTimeout(timer.current)
    timer.current = window.setTimeout(() => setOn(false), 2400)
  }, [])
  return (
    <ToastCtx.Provider value={show}>
      {children}
      <div className={`toast ${on ? 'on' : ''}`} role="status" aria-live="polite">{msg}</div>
    </ToastCtx.Provider>
  )
}

// ---------------------------------------------------------------- Bottom sheet
interface SheetApi { open: (node: ReactNode) => void; close: () => void }
const SheetCtx = createContext<SheetApi>({ open: () => {}, close: () => {} })
export const useSheet = () => useContext(SheetCtx)

export function SheetProvider({ children }: { children: ReactNode }) {
  const [node, setNode] = useState<ReactNode>(null)
  const [on, setOn] = useState(false)
  const sheetRef = useRef<HTMLDivElement>(null)
  const bodyRef = useRef<HTMLDivElement>(null)
  const api = useMemo<SheetApi>(() => ({
    open: n => { setNode(n); setOn(true); requestAnimationFrame(() => { if (bodyRef.current) bodyRef.current.scrollTop = 0 }) },
    close: () => setOn(false),
  }), [])

  // Swipe down to close.
  useEffect(() => {
    const sh = sheetRef.current
    if (!sh) return
    let y0: number | null = null, dy = 0
    const start = (e: TouchEvent) => { if ((bodyRef.current?.scrollTop ?? 0) > 0) return; y0 = e.touches[0].clientY; sh.style.transition = 'none' }
    const move = (e: TouchEvent) => { if (y0 === null) return; dy = Math.max(0, e.touches[0].clientY - y0); sh.style.transform = `translateY(${dy}px)` }
    const end = () => { sh.style.transition = ''; sh.style.transform = ''; if (dy > 110) setOn(false); y0 = null; dy = 0 }
    sh.addEventListener('touchstart', start, { passive: true }); sh.addEventListener('touchmove', move, { passive: true }); sh.addEventListener('touchend', end)
    return () => { sh.removeEventListener('touchstart', start); sh.removeEventListener('touchmove', move); sh.removeEventListener('touchend', end) }
  }, [])

  useEffect(() => {
    if (!on) return
    const k = (e: KeyboardEvent) => e.key === 'Escape' && setOn(false)
    addEventListener('keydown', k)
    return () => removeEventListener('keydown', k)
  }, [on])

  return (
    <SheetCtx.Provider value={api}>
      {children}
      <div className={`backdrop ${on ? 'on' : ''}`} onClick={api.close} />
      <div className={`sheet ${on ? 'on' : ''}`} ref={sheetRef} role="dialog" aria-modal="true" aria-hidden={!on}>
        <div className="grab" />
        <div id="sheetBody" ref={bodyRef}>{node}</div>
      </div>
    </SheetCtx.Provider>
  )
}

// ---------------------------------------------------------------- Page stack (slide-in pages over the tabs)
export type PageKey = string
interface Entry { id: number; key: PageKey; arg?: string; open: boolean }
interface PagesApi { open: (key: PageKey, arg?: string) => void; close: () => void; depth: number }
const PagesCtx = createContext<PagesApi>({ open: () => {}, close: () => {}, depth: 0 })
export const usePages = () => useContext(PagesCtx)

export type PageRenderer = (arg: string | undefined) => ReactNode

export function PageStack({ pages, children }: { pages: Record<string, PageRenderer>; children: ReactNode }) {
  const [stack, setStack] = useState<Entry[]>([])
  const seq = useRef(0)
  const stackRef = useRef(stack)
  stackRef.current = stack

  const popTop = useCallback(() => {
    const top = stackRef.current.filter(e => e.open).at(-1)
    if (!top) return
    setStack(s => s.map(e => (e.id === top.id ? { ...e, open: false } : e)))
    window.setTimeout(() => setStack(s => s.filter(e => e.id !== top.id)), 480)
  }, [])

  // Hardware / browser back button closes the top page.
  useEffect(() => {
    const onPop = () => popTop()
    addEventListener('popstate', onPop)
    return () => removeEventListener('popstate', onPop)
  }, [popTop])

  const api = useMemo<PagesApi>(() => ({
    open: (key, arg) => {
      const id = ++seq.current
      setStack(s => [...s, { id, key, arg, open: false }])
      requestAnimationFrame(() => requestAnimationFrame(() => setStack(s => s.map(e => (e.id === id ? { ...e, open: true } : e)))))
      history.pushState({ ammaPage: id }, '')
    },
    close: () => { if (history.state?.ammaPage) history.back(); else popTop() },
    depth: stack.filter(e => e.open).length,
  }), [popTop, stack])

  return (
    <PagesCtx.Provider value={api}>
      {children}
      <div className={`page-dim ${stack.length ? 'on' : ''}`} />
      {stack.map(e => (
        <PageSlot key={e.id} open={e.open}>{pages[e.key]?.(e.arg) ?? <PageFrame title="Not found"><p className="muted">This page is not available.</p></PageFrame>}</PageSlot>
      ))}
    </PagesCtx.Provider>
  )
}

const SlotCtx = createContext(false)
function PageSlot({ open, children }: { open: boolean; children: ReactNode }) {
  return <SlotCtx.Provider value={open}>{children}</SlotCtx.Provider>
}

/** Standard page chrome: back button, title, optional right action; body scrolls unless `chat` layout. */
export function PageFrame({ title, right, chat, children }: { title: string; right?: ReactNode; chat?: boolean; children: ReactNode }) {
  const open = useContext(SlotCtx)
  const { close } = usePages()
  const bodyRef = useRef<HTMLDivElement>(null)
  useEffect(() => { if (open && bodyRef.current && !chat) stagger(bodyRef.current) }, [open, chat])
  return (
    <section className={`page ${chat ? 'chat' : ''} ${open ? 'open' : ''}`} aria-label={title || 'Page'}>
      <header className="phead">
        <button className="icon-btn" onClick={close} aria-label="Back"><Icon name="back" size={20} /></button>
        <h2>{title}</h2>
        {right}
      </header>
      <div className={`pbody ${chat ? '' : 'scroll'}`} ref={bodyRef}>{children}</div>
    </section>
  )
}

/** Adds the staggered "rise" entrance animation used throughout the design. */
export function stagger(root: HTMLElement) {
  Array.from(root.children).forEach((el, i) => {
    const h = el as HTMLElement
    h.style.setProperty('--i', String(Math.min(i, 10)))
    h.classList.remove('rise'); void h.offsetWidth; h.classList.add('rise')
  })
}

// Ripple feedback on buttons, tiles and menu items.
export function useRipple() {
  useEffect(() => {
    const h = (e: PointerEvent) => {
      const b = (e.target as HTMLElement).closest<HTMLElement>('.btn,.tile,.mi,.call')
      if (!b) return
      const r = b.getBoundingClientRect(), s = Math.max(b.offsetWidth, b.offsetHeight)
      const sp = document.createElement('span')
      sp.className = 'ripple'
      sp.style.cssText = `width:${s}px;height:${s}px;left:${e.clientX - r.left - s / 2}px;top:${e.clientY - r.top - s / 2}px`
      if (getComputedStyle(b).position === 'static') b.style.position = 'relative'
      b.style.overflow = 'hidden'
      b.appendChild(sp)
      setTimeout(() => sp.remove(), 650)
    }
    document.addEventListener('pointerdown', h)
    return () => document.removeEventListener('pointerdown', h)
  }, [])
}
