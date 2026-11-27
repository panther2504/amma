import { put } from '../api/client'
import { useAction, useDashboard } from '../api/hooks'
import { SecHead } from '../ui/bits'
import { Icon, type IconName } from '../ui/Icon'
import { useApp, usePages, useSheet, useToast } from '../ui/shell'
import { fmt, ordinal } from '../lib/util'
import type { User } from '../api/types'

export function MenuItem({ c, i, label, onClick }: { c: string; i: IconName; label: string; onClick: () => void }) {
  return <button className="mi" onClick={onClick}><span className={`ico ${c}`}><Icon name={i} size={19} /></span><b>{label}</b><span className="chev"><Icon name="chev" size={18} /></span></button>
}

export function ProfileTab() {
  const { user, setUser, signOut, lang } = useApp()
  const { open } = usePages()
  const sheet = useSheet()
  const toast = useToast()
  const dash = useDashboard()
  const p = dash.data?.pregnancy
  const g = p?.gestation
  const lng = useAction((l: 'en' | 'ta') => put<User>('/me', { language: l }), [], u => setUser(u))

  return (
    <>
      <header className="top">
        <div><small>Account</small><h1>Profile</h1></div>
        <button className="icon-btn" onClick={() => open('settings')} aria-label="Settings"><Icon name="sliders" size={20} /></button>
      </header>
      <div className="prof">
        <div className="avatar">{user.name[0]?.toUpperCase()}</div>
        <h2>{user.name}</h2>
        {p && g && <>
          <p className="small muted" style={{ marginTop: 4 }}>Due {fmt(p.edd)} · {p.pregnancyType === 'single' ? 'Singleton' : p.pregnancyType === 'twins' ? 'Twin' : 'Multiple'} pregnancy</p>
          <div className="pstats">
            <div><b>{g.weeks}w {g.days}d</b><small>Gestation</small></div>
            <div><b>{ordinal(g.trimester)}</b><small>Trimester</small></div>
            <div><b>{g.daysToGo}</b><small>Days to go</small></div>
          </div>
        </>}
      </div>
      <SecHead title="My pregnancy" />
      <div className="menu">
        <MenuItem c="c-blush" i="heart" label="Pregnancy profile" onClick={() => open('pregnancy')} />
        <MenuItem c="c-sky" i="file" label="Medical report vault" onClick={() => open('reports')} />
        <MenuItem c="c-sage" i="users" label="Family sharing" onClick={() => open('family')} />
        <MenuItem c="c-red" i="phone" label="Emergency contacts" onClick={() => open('contacts')} />
      </div>
      <SecHead title="Preferences" />
      <div className="menu">
        <MenuItem c="c-lav" i="globe" label={`Language · ${lang === 'en' ? 'English' : 'தமிழ்'}`} onClick={() => lng.mutate(lang === 'en' ? 'ta' : 'en')} />
        <MenuItem c="c-amber" i="bell" label="Notifications" onClick={() => open('settings')} />
        <MenuItem c="c-sage" i="shield" label="Privacy & consent" onClick={() => open('privacy')} />
        <MenuItem c="c-peach" i="logout" label="Sign out" onClick={() => sheet.open(
          <>
            <h3>Sign out?</h3>
            <p className="small muted" style={{ margin: '8px 0 18px', lineHeight: 1.5 }}>Your data stays safe in your account. Sign in again with your mobile number.</p>
            <button className="btn danger block" onClick={() => { sheet.close(); signOut() }}>Sign out</button>
            <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Cancel</button>
          </>)} />
      </div>
      <button className="plus-card tap" style={{ marginTop: 18, width: '100%', textAlign: 'left' }} onClick={() => toast('Amma Plus — coming soon ✨')}>
        <div className="ico" style={{ background: 'rgba(255,255,255,.12)', color: '#FFD27A' }}><Icon name="crown" /></div>
        <div className="grow"><b>Amma Plus</b><small>Personal meal plans, report explanations & family features. Safety information is always free.</small></div>
      </button>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 20 }}>Amma v1.0</p>
    </>
  )
}
