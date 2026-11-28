import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { OfflineError, cachedUser, get, hasStoredSession, logout, onOutboxChange, onSignedOut, refreshSession } from './api/client'
import { flushOutbox, pendingCount } from './lib/sync'
import { CHANNEL, clearOccurrence, prepareNotifications, scheduleAll, snooze } from './lib/reminders'
import { checkForUpdate, dueForCheck } from './lib/updates'
import { UpdateSheet } from './ui/UpdateSheet'
import { useAppointments, useDay, useReminders } from './api/hooks'
import { isNative } from './lib/localdb'
import { App as NativeApp } from '@capacitor/app'
import { LocalNotifications } from '@capacitor/local-notifications'
import type { AuthResponse, Day, User } from './api/types'
import { AppContext, useApp as useAppCtx, useSheet, PageStack, SheetProvider, ToastProvider, stagger, useRipple, type PageRenderer } from './ui/shell'
import { Icon, type IconName } from './ui/Icon'
import { Onboarding, Splash } from './screens/Intro'
import { Login } from './screens/Login'
import { Setup } from './screens/Setup'
import { TodayTab } from './tabs/Today'
import { WeightTab } from './tabs/Weight'
import { CheckupsTab } from './tabs/Checkups'
import { MemoriesTab } from './tabs/Memories'
import { SettingsTab } from './tabs/Settings'
import { RemindersPage } from './pages/RemindersEdit'
import { PregnancyPage } from './pages/Account'
import { DrivePage } from './pages/Drive'
import { queuePost } from './api/client'
import { translate } from './lib/i18n'

const PAGES: Record<string, PageRenderer> = {
  reminders: () => <RemindersPage />,
  pregnancy: () => <PregnancyPage />,
  drive: () => <DrivePage />,
}

type Stage = 'splash' | 'onboard' | 'login' | 'setup' | 'app'
const SEEN_ONBOARDING = 'amma.onboarded'

export default function App() {
  const qc = useQueryClient()
  const [stage, setStage] = useState<Stage>('splash')
  const [user, setUser] = useState<User | null>(null)
  useRipple()

  const enter = useCallback((u: User) => { setUser(u); setStage(u.hasPregnancy ? 'app' : 'setup') }, [])

  // Boot: show the splash briefly while restoring the session.
  useEffect(() => {
    const minDelay = new Promise(r => setTimeout(r, 1600))
    // Offline (or the server is waking up too slowly): open with the last signed-in user and saved data.
    const restore = hasStoredSession()
      ? refreshSession().then(a => a?.user ?? null, async e => (e instanceof OfflineError ? cachedUser() : null))
      : Promise.resolve(null)
    Promise.all([restore, minDelay]).then(([u]) => {
      if (u) enter(u)
      else setStage(localStorage.getItem(SEEN_ONBOARDING) ? 'login' : 'onboard')
    })
  }, [enter])

  useEffect(() => onSignedOut(() => { qc.clear(); setUser(null); setStage('login') }), [qc])

  const ctx = useMemo(() => user && {
    user, lang: user.language, t: (k: Parameters<typeof translate>[1]) => translate(user.language, k),
    setUser: (u: User) => setUser(u), signOut: () => { logout() },
  }, [user])

  useEffect(() => { document.documentElement.lang = user?.language ?? 'en'; document.documentElement.classList.toggle('lang-ta', user?.language === 'ta') }, [user?.language])

  return (
    <div className="app-wrap">
      <div className="device">
        <div className="screen"><ToastProvider><SheetProvider>
          <OfflineBar />
          {stage === 'splash' && <Splash />}
          {stage === 'onboard' && <Onboarding onDone={() => { try { localStorage.setItem(SEEN_ONBOARDING, '1') } catch { /* ignore */ } setStage('login') }} />}
          {stage === 'login' && <Login onSignedIn={(a: AuthResponse) => enter(a.user)} />}
          {stage === 'setup' && user && <Setup user={user} onDone={u => { setUser(u); qc.invalidateQueries(); setStage('app') }} />}
          {stage === 'app' && ctx && (
            <AppContext.Provider value={ctx}>
              <SyncManager />
              <PageStack pages={PAGES}><ReminderBridge /><UpdateChecker /><Shell /></PageStack>
            </AppContext.Provider>
          )}
        </SheetProvider></ToastProvider></div>
      </div>
    </div>
  )
}

