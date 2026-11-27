import { useState } from 'react'
import { useContent } from '../api/hooks'
import { SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { useApp, usePages } from '../ui/shell'

const CATS = ['All', 'Pregnancy basics', 'Nutrition', 'Medical checkups', 'Baby development', 'Mental wellness', 'Labour & delivery']

export function LearnTab() {
  const { lang } = useApp()
  const { open } = usePages()
  const content = useContent(lang)
  const [cat, setCat] = useState('All')
  const [flipped, setFlipped] = useState<Record<string, boolean>>({})
  const [acc, setAcc] = useState<string | null>(null)
  const articles = (content.data?.articles ?? []).filter(a => cat === 'All' || a.category === cat)
  const myths = (content.data?.snippets ?? []).filter(s => s.kind === 'Myth')
  const faqs = (content.data?.snippets ?? []).filter(s => s.kind === 'Faq')

  return (
    <>
      <header className="top">
        <div><small>Learning centre</small><h1>Learn</h1></div>
        <button className="icon-btn" onClick={() => open('chat')} aria-label="Ask Amma"><Icon name="chat" size={20} /></button>
      </header>
      <button className="ai-hero tap" style={{ display: 'block', width: '100%', textAlign: 'left' }} onClick={() => open('chat')}>
        <span className="ai-orb" />
        <span className="chip-glass"><Icon name="spark" size={12} /> AI ASSISTANT</span>
        <h3>Ask Amma anything</h3>
        <p>Food, symptoms, scan terms, questions for your doctor — in English or Tamil.</p>
        <div className="ai-ask"><Icon name="chat" size={18} /> Can I eat papaya during pregnancy?</div>
      </button>

      <SecHead title="Articles for you" />
      <div className="chips">{CATS.map(c => <button key={c} className={`chip ${c === cat ? 'on' : ''}`} onClick={() => setCat(c)}>{c}</button>)}</div>
      <div style={{ marginTop: 12 }}>
        {!content.data ? <Skeleton h={90} /> : articles.map(a => (
          <button key={a.id} className="article tap" style={{ width: '100%', textAlign: 'left' }} onClick={() => open('article', a.slug)}>
            <div className={`thumb c-${a.colorKey}`}>{a.emoji}</div>
            <div className="grow"><small style={{ color: 'var(--primary)', fontWeight: 700 }}>{a.category}</small><b style={{ margin: '3px 0' }}>{a.title}</b><small>{a.readMinutes} min read · {a.reviewed ? '✔ Medically reviewed' : 'Review pending'}</small></div>
          </button>
        ))}
      </div>

      <SecHead title="Myth or fact?" right={<span className="small muted">Tap to flip</span>} />
      <div className="hscroll">
        {myths.map(m => (
          <div key={m.key} className={`flip ${flipped[m.key] ? 'on' : ''}`} onClick={() => setFlipped(f => ({ ...f, [m.key]: !f[m.key] }))} role="button" tabIndex={0} aria-label={`Myth: ${m.title}`}>
            <div className="flip-in">
              <div className="face front"><small style={{ color: '#B05A34' }}>❌ Myth</small><p>{m.title}</p><span className="small muted">Tap to reveal →</span></div>
              <div className="face back"><small style={{ color: 'var(--sage-ink)' }}>✅ Fact</small><p style={{ fontSize: 13 }}>{m.body}</p></div>
            </div>
          </div>
        ))}
      </div>

      <SecHead title="Frequently asked" />
      {faqs.map(f => (
        <div key={f.key} className={`acc ${acc === f.key ? 'on' : ''}`}>
          <button className="acc-h" aria-expanded={acc === f.key} onClick={() => setAcc(acc === f.key ? null : f.key)}>{f.title}<Icon name="chev" size={18} /></button>
          <div className="acc-b"><div><p>{f.body}</p></div></div>
        </div>
      ))}
    </>
  )
}
