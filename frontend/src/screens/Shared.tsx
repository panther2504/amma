import { useSharedWithMe } from '../api/hooks'
import { SecHead, Skeleton } from '../ui/bits'
import { Icon } from '../ui/Icon'
import { useApp, usePages } from '../ui/shell'
import { fmt, ordinal, t12, daysUntil, relDays } from '../lib/util'

/** What a partner or family member sees: only what the mother has chosen to share. */
export function SharedHome() {
  const { user } = useApp()
  const { open } = usePages()
  const q = useSharedWithMe()
  return (
    <>
      <header className="top">
        <div className="hello"><div className="avatar">{user.name[0]?.toUpperCase() ?? 'A'}</div><div><small>Family companion</small><h1>{user.name || 'Welcome'}</h1></div></div>
        <div className="top-actions"><button className="icon-btn sos" onClick={() => open('emergency')} aria-label="Emergency help">SOS</button></div>
      </header>
      {q.isLoading && <Skeleton h={220} mt={0} />}
      {q.data && !q.data.length && (
        <div className="share-banner"><b>No journeys shared with you yet</b><p className="small" style={{ marginTop: 6, lineHeight: 1.5, color: 'var(--ink2)' }}>Ask the mother to invite you from Profile → Family sharing using this mobile number.</p></div>
      )}
      {q.data?.map(j => (
        <div key={j.familyMemberId}>
          <section className="hero">
            <span className="blob b1" /><span className="blob b2" />
            <div className="hero-top"><div className="hero-info">
              <span className="chip-glass">{j.role === 'partner' ? 'Partner' : 'Family'} view</span>
              <h2>{j.motherName}'s journey</h2>
              {j.gestation ? <p>{j.gestation.weeks} weeks, {j.gestation.days} days · {ordinal(j.gestation.trimester)} trimester{j.week ? ` · baby is the size of a ${j.week.sizeName}` : ''}</p> : <p>Baby updates aren't shared.</p>}
            </div>{j.week && <div className="fruit-float">{j.week.emoji}</div>}</div>
            {j.edd && <div className="hero-stats" style={{ gridTemplateColumns: '1fr 1fr' }}><div><small>Due date</small><b>{fmt(j.edd, { day: 'numeric', month: 'short' })}</b></div><div><small>Days to go</small><b>{j.gestation?.daysToGo}</b></div></div>}
          </section>
          {j.upcomingAppointments && <>
            <SecHead title="Upcoming appointments" />
            <div className="card">{j.upcomingAppointments.length ? j.upcomingAppointments.map(a => (
              <div className="rowi" key={a.id}><div className="ico c-lav"><Icon name="cal" size={19} /></div><div className="grow"><b>{a.title}</b><small>{fmt(a.date, { weekday: 'short', day: 'numeric', month: 'short' })} · {t12(a.time)} · {relDays(daysUntil(a.date))}</small></div></div>
            )) : <p className="small muted">No upcoming appointments.</p>}</div>
          </>}
          {j.todaysMedicines && <>
            <SecHead title="Today's medicines" />
            <div className="card">{j.todaysMedicines.length ? j.todaysMedicines.map(m => (
              <div className="rowi" key={m.medicationId + m.time}><div className={`ico c-${m.colorKey}`}><Icon name="pill" size={19} /></div><div className="grow"><b>{m.name}</b><small>{t12(m.time)}</small></div>
                <span className={`tag ${m.status === 'taken' ? 'c-sage' : 'c-amber'}`}>{m.status === 'taken' ? 'Taken' : m.status === 'skipped' ? 'Skipped' : 'Pending'}</span></div>
            )) : <p className="small muted">No medicines today.</p>}</div>
          </>}
          {j.hospitalBag && <>
            <SecHead title="Hospital bag" right={<span className="small muted">{j.hospitalBag.percentPacked}% packed</span>} />
            <div className="card"><div className="progress"><i style={{ width: `${j.hospitalBag.percentPacked}%` }} /></div>
              <p className="small muted" style={{ marginTop: 10 }}>Still to pack: {j.hospitalBag.items.filter(i => !i.packed).slice(0, 5).map(i => i.title).join(', ') || 'nothing — all packed! 🎉'}</p></div>
          </>}
        </div>
      ))}
      <button style={{ marginTop: 14, width: '100%', textAlign: 'left' }} className="alert-card tap" onClick={() => open('emergency')}>
        <div className="ico"><Icon name="alert" /></div><div className="grow"><b>Know the warning signs</b><small style={{ display: 'block', marginTop: 2 }}>Help her get care quickly</small></div><Icon name="chev" size={20} />
      </button>
    </>
  )
}