const TABS: { key: string; icon: IconName; label: string }[] = [
  { key: 'today', icon: 'bell', label: 'Today' }, { key: 'weight', icon: 'scale', label: 'Weight' }, { key: 'checkups', icon: 'cal', label: 'Checkups' },
  { key: 'memories', icon: 'camera', label: 'Memories' }, { key: 'settings', icon: 'sliders', label: 'Settings' },
]

function Shell() {
  const [tab, setTab] = useState('today')
  const view = useRef<HTMLElement>(null)
  const nav = useRef<HTMLElement>(null)
  const pill = useRef<HTMLSpanElement>(null)
  const tabs = TABS

  const go = useCallback((k: string) => {
    if (k === tab) { view.current?.scrollTo({ top: 0, behavior: 'smooth' }); return }
    setTab(k)
  }, [tab])

  useLayoutEffect(() => {
    const move = () => {
      const b = nav.current?.querySelector<HTMLElement>(`button[data-tab="${tab}"]`)
      if (b && pill.current) { pill.current.style.width = `${b.offsetWidth}px`; pill.current.style.transform = `translateX(${b.offsetLeft - 8}px)` }
    }
    move()
    addEventListener('resize', move)
    return () => removeEventListener('resize', move)
  }, [tab, tabs.length])

  useEffect(() => {
    const v = view.current
    if (!v) return
    v.scrollTop = 0
    v.classList.remove('swap'); void v.offsetWidth; v.classList.add('swap')
    stagger(v)
  }, [tab])

  return (
    <section id="app" className="layer">
      <main ref={view} className="view scroll">
        {tab === 'today' && <TodayTab go={go} />}
        {tab === 'weight' && <WeightTab />}
        {tab === 'checkups' && <CheckupsTab />}
        {tab === 'memories' && <MemoriesTab />}
        {tab === 'settings' && <SettingsTab />}
      </main>
      <nav className="nav" ref={nav} aria-label="Main">
        <span className="nav-pill" ref={pill} />
        {tabs.map(x => (
          <button key={x.key} data-tab={x.key} className={tab === x.key ? 'on' : ''} aria-current={tab === x.key ? 'page' : undefined} onClick={() => go(x.key)}>
            <Icon name={x.icon} size={22} /><span>{x.label}</span>
          </button>
        ))}
      </nav>
    </section>
  )
}

/** Sends changes saved offline when the connection returns, the app resumes, and every minute. */
function SyncManager() {
  const qc = useQueryClient()
  const { lang } = useAppCtx()
  useEffect(() => {
    // Load every main screen's data while online so all of it is saved on the phone for offline use.
    const warm = () => {
      const pre: [string[], string][] = [[['reminders'], '/reminders'], [['day'], '/reminders/day'], [['appts'], '/appointments'], [['docs'], '/documents'],
        [['health'], '/health/summary'], [['memories'], '/memories'], [['pregnancy'], '/pregnancy'], [['person'], '/me/person']]
      pre.forEach(([key, path]) => qc.prefetchQuery({ queryKey: key, queryFn: () => get(path), staleTime: 60_000 }).catch(() => {}))
    }
    const run = () => { flushOutbox(qc).then(warm).catch(() => {}) }
    run()
    addEventListener('online', run)
    const t = setInterval(run, 60_000)
    const sub = isNative() ? NativeApp.addListener('resume', run) : null
    return () => { removeEventListener('online', run); clearInterval(t); sub?.then(h => h.remove()) }
  }, [qc, lang])
  return null
}

