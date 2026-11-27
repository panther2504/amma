import { useEffect } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { post } from '../api/client'
import { useNotifications } from '../api/hooks'
import { Icon, type IconName } from '../ui/Icon'
import { fmtDateTime } from '../lib/util'

const STYLE: Record<string, [string, IconName]> = {
  Medicine: ['c-amber', 'pill'], Appointment: ['c-lav', 'cal'], Water: ['c-sky', 'drop'], Weekly: ['c-blush', 'baby'], Baby: ['c-blush', 'baby'], Family: ['c-red', 'users'], System: ['c-sage', 'info'],
}

export function NotificationsSheet() {
  const q = useNotifications()
  const qc = useQueryClient()
  useEffect(() => { post('/notifications/read-all').then(() => qc.invalidateQueries({ queryKey: ['dashboard'] })).catch(() => {}) }, [qc])
  return (
    <>
      <h3>Notifications</h3>
      <div style={{ marginTop: 12 }}>
        {(q.data ?? []).map(n => {
          const [c, i] = STYLE[n.type] ?? STYLE.System
          return <div className="rowi" key={n.id}><div className={`ico ${c}`}><Icon name={i} size={19} /></div><div className="grow"><b>{n.title}</b><small>{n.body}</small></div><span className="small muted">{fmtDateTime(n.createdAt)}</span></div>
        })}
        {q.data && !q.data.length && <p className="small muted">You're all caught up. Reminders for medicines and checkups will appear here.</p>}
      </div>
    </>
  )
}
