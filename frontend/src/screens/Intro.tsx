import { useState } from 'react'
import { Logo } from '../ui/Icon'

export function Splash() {
  return (
    <section id="splash" className="layer">
      <span className="bubble-bg" style={{ width: 260, height: 260, top: -80, left: -90 }} />
      <span className="bubble-bg" style={{ width: 200, height: 200, bottom: -60, right: -60, animationDelay: '-3s' }} />
      <div className="splash-logo"><span className="pulse" /><span className="pulse" /><div className="logo-core"><Logo size={64} /></div></div>
      <div className="splash-title">Amma</div>
      <div className="splash-tag">Caring for two, every single day</div>
      <div className="splash-ta">அம்மா · உங்கள் கர்ப்பகால துணை</div>
      <div className="loader" aria-label="Loading"><i /><i /><i /></div>
    </section>
  )
}

const SLIDES = [
  { bg: 'linear-gradient(135deg,#FCE7EE,#F8D0DE)', main: '🤰', a: '🍋', b: '📅', title: <>Your pregnancy,<br />week by week</>, text: 'See how your baby grows, what changes in your body, and what to expect next.' },
  { bg: 'linear-gradient(135deg,#E2F2EA,#C8E8D7)', main: '🥗', a: '🌿', b: '🥥', title: <>Eat well,<br />the Indian way</>, text: 'South and North Indian meal ideas, plus clear guidance on what to eat, limit and avoid.' },
  { bg: 'linear-gradient(135deg,#EEE8FC,#D9CDF7)', main: '💊', a: '🔔', b: '👨‍👩‍👧', title: <>Never miss a<br />medicine or checkup</>, text: 'Reminders for prescribed medicines and appointments — and your family can stay in the loop.' },
]

export function Onboarding({ onDone }: { onDone: () => void }) {
  const [i, setI] = useState(0)
  const next = () => (i >= SLIDES.length - 1 ? onDone() : setI(i + 1))
  return (
    <section id="onboard" className="layer">
      <button className="ob-skip" onClick={onDone}>Skip</button>
      <div className="ob-track" style={{ transform: `translateX(-${i * 100}%)` }}>
        {SLIDES.map((s, k) => (
          <div className="ob-slide" key={k} aria-hidden={k !== i}>
            <div className="ob-art">
              <div className="ob-blob" style={{ background: s.bg }} /><div className="ob-main">{s.main}</div>
              <div className="ob-mini" style={{ top: 18, right: 6 }}>{s.a}</div>
              <div className="ob-mini" style={{ bottom: 26, left: 0, animationDelay: '-2s' }}>{s.b}</div>
            </div>
            <h2>{s.title}</h2><p>{s.text}</p>
          </div>
        ))}
      </div>
      <div className="ob-foot">
        <div className="dots">{SLIDES.map((_, k) => <i key={k} className={k === i ? 'on' : ''} />)}</div>
        <button className="btn" onClick={next}>{i === SLIDES.length - 1 ? 'Get started' : 'Next'}</button>
      </div>
    </section>
  )
}
