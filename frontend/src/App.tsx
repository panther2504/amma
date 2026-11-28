import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { OfflineError, cachedUser, get, hasStoredSession, logout, onOutboxChange, onSignedOut, refreshSession } from './api/client'
import { flushOutbox, pendingCount } from './lib/sync'
import { onReminderTap, syncReminders } from './lib/reminders'
import { useAppointments, useMedications, useNotificationPrefs } from './api/hooks'
import { isNative } from './lib/localdb'
import { App as NativeApp } from '@capacitor/app'
import type { AuthResponse, User } from './api/types'
import { translate } from './lib/i18n'
import { AppContext, useApp as useAppCtx, usePages, PageStack, SheetProvider, ToastProvider, stagger, useRipple, type PageRenderer } from './ui/shell'
import { Icon, type IconName } from './ui/Icon'
import { Onboarding, Splash } from './screens/Intro'
import { Login } from './screens/Login'
import { Setup } from './screens/Setup'
import { SharedHome } from './screens/Shared'
import { HomeTab } from './tabs/Home'
import { BabyTab } from './tabs/Baby'
import { CareTab } from './tabs/Care'
import { LearnTab } from './tabs/Learn'
import { ProfileTab } from './tabs/Profile'
import { FoodPage } from './pages/Food'
import { MedsPage } from './pages/Meds'
import { AppointmentsPage } from './pages/Appointments'
import { HealthPage } from './pages/Health'
import { KickPage } from './pages/Kick'
import { BagPage, ExercisePage } from './pages/Lifestyle'
import { ChatPage } from './pages/Chat'
import { ArticlePage } from './pages/Article'
import { ReportsPage } from './pages/Reports'
import { FamilyPage } from './pages/Family'
import { ContactsPage, EmergencyPage } from './pages/Emergency'
import { PregnancyPage, PrivacyPage, SettingsPage } from './pages/Account'

const PAGES: Record<string, PageRenderer> = {
  food: () => <FoodPage />, meds: () => <MedsPage />, appts: () => <AppointmentsPage />, health: () => <HealthPage />,
  kick: () => <KickPage />, exercise: () => <ExercisePage />, bag: () => <BagPage />, chat: () => <ChatPage />,
  article: slug => <ArticlePage slug={slug} />, reports: () => <ReportsPage />, family: () => <FamilyPage />,
  emergency: () => <EmergencyPage />, contacts: () => <ContactsPage />, pregnancy: () => <PregnancyPage />,
  settings: () => <SettingsPage />, privacy: () => <PrivacyPage />,
}

type Stage = 'splash' | 'onboard' | 'login' | 'setup' | 'app'
const SEEN_ONBOARDING = 'amma.onboarded'
const isFamilyOnly = (u: User) => !u.hasPregnancy && (u.role === 'partner' || u.role === 'familyViewer')

export default function App() {
  const qc = useQueryClient()
  const [stage, setStage] = useState<Stage>('splash')
  const [user, setUser] = useState<User | null>(null)
  useRipple()

  const enter = useCallback((u: User) => { setUser(u); setStage(u.hasPregnancy || isFamilyOnly(u) ? 'app' : 'setup') }, [])

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
              <PageStack pages={PAGES}><DeviceReminders /><Shell familyOnly={isFamilyOnly(ctx.user)} /></PageStack>
            </AppContext.Provider>
          )}
        </SheetProvider></ToastProvider></div>
      </div>
    </div>
  )
}

const TABS: { key: string; icon: IconName }[] = [
  { key: 'home', icon: 'home' }, { key: 'baby', icon: 'baby' }, { key: 'care', icon: 'care' }, { key: 'learn', icon: 'learn' }, { key: 'profile', icon: 'profile' },
]

function Shell({ familyOnly }: { familyOnly: boolean }) {
  const [tab, setTab] = useState('home')
  const view = useRef<HTMLElement>(null)
  const nav = useRef<HTMLElement>(null)
  const pill = useRef<HTMLSpanElement>(null)
  const tabs = familyOnly ? TABS.filter(t => ['home', 'learn', 'profile'].includes(t.key)) : TABS
  const t = (k: string) => translate((document.documentElement.lang as 'en' | 'ta') || 'en', k as never)

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
        {tab === 'home' && (familyOnly ? <SharedHome /> : <HomeTab go={go} />)}
        {tab === 'baby' && <BabyTab />}
        {tab === 'care' && <CareTab />}
        {tab === 'learn' && <LearnTab />}
        {tab === 'profile' && <ProfileTab />}
      </main>
      <nav className="nav" ref={nav} aria-label="Main">
        <span className="nav-pill" ref={pill} />
        {tabs.map(x => (
          <button key={x.key} data-tab={x.key} className={tab === x.key ? 'on' : ''} aria-current={tab === x.key ? 'page' : undefined} onClick={() => go(x.key)}>
            <Icon name={x.icon} size={22} /><span>{t(x.key)}</span>
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
      const pre: [string[], string][] = [[['content', lang], `/content/bundle?lang=${lang}`], [['doses'], '/medications/doses'], [['meds'], '/medications'],
        [['appts'], '/appointments'], [['health'], '/health/summary'], [['kicks'], '/health/kick-sessions'], [['bag'], '/tracking/hospital-bag'],
        [['pregnancy'], '/pregnancy'], [['contacts'], '/me/emergency-contacts'], [['prefs'], '/notifications/preferences']]
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

/** Keeps the phone's scheduled reminders in step with medicines, appointments and notification settings. */
function DeviceReminders() {
  const meds = useMedications()
  const appts = useAppointments()
  const prefs = useNotificationPrefs()
  const { open } = usePages()
  useEffect(() => onReminderTap(open), [open])
  useEffect(() => {
    if (meds.data && appts.data) syncReminders(meds.data, appts.data, prefs.data).catch(e => console.warn('reminders', e))
  }, [meds.data, appts.data, prefs.data])
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
