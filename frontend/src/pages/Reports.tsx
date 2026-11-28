import { useEffect, useRef, useState } from 'react'
import { api, del, get } from '../api/client'
import { useAction, useDocuments } from '../api/hooks'
import type { DocumentCategory, DocumentExplanation, MedicalDocument } from '../api/types'
import { Empty, Info } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { useSheet, useToast } from '../ui/shell'
import { bytes, fmt, iso, today } from '../lib/util'

const CATS: [DocumentCategory | 'all', string, string][] = [['all', 'All', ''], ['ultrasound', 'Ultrasound', '🩻'], ['blood', 'Blood', '🩸'], ['urine', 'Urine', '🧪'], ['prescription', 'Prescription', '📝'], ['vaccination', 'Vaccination', '💉'], ['discharge', 'Discharge', '🏥'], ['other', 'Other', '📄']]
const emojiOf = (c: DocumentCategory) => CATS.find(x => x[0] === c)?.[2] ?? '📄'
const labelOf = (c: DocumentCategory) => CATS.find(x => x[0] === c)?.[1] ?? 'Other'

export function ReportsBody() {
  const docs = useDocuments()
  const sheet = useSheet()
  const toast = useToast()
  const [cat, setCat] = useState<DocumentCategory | 'all'>('all')
  const list = (docs.data ?? []).filter(d => cat === 'all' || d.category === cat)

  const download = async (d: MedicalDocument) => {
    try {
      const blob = await api<Blob>(`/documents/${d.id}/download`)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a'); a.href = url; a.download = d.fileName; a.click()
      setTimeout(() => URL.revokeObjectURL(url), 5000)
    } catch (e) { toast((e as Error).message) }
  }

  return (
    <>
      <div className="card" style={{ background: 'linear-gradient(135deg,var(--sky),#F3F9FE)', display: 'flex', gap: 14, alignItems: 'center' }}>
        <div className="ico" style={{ background: '#fff', color: 'var(--sky-ink)' }}><Icon name="lock" /></div>
        <div className="grow"><b style={{ fontSize: 15 }}>{docs.data?.length ?? 0} documents · private</b><small className="muted" style={{ display: 'block', marginTop: 2 }}>Only you decide who can view them</small></div>
      </div>
      <div className="chips" style={{ marginTop: 14 }}>{CATS.slice(0, 6).map(([k, l]) => <button key={k} className={`chip ${cat === k ? 'on' : ''}`} onClick={() => setCat(k)}>{l}</button>)}</div>
      <div style={{ marginTop: 12 }}>
        {list.map(d => (
          <div className="food" key={d.id}>
            <button className="ico" onClick={() => download(d)} aria-label={`Download ${d.title}`}>{emojiOf(d.category)}</button>
            <button className="grow" style={{ textAlign: 'left' }} onClick={() => sheet.open(<DocSheet doc={d} onDownload={() => download(d)} />)}>
              <b style={{ fontSize: 14.5 }}>{d.title}</b>
              <small className="muted" style={{ display: 'block', fontSize: 12 }}>{labelOf(d.category)} · {fmt(d.documentDate)} · {bytes(d.sizeBytes)}</small>
            </button>
            <button className="btn sm ghost" onClick={() => sheet.open(<ExplainSheet doc={d} />)}><Icon name="spark" size={14} /> Explain</button>
          </div>
        ))}
        {!docs.isLoading && !list.length && <Empty emoji="🗂️" text="No documents yet. Upload scans, blood reports and prescriptions to keep them in one place." />}
      </div>
      <button className="btn ghost block" style={{ marginTop: 6 }} onClick={() => sheet.open(<UploadSheet />)}><Icon name="upload" size={18} /> Upload a report</button>
      <p className="small muted" style={{ textAlign: 'center', marginTop: 10 }}>Timeline sorted by newest first</p>
    </>
  )
}