/**
 * Keeps this phone's reminders in step with the shared schedule:
 * - schedules 7 days of reminders (with sound) whenever reminders, today's progress or appointments change,
 * - handles the notification buttons (Done / Snooze) even offline,
 * - when the other phone marks something done, removes it from this phone too.
 */
function ReminderBridge() {
  const qc = useQueryClient()
  const reminders = useReminders()
  const day = useDay()
  const appts = useAppointments()

  // What's done today, as a compact key, so we only reschedule when something actually changes.
  const doneKey = (day.data?.items ?? []).filter(i => i.status).map(i => i.reminderId + i.time).join(',')
  const remKey = JSON.stringify(reminders.data ?? null)
  const apptKey = JSON.stringify((appts.data ?? []).map(a => [a.id, a.date, a.time, a.status, a.reminderMinutesBefore]))
  const prevDone = useRef<Set<string>>(new Set())

  useEffect(() => {
    if (!reminders.data) return
    scheduleAll(reminders.data, day.data, appts.data ?? []).catch(e => console.warn('schedule', e))
    // Anything newly marked done (here or on the other phone) is cleared from the notification tray.
    const now = new Set((day.data?.items ?? []).filter(i => i.status).map(i => `${i.reminderId}|${i.time.slice(0, 5)}`))
    now.forEach(k => { if (!prevDone.current.has(k)) { const [id, t] = k.split('|'); clearOccurrence(id, t) } })
    prevDone.current = now
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remKey, doneKey, apptKey])

  useEffect(() => {
    if (!isNative()) return
    prepareNotifications().catch(() => {})
    const action = LocalNotifications.addListener('localNotificationActionPerformed', async e => {
      const x = e.notification.extra as { type?: string; reminderId?: string; time?: string; date?: string } | undefined
      if (x?.type !== 'reminder' || !x.reminderId || !x.time) return
      if (e.actionId === 'snooze') { await snooze({ ...e.notification, channelId: CHANNEL }); return }
      if (e.actionId === 'done') {
        await clearOccurrence(x.reminderId, x.time)
        qc.setQueryData<Day>(['day'], d => d && { ...d, items: d.items.map(i => (i.reminderId === x.reminderId && i.time.startsWith(x.time!) ? { ...i, status: 'done', byName: 'You' } : i)) })
        await queuePost(`/reminders/${x.reminderId}/log`, { date: x.date, time: x.time, status: 'done' }).catch(() => {})
      }
      qc.invalidateQueries({ queryKey: ['day'] })
    })
    const resume = NativeApp.addListener('resume', () => { qc.invalidateQueries({ queryKey: ['day'] }); qc.invalidateQueries({ queryKey: ['reminders'] }) })
    return () => { action.then(h => h.remove()); resume.then(h => h.remove()) }
  }, [qc])
  return null
}

/** Checks for a new app version on start and when the app comes back to the foreground (every few hours). */
function UpdateChecker() {
  const sheet = useSheet()
  useEffect(() => {
    if (!isNative()) return
    const run = () => {
      if (!dueForCheck()) return
      checkForUpdate().then(r => { if (r) sheet.open(<UpdateSheet release={r} />) }).catch(() => {})
    }
    const t = setTimeout(run, 4000)
    const sub = NativeApp.addListener('resume', run)
    return () => { clearTimeout(t); sub.then(h => h.remove()) }
  }, [sheet])
  return null
}

function OfflineBar() {
  const [online, setOnline] = useState(navigator.onLine)
  const [pending, setPending] = useState(0)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    const count = () => { pendingCount().then(setPending).catch(() => {}) }
    count()
    const unsub = onOutboxChange(count)
    addEventListener('online', on); addEventListener('offline', off)
    return () => { unsub(); removeEventListener('online', on); removeEventListener('offline', off) }
  }, [])
  if (online && !pending) return null
  const waiting = pending ? `${pending} change${pending === 1 ? '' : 's'} saved on this phone will sync when you're online.` : ''
  return <div className="offline-bar" role="status">{online ? waiting : `You're offline — showing saved information. ${waiting || 'Emergency numbers still work.'}`}</div>
}
