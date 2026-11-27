import { useBag, useContent, useDashboard } from '../api/hooks'
import { Badge, SecHead } from '../ui/bits'
import { Icon, type IconName } from '../ui/Icon'
import { useApp, usePages, useSheet, useT } from '../ui/shell'
import { FoodSheet } from '../pages/Food'

export function CareTab() {
  const { lang } = useApp()
  const t = useT()
  const { open } = usePages()
  const sheet = useSheet()
  const dash = useDashboard()
  const bag = useBag()
  const content = useContent(lang)
  const d = dash.data
  const taken = d?.todaysMedicines.filter(m => m.status === 'taken').length ?? 0
  const diet = d?.pregnancy?.diet
  const spotlight = (content.data?.foods ?? []).filter(f => ['Drumstick leaves', 'Ragi (finger millet)', 'Ripe papaya', 'Unripe / raw papaya'].includes(f.name))

  const tiles: [string, IconName, string, string, string][] = [
    ['c-sage', 'food', 'Food & nutrition', 'What to eat, limit, avoid', 'food'],
    ['c-amber', 'pill', 'Medicines', d ? `${taken} of ${d.todaysMedicines.length} taken today` : 'Prescribed reminders', 'meds'],
    ['c-lav', 'cal', 'Checkups', d?.nextAppointment ? `Next: ${d.nextAppointment.title}` : 'Appointments & scans', 'appts'],
    ['c-blush', 'heart', 'Health tracker', 'Weight, BP, mood', 'health'],
    ['c-sky', 'walk', 'Exercise', 'Safe activity & breathing', 'exercise'],
    ['c-peach', 'bag', 'Hospital bag', bag.data ? `${bag.data.percentPacked}% packed` : 'Delivery checklist', 'bag'],
  ]

  return (
    <>
      <header className="top">
        <div><small>Everything for you</small><h1>Care</h1></div>
        <button className="icon-btn" onClick={() => open('health')} aria-label="Log health"><Icon name="plus" size={20} /></button>
      </header>
      <div className="tiles">
        {tiles.map(([c, i, b, s, p]) => (
          <button key={p} className={`tile tap ${c}`} onClick={() => open(p)}><span className="ti"><Icon name={i} /></span><div><b>{b}</b><small>{s}</small></div></button>
        ))}
      </div>
      <SecHead title={t('meals')} right={<span className="badge b-rec">{diet === 'eggetarian' ? 'Eggetarian' : diet === 'nonVegetarian' ? 'Non-veg' : 'Vegetarian'}</span>} />
      <div className="card">
        {(d?.meals ?? []).map(m => (
          <div className="rowi" key={m.slot}>
            <div className="ico" style={{ background: 'var(--bg)' }}>{m.emoji}</div>
            <div className="grow"><small style={{ margin: 0, color: 'var(--primary)', fontWeight: 700, textTransform: 'uppercase', fontSize: 11, letterSpacing: '.5px' }}>{m.slot}</small><b style={{ marginTop: 2 }}>{m.description}</b></div>
          </div>
        ))}
      </div>
      <SecHead title="Food spotlight" action={t('seeAll')} onAction={() => open('food')} />
      <div className="hscroll">
        {spotlight.map(f => (
          <button key={f.id} className="meal tap" style={{ textAlign: 'left' }} onClick={() => sheet.open(<FoodSheet food={f} />)}>
            <div className="e">{f.emoji}</div><p style={{ marginTop: 8 }}>{f.name}</p>
            <span style={{ display: 'inline-block', marginTop: 8 }}><Badge safety={f.safety} /></span>
          </button>
        ))}
      </div>
    </>
  )
}
