import { useState } from 'react'
import { Segment } from '../ui/bits'
import { AppointmentsBody } from '../pages/Appointments'
import { ReportsBody } from '../pages/Reports'

export function CheckupsTab() {
  const [tab, setTab] = useState(0)
  return (
    <>
      <header className="top"><div><small>Hospital</small><h1>Checkups</h1></div></header>
      <div style={{ marginTop: -14 }}><Segment items={['Appointments', 'Reports']} value={tab} onChange={setTab} /></div>
      {tab === 0 ? <AppointmentsBody /> : <ReportsBody />}
    </>
  )
}
