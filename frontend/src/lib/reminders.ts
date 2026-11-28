import { LocalNotifications, type LocalNotificationSchema } from '@capacitor/local-notifications'
import type { Appointment, Medication, NotificationPrefs } from '../api/types'
import { isNative } from './localdb'
import { t12 } from './util'

// Reminders are scheduled ON THE PHONE, so they fire on time even offline and even if the free
// server is asleep. They are re-synced whenever medicines, appointments or preferences change.
const MED_BASE = 100_000
const APPT_BASE = 500_000
const hash = (s: string) => { let h = 0; for (const c of s) h = (h * 31 + c.charCodeAt(0)) | 0; return Math.abs(h) % 300_000 }

export async function ensureNotificationPermission() {
  if (!isNative()) return false
  const cur = await LocalNotifications.checkPermissions()
  if (cur.display === 'granted') return true
  return (await LocalNotifications.requestPermissions()).display === 'granted'
}

export async function syncReminders(meds: Medication[], appts: Appointment[], prefs?: NotificationPrefs) {
  if (!isNative() || !(await ensureNotificationPermission())) return
  const pending = await LocalNotifications.getPending()
  const ours = pending.notifications.filter(n => n.id >= MED_BASE)
  if (ours.length) await LocalNotifications.cancel({ notifications: ours.map(n => ({ id: n.id })) })

  const list: LocalNotificationSchema[] = []
  const today = new Date().toISOString().slice(0, 10)
  if (prefs?.medicines ?? true) {
    for (const m of meds.filter(m => m.isActive && (!m.endDate || m.endDate >= today))) {
      for (const t of m.reminderTimes) {
        const [hour, minute] = t.split(':').map(Number)
        list.push({
          id: MED_BASE + hash(m.id + t),
          title: `💊 ${m.name} · ${t12(t)}`,
          body: `${m.dosage}${m.instructions ? ` · ${m.instructions}` : ''} — as prescribed`,
          schedule: { on: { hour, minute }, allowWhileIdle: true },
          extra: { page: 'meds' },
        })
      }
    }
  }
  if (prefs?.appointments ?? true) {
    const now = Date.now()
    for (const a of appts.filter(a => a.status === 'scheduled')) {
      const at = new Date(`${a.date}T${a.time.slice(0, 5)}:00`).getTime() - a.reminderMinutesBefore * 60_000
      if (at > now) list.push({
        id: APPT_BASE + hash(a.id),
        title: `📅 ${a.title}`,
        body: `${new Date(`${a.date}T00:00:00`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })} at ${t12(a.time)}${a.doctorName ? ` · ${a.doctorName}` : ''}`,
        schedule: { at: new Date(at), allowWhileIdle: true },
        extra: { page: 'appts' },
      })
    }
  }
  if (list.length) await LocalNotifications.schedule({ notifications: list })
}

/** Opens the related page when a reminder is tapped. */
export function onReminderTap(open: (page: string) => void) {
  if (!isNative()) return () => {}
  const sub = LocalNotifications.addListener('localNotificationActionPerformed', e => {
    const page = (e.notification.extra as { page?: string } | undefined)?.page
    if (page) open(page)
  })
  return () => { sub.then(h => h.remove()) }
}
