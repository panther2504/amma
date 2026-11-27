import { useMemo, useState } from 'react'
import { put } from '../api/client'
import type { Diet, Pregnancy, User } from '../api/types'
import { Choice } from '../ui/bits'
import { Logo } from '../ui/Icon'
import { useToast } from '../ui/shell'
import { addDays, fmt, gestationFromLmp, iso, lmpFrom, today } from '../lib/util'

const SIZE_EMOJI = ['🌱', '🌱', '🌱', '🌱', '🌱', '⚪', '🫘', '🫐', '🍇', '🍒', '🍓', '🍋‍🟩', '🍑', '🍋', '🍑', '🍎', '🥑', '🍊', '🍠', '🥭', '🍌', '🥕', '🌽', '🍊', '🌽', '🥦', '🥬', '🥬', '🍆', '🎃', '🥒', '🥥', '🥬', '🍍', '🍈', '🍈', '🥭', '🍈', '🎃', '🍉', '🍉']

/** Creates the pregnancy profile: name, dating (LMP or doctor's due date), diet, language and consent. */
export function Setup({ user, onDone }: { user: User; onDone: (u: User) => void }) {
  const toast = useToast()
  const [name, setName] = useState(user.name)
  const [mode, setMode] = useState<'lmp' | 'edd'>('lmp')
  const [date, setDate] = useState('')
  const [diet, setDiet] = useState<Diet>('vegetarian')
  const [lang, setLang] = useState<'en' | 'ta'>(user.language)
  const [consent, setConsent] = useState(false)
  const [busy, setBusy] = useState(false)

  const g = useMemo(() => (date ? gestationFromLmp(lmpFrom(mode, date)) : null), [date, mode])

  const finish = async () => {
    if (!name.trim()) return toast('Please enter your name')
    if (!g) return toast('Please choose a date')
    if (!g.plausible) return toast('Please check the date')
    if (!consent) return toast('Please agree to store your health data to continue')
    setBusy(true)
    try {
      await put('/me/consents', { type: 'healthData', granted: true })
      const u = await put<User>('/me', { name: name.trim(), language: lang })
      await put<Pregnancy>('/pregnancy', mode === 'lmp' ? { datingMethod: 'lmp', lmp: date, diet } : { datingMethod: 'doctorEdd', edd: date, diet })
      onDone({ ...u, hasPregnancy: true })
      toast(`Welcome to Amma, ${u.name} 💗`)
    } catch (e) { toast((e as Error).message) } finally { setBusy(false) }
  }

  return (
    <section id="setup" className="layer">
      <div className="scroll">
        <div className="setup-head">
          <div className="avatar" style={{ width: 52, height: 52 }}><Logo size={34} /></div>
          <h2>Let's set up your journey</h2><p>This helps Amma personalise your week-by-week guide.</p>
        </div>
        <label className="label" htmlFor="suName">Your name</label>
        <input className="field" id="suName" placeholder="Enter your name" value={name} onChange={e => setName(e.target.value)} />

        <span className="label">How should we date your pregnancy?</span>
        <Choice value={mode} onChange={v => { setMode(v); setDate('') }} options={[['lmp', 'Last period (LMP)'], ['edd', 'Due date from doctor']]} />

        <label className="label" htmlFor="suDate">{mode === 'lmp' ? 'First day of last period' : 'Due date given by your doctor'}</label>
        <input className="field" type="date" id="suDate" value={date} onChange={e => setDate(e.target.value)}
          max={mode === 'lmp' ? iso(today()) : iso(addDays(today(), 290))} min={mode === 'lmp' ? iso(addDays(today(), -300)) : iso(today())} />
        {g && (g.plausible ? (
          <div className="edd-preview"><span className="big">{SIZE_EMOJI[Math.min(40, Math.max(4, g.weeks))]}</span><div><b>{g.weeks} weeks, {g.rem} days</b><small>Estimated due date: {fmt(g.edd)}</small></div></div>
        ) : (
          <div className="edd-preview"><span className="big">🗓️</span><div><b>Please check the date</b><small>It should fall within a current pregnancy.</small></div></div>
        ))}

        <span className="label">Food preference</span>
        <Choice value={diet} onChange={setDiet} options={[['vegetarian', '🥦 Veg'], ['eggetarian', '🥚 Eggetarian'], ['nonVegetarian', '🍗 Non-veg']]} />

        <span className="label">Language</span>
        <Choice value={lang} onChange={setLang} options={[['en', 'English'], ['ta', <span className="ta" key="ta">தமிழ்</span>]]} />

        <label className="check-item" style={{ marginTop: 16, alignItems: 'flex-start' }}>
          <input type="checkbox" checked={consent} onChange={e => setConsent(e.target.checked)} style={{ width: 22, height: 22, accentColor: 'var(--primary)', marginTop: 2 }} />
          <span className="small" style={{ lineHeight: 1.5, color: 'var(--ink2)' }}>I agree that Amma may securely store the pregnancy and health information I enter, to personalise my guide. I can export or delete it at any time.</span>
        </label>
        <p className="small muted" style={{ marginTop: 10, lineHeight: 1.5 }}>🔒 Your health data stays private. You decide what to share with family.</p>
      </div>
      <button className="btn block" disabled={busy} onClick={finish}>{busy ? 'Saving…' : 'Start my journey'}</button>
    </section>
  )
}
