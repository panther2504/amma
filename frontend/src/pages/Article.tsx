import { useContent } from '../api/hooks'
import { ReviewNote } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useApp, usePages } from '../ui/shell'

export function ArticlePage({ slug }: { slug?: string }) {
  const { lang } = useApp()
  const { open } = usePages()
  const a = useContent(lang).data?.articles.find(x => x.slug === slug)
  return (
    <PageFrame title="">
      {!a ? <p className="muted">Article not found.</p> : <>
        <div className={`c-${a.colorKey}`} style={{ borderRadius: 26, height: 170, display: 'grid', placeItems: 'center', fontSize: 80 }}>{a.emoji}</div>
        <div>
          <small style={{ color: 'var(--primary)', fontWeight: 700, display: 'block', marginTop: 18 }}>{a.category} · {a.readMinutes} min read</small>
          <h2 style={{ fontFamily: 'var(--display)', fontSize: 26, fontWeight: 600, lineHeight: 1.2, marginTop: 6 }}>{a.title}</h2>
        </div>
        {a.paragraphs.map(p => <p key={p} style={{ fontSize: 15, lineHeight: 1.75, color: 'var(--ink2)', marginTop: 14 }}>{p}</p>)}
        {a.reviewed ? <ReviewNote>Medically reviewed.</ReviewNote> : <ReviewNote />}
        <button className="btn ghost block" style={{ marginTop: 14 }} onClick={() => open('chat')}><Icon name="spark" size={18} /> Ask Amma about this</button>
      </>}
    </PageFrame>
  )
}
