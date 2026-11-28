import { useEffect, useRef, useState } from 'react'
import { api, del, patch } from '../api/client'
import { useAction, useMemories } from '../api/hooks'
import type { Memory } from '../api/types'
import { Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { useSheet, useToast } from '../ui/shell'
import { blobToDataUrl, resizeImage } from '../lib/images'
import { fmt, iso, today } from '../lib/util'

export function MemoriesTab() {
  const q = useMemories()
  const sheet = useSheet()
  const toast = useToast()
  const input = useRef<HTMLInputElement>(null)

  const pick = async (file?: File) => {
    if (!file) return
    try {
      const [photo, thumb] = await Promise.all([resizeImage(file, 1600, 0.82), resizeImage(file, 360, 0.7)])
      sheet.open(<AddMemory photo={photo} thumb={await blobToDataUrl(thumb)} />)
    } catch (e) { toast((e as Error).message) }
    if (input.current) input.current.value = ''
  }

  const list = q.data ?? []
  const months = [...new Set(list.map(m => m.takenOn.slice(0, 7)))]

  return (
    <>
      <header className="top">
        <div><small>Our journey</small><h1>Memories</h1></div>
        <button className="icon-btn" onClick={() => input.current?.click()} aria-label="Add photo"><Icon name="camera" size={20} /></button>
      </header>
      <input ref={input} type="file" accept="image/*" hidden onChange={e => pick(e.target.files?.[0])} />
      <button className="btn block" onClick={() => input.current?.click()}><Icon name="camera" size={18} /> Add a photo</button>
      {q.isLoading && <Skeleton h={240} />}
      {months.map(mo => (
        <div key={mo}>
          <div className="h-sec"><h3>{fmt(`${mo}-01`, { month: 'long', year: 'numeric' })}</h3><span className="small muted">{list.filter(m => m.takenOn.startsWith(mo)).length} photos</span></div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3,1fr)', gap: 6 }}>
            {list.filter(m => m.takenOn.startsWith(mo)).map(m => (
              <button key={m.id} onClick={() => sheet.open(<ViewMemory m={m} />)} aria-label={m.caption ?? `Photo from ${m.takenOn}`}
                style={{ position: 'relative', aspectRatio: '1', borderRadius: 14, overflow: 'hidden', background: 'var(--blush)' }}>
                <img src={m.thumbnail} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover', display: 'block' }} />
                {m.pregnancyWeek && <span style={{ position: 'absolute', left: 6, bottom: 6, fontSize: 10, fontWeight: 800, color: '#fff', background: 'rgba(43,26,38,.55)', borderRadius: 8, padding: '2px 6px' }}>Wk {m.pregnancyWeek}</span>}
              </button>
            ))}
          </div>
        </div>
      ))}
      {q.data && !list.length && (
        <div className="card empty" style={{ marginTop: 14 }}><div>📸</div>
          <p className="small muted" style={{ marginTop: 6, lineHeight: 1.5 }}>Keep a photo of each week — bump pictures, scan days, little moments. You both see them.</p></div>
      )}
    </>
  )
}

function AddMemory({ photo, thumb }: { photo: Blob; thumb: string }) {
  const sheet = useSheet()
  const toast = useToast()
  const [caption, setCaption] = useState('')
  const [date, setDate] = useState(iso(today()))
  const up = useAction((fd: FormData) => api('/memories', { method: 'POST', body: fd }), [['memories']], () => { sheet.close(); toast('📸 Memory saved') })
  const save = () => {
    const fd = new FormData()
    fd.append('photo', new File([photo], 'memory.jpg', { type: 'image/jpeg' }))
    fd.append('thumbnail', thumb)
    fd.append('takenOn', date)
    if (caption.trim()) fd.append('caption', caption.trim())
    up.mutate(fd, { onError: e => toast((e as Error).message) })
  }
  return (
    <>
      <img src={thumb} alt="" style={{ width: '100%', maxHeight: 260, objectFit: 'cover', borderRadius: 20 }} />
      <label className="label" htmlFor="mC">Caption</label>
      <input className="field" id="mC" placeholder="e.g. 24 weeks — first kicks!" value={caption} onChange={e => setCaption(e.target.value)} />
      <label className="label" htmlFor="mD">Date</label>
      <input className="field" type="date" id="mD" max={iso(today())} value={date} onChange={e => setDate(e.target.value)} />
      <button className="btn block" style={{ marginTop: 18 }} disabled={up.isPending} onClick={save}>{up.isPending ? 'Saving…' : 'Save memory'}</button>
    </>
  )
}

function ViewMemory({ m }: { m: Memory }) {
  const sheet = useSheet()
  const toast = useToast()
  const [src, setSrc] = useState(m.thumbnail)
  const [caption, setCaption] = useState(m.caption ?? '')
  useEffect(() => {
    let url: string | undefined
    api<Blob>(`/memories/${m.id}/photo`).then(b => { url = URL.createObjectURL(b); setSrc(url) }).catch(() => {})
    return () => { if (url) URL.revokeObjectURL(url) }
  }, [m.id])
  const saveCaption = useAction((c: string) => patch(`/memories/${m.id}`, { caption: c }), [['memories']], () => toast('Caption saved'))
  const remove = useAction(() => del(`/memories/${m.id}`), [['memories']], () => { sheet.close(); toast('Photo deleted') })
  return (
    <>
      <img src={src} alt={m.caption ?? ''} style={{ width: '100%', borderRadius: 20, display: 'block' }} />
      <p className="small muted" style={{ marginTop: 10 }}>{fmt(m.takenOn, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric' })}{m.pregnancyWeek ? ` · Week ${m.pregnancyWeek}` : ''}{m.byName ? ` · added by ${m.byName}` : ''}</p>
      <div style={{ display: 'flex', gap: 8, marginTop: 10 }}>
        <input className="field" placeholder="Add a caption" value={caption} onChange={e => setCaption(e.target.value)} aria-label="Caption" />
        {caption !== (m.caption ?? '') && <button className="btn sm" style={{ height: 'auto' }} onClick={() => saveCaption.mutate(caption)}>Save</button>}
      </div>
      <button className="btn ghost block" style={{ marginTop: 12 }} onClick={() => sheet.open(
        <><h3>Delete this photo?</h3><p className="small muted" style={{ margin: '8px 0 18px' }}>It will be removed from both phones.</p>
          <button className="btn danger block" onClick={() => remove.mutate()}>Delete</button>
          <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Cancel</button></>)}>
        <Icon name="trash" size={16} /> Delete photo
      </button>
    </>
  )
}