function UploadSheet() {
  const sheet = useSheet()
  const toast = useToast()
  const input = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [f, setF] = useState({ title: '', category: 'ultrasound' as DocumentCategory, date: iso(today()), notes: '' })
  const up = useAction((fd: FormData) => api('/documents', { method: 'POST', body: fd }), [['docs']], () => { sheet.close(); toast('📁 Report saved securely') })
  const submit = () => {
    if (!file) return toast('Choose a file to upload')
    if (file.size > 15 * 1024 * 1024) return toast('Files must be under 15 MB')
    const fd = new FormData()
    fd.append('file', file); fd.append('title', f.title.trim() || file.name); fd.append('category', f.category); fd.append('documentDate', f.date)
    if (f.notes) fd.append('notes', f.notes)
    up.mutate(fd)
  }
  return (
    <>
      <h3>Upload a report</h3>
      <button className="file-drop" style={{ width: '100%', marginTop: 14 }} onClick={() => input.current?.click()}>
        <Icon name="upload" size={26} /><b>{file ? file.name : 'Choose a PDF or photo'}</b><span className="small muted">{file ? bytes(file.size) : 'PDF, JPG, PNG · up to 15 MB'}</span>
      </button>
      <input ref={input} type="file" accept="application/pdf,image/jpeg,image/png,image/webp,image/heic" hidden onChange={e => { const x = e.target.files?.[0] ?? null; setFile(x); if (x && !f.title) setF({ ...f, title: x.name.replace(/\.[^.]+$/, '') }) }} />
      <label className="label" htmlFor="dT">Title</label><input className="field" id="dT" placeholder="e.g. Anomaly scan" value={f.title} onChange={e => setF({ ...f, title: e.target.value })} />
      <div className="row2">
        <div><label className="label" htmlFor="dC">Type</label>
          <select className="field" id="dC" value={f.category} onChange={e => setF({ ...f, category: e.target.value as DocumentCategory })}>{CATS.slice(1).map(([k, l]) => <option key={k} value={k}>{l}</option>)}</select></div>
        <div><label className="label" htmlFor="dD">Report date</label><input className="field" type="date" id="dD" max={iso(today())} value={f.date} onChange={e => setF({ ...f, date: e.target.value })} /></div>
      </div>
      <button className="btn block" style={{ marginTop: 20 }} disabled={up.isPending} onClick={submit}>{up.isPending ? 'Uploading…' : 'Save to vault'}</button>
    </>
  )
}

function DocSheet({ doc, onDownload }: { doc: MedicalDocument; onDownload: () => void }) {
  const sheet = useSheet()
  const toast = useToast()
  const rm = useAction(() => del(`/documents/${doc.id}`), [['docs']], () => { sheet.close(); toast('Document deleted') })
  return (
    <>
      <div style={{ textAlign: 'center' }}><div style={{ fontSize: 52 }}>{emojiOf(doc.category)}</div><h3 style={{ marginTop: 6 }}>{doc.title}</h3>
        <p className="small muted">{labelOf(doc.category)} · {fmt(doc.documentDate)} · {doc.fileName}</p></div>
      <button className="btn block" style={{ marginTop: 18 }} onClick={onDownload}><Icon name="upload" size={18} /> Download</button>
      <button className="btn ghost block" style={{ marginTop: 10 }} onClick={() => rm.mutate()}>Delete document</button>
    </>
  )
}

function ExplainSheet({ doc }: { doc: MedicalDocument }) {
  const [x, setX] = useState<DocumentExplanation | null>(null)
  useEffect(() => { get<DocumentExplanation>(`/documents/${doc.id}/explain`).then(setX).catch(() => {}) }, [doc.id])
  return (
    <>
      <span className="tag c-lav"><Icon name="spark" size={12} /> Plain-language guide</span>
      <h3 style={{ marginTop: 10 }}>{doc.title}</h3>
      {!x ? <p className="small muted" style={{ marginTop: 12 }}>Loading…</p> : <>
        <div className="card" style={{ marginTop: 14 }}><small className="muted" style={{ fontWeight: 700 }}>IN SIMPLE WORDS</small><p className="small" style={{ marginTop: 6, lineHeight: 1.6 }}>{x.summary}</p></div>
        {x.terms.length > 0 && <div className="card"><small className="muted" style={{ fontWeight: 700 }}>COMMON TERMS</small>
          {x.terms.map(t => <p key={t.key} className="small" style={{ marginTop: 8, lineHeight: 1.5 }}><b>{t.key}</b> — {t.value}</p>)}</div>}
        <div className="card"><small className="muted" style={{ fontWeight: 700 }}>POINTS TO DISCUSS WITH YOUR DOCTOR</small>
          <ul className="bullets" style={{ marginTop: 4 }}>{x.questionsForDoctor.map(q => <li key={q}>{q}</li>)}</ul></div>
        <Info warn>{x.disclaimer}</Info>
      </>}
    </>
  )
}
