import { useState } from 'react'
import { del, patch, post } from '../api/client'
import { useAction, useFamily } from '../api/hooks'
import type { FamilyMember, FamilyRole } from '../api/types'
import { Choice, Info, Switch } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { PageFrame, useSheet, useToast } from '../ui/shell'

const PERMS: [keyof FamilyMember & `share${string}`, string][] = [
  ['shareAppointments', 'Appointments'], ['shareMedicines', 'Medicine reminders'], ['shareBabyUpdates', 'Baby updates'], ['shareHealth', 'Health records'], ['shareReports', 'Medical reports'],
]

export function FamilyPage() {
  const fam = useFamily()
  const sheet = useSheet()
  const toast = useToast()
  const update = useAction((a: { id: string; body: Partial<FamilyMember> }) => patch(`/family/${a.id}`, a.body), [['family']])
  const revoke = useAction((id: string) => del(`/family/${id}`), [['family']], () => { sheet.close(); toast('Access removed') })

  return (
    <PageFrame title="Family sharing" right={<button className="icon-btn" onClick={() => sheet.open(<InviteSheet />)} aria-label="Invite"><Icon name="plus" size={20} /></button>}>
      <Info icon="shield">You control exactly what each person can see. Health records and reports are private by default.</Info>
      {(fam.data ?? []).map((f, i) => (
        <div className="card" style={{ marginTop: 12 }} key={f.id}>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
            <div className="avatar" style={{ width: 46, height: 46, fontSize: 17, background: `linear-gradient(135deg,${i % 2 ? '#9B86E0,#6A55B8' : '#7CC0EC,#2C6C9D'})` }}>{f.name[0]}</div>
            <div className="grow"><b>{f.name}</b><small className="muted" style={{ display: 'block' }}>{f.relation || 'Family'} · {f.role === 'partner' ? 'Partner' : 'Family viewer'}</small></div>
            <span className={`tag ${f.status === 'active' ? 'c-sage' : 'c-amber'}`}>{f.status === 'active' ? 'Active' : 'Invited'}</span>
          </div>
          <div style={{ marginTop: 12 }}>
            {PERMS.map(([k, l]) => (
              <div key={k} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '9px 0', borderTop: '1px solid var(--line)' }}>
                <span className="small" style={{ fontWeight: 600 }}>{l}</span>
                <Switch on={!!f[k]} label={`${l} for ${f.name}`} onChange={v => update.mutate({ id: f.id, body: { [k]: v } })} />
              </div>
            ))}
          </div>
          {f.status === 'invited' && <p className="small muted" style={{ marginTop: 8 }}>They'll get access when they sign in to Amma with {f.phone ?? 'their number'}. Invite code: <b>{f.inviteCode}</b></p>}
          <button className="small" style={{ color: 'var(--red-ink)', fontWeight: 700, marginTop: 10 }} onClick={() => sheet.open(
            <><h3>Remove {f.name}?</h3><p className="small muted" style={{ margin: '8px 0 18px' }}>They will no longer see anything you share.</p>
              <button className="btn danger block" onClick={() => revoke.mutate(f.id)}>Remove access</button>
              <button className="btn ghost block" style={{ marginTop: 10 }} onClick={sheet.close}>Cancel</button></>)}>Remove access</button>
        </div>
      ))}
      <button className="btn ghost block" style={{ marginTop: 14 }} onClick={() => sheet.open(<InviteSheet />)}><Icon name="users" size={18} /> Invite partner or family</button>
    </PageFrame>
  )
}

function InviteSheet() {
  const sheet = useSheet()
  const toast = useToast()
  const [f, setF] = useState({ name: '', relation: '', phone: '', role: 'partner' as FamilyRole })
  const send = useAction(() => post<FamilyMember>('/family', f), [['family']], m => { sheet.close(); toast(m.status === 'active' ? `✅ ${m.name} can now follow your journey` : `📨 Invite created for ${m.name}`) })
  return (
    <>
      <h3>Invite to your journey</h3>
      <label className="label" htmlFor="iN">Name</label><input className="field" id="iN" placeholder="Name" value={f.name} onChange={e => setF({ ...f, name: e.target.value })} />
      <label className="label" htmlFor="iRel">Relation</label><input className="field" id="iRel" placeholder="e.g. Husband, Mother" value={f.relation} onChange={e => setF({ ...f, relation: e.target.value })} />
      <label className="label" htmlFor="iM">Mobile number</label><input className="field" id="iM" inputMode="tel" placeholder="+91" value={f.phone} onChange={e => setF({ ...f, phone: e.target.value })} />
      <span className="label">Access</span>
      <Choice value={f.role} onChange={role => setF({ ...f, role })} options={[['partner', 'Partner'], ['familyViewer', 'Family viewer']]} />
      <button className="btn block" style={{ marginTop: 20 }} disabled={send.isPending} onClick={() => (f.name.trim() ? send.mutate() : toast('Please enter a name'))}>Send invite</button>
    </>
  )
}
