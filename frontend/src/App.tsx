import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { hasStoredSession, logout, onSignedOut, refreshSession } from './api/client'
import type { AuthResponse, User } from './api/types'
import { translate } from './lib/i18n'
import { AppContext, PageStack, SheetProvider, ToastProvider, stagger, useRipple, type PageRenderer } from './ui/shell'
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
    const restore = hasStoredSession() ? refreshSession().catch(() => null) : Promise.resolve(null)
    Promise.all([restore, minDelay]).then(([auth]) => {
      if (auth) enter(auth.user)
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
              <PageStack pages={PAGES}><Shell familyOnly={isFamilyOnly(ctx.user)} /></PageStack>
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

function OfflineBar() {
  const [online, setOnline] = useState(navigator.onLine)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    addEventListener('online', on); addEventListener('offline', off)
    return () => { removeEventListener('online', on); removeEventListener('offline', off) }
  }, [])
  return online ? null : <div className="offline-bar" role="status">You're offline — showing saved information. Emergency numbers still work.</div>
}
