import { LocalNotifications, type LocalNotificationSchema } from '@capacitor/local-notifications'
import type { Appointment, Day, Reminder, ReminderKind } from '../api/types'
import { isNative } from './localdb'
import { addDays, iso, t12, today } from './util'

/*
 * Reminders are scheduled ON EACH PHONE (both of you share one account, so both phones schedule the same list).
 * They ring with sound even offline. Each one has "Done" and "Snooze" buttons, and if nobody marks it done a
 * follow-up rings 15 minutes later on both phones. Seven days are scheduled ahead and refreshed whenever the app opens.
 */
export const CHANNEL = 'amma-reminders-v1'
const ACTIONS = 'AMMA_REMINDER'
const BASE = 100_000
const APPT_BASE = 700_000
const DAYS_AHEAD = 7
export const FOLLOW_UP_MIN = 15

export const KIND_ICON: Record<ReminderKind, string> = { water: '💧', meal: '🍽️', snack: '🍎', juice: '🥤', tablet: '💊', other: '⏰' }
export const KIND_LABEL: Record<ReminderKind, string> = { water: 'Water', meal: 'Meal', snack: 'Snack', juice: 'Juice', tablet: 'Tablet', other: 'Other' }

const hash = (s: string) => { let h = 0; for (const c of s) h = (h * 31 + c.charCodeAt(0)) | 0; return Math.abs(h) % 25_000 }
/** Stable notification id for a reminder time on a given day (0 = today) and whether it is the follow-up. */
export const notifId = (reminderId: string, time: string, dayIndex: number, followUp: boolean) =>
  BASE + hash(reminderId + time.slice(0, 5)) * 20 + dayIndex * 2 + (followUp ? 1 : 0)

let prepared = false
export async function prepareNotifications() {
  if (!isNative() || prepared) return
  prepared = true
  await LocalNotifications.createChannel({
    id: CHANNEL, name: 'Reminders', description: 'Water, food, juice and tablet reminders',
    importance: 5, visibility: 1, sound: 'amma_chime.wav', vibration: true, lights: true, lightColor: '#C2497A',
  })
  await LocalNotifications.registerActionTypes({
    types: [{ id: ACTIONS, actions: [{ id: 'done', title: '✓ Done' }, { id: 'snooze', title: 'Snooze 10 min' }] }],
  })
}

export async function notificationStatus() {
  if (!isNative()) return { permission: true, exact: true }
  const p = await LocalNotifications.checkPermissions()
  let exact = true
  try { exact = (await LocalNotifications.checkExactNotificationSetting()).exact_alarm === 'granted' } catch { /* Android < 12 */ }
  return { permission: p.display === 'granted', exact }
}

export async function requestNotificationPermission() {
  if (!isNative()) return true
  return (await LocalNotifications.requestPermissions()).display === 'granted'
}

export const openExactAlarmSettings = () => LocalNotifications.changeExactNotificationSetting()

function titleFor(r: { kind: ReminderKind; title: string }) {
  switch (r.kind) {
    case 'water': return '💧 Time to drink water'
    case 'meal': return `🍽️ ${r.title} time`
    case 'snack': return `🍎 ${r.title}`
    case 'juice': return `🥤 ${r.title}`
    case 'tablet': return `💊 Tablet: ${r.title}`
    default: return `⏰ ${r.title}`
  }
}

const at = (date: Date, time: string) => { const [h, m] = time.split(':').map(Number); const d = new Date(date); d.setHours(h, m, 0, 0); return d }

/** Rebuilds all scheduled reminders on this phone. `todayDay` tells which of today's are already done. */
export async function scheduleAll(reminders: Reminder[], todayDay: Day | undefined, appts: Appointment[]) {
  if (!isNative()) return
  if (!(await requestNotificationPermission())) return
  await prepareNotifications()

  const pending = await LocalNotifications.getPending()
  const ours = pending.notifications.filter(n => n.id >= BASE)
  if (ours.length) await LocalNotifications.cancel({ notifications: ours.map(n => ({ id: n.id })) })

  const now = Date.now()
  const start = today()
  const doneToday = new Set((todayDay?.items ?? []).filter(i => i.status).map(i => `${i.reminderId}|${i.time.slice(0, 5)}`))
  const list: LocalNotificationSchema[] = []

  for (let d = 0; d < DAYS_AHEAD; d++) {
    const date = addDays(start, d)
    for (const r of reminders.filter(x => x.isActive)) {
      for (const time of r.times.map(t => t.slice(0, 5))) {
        if (d === 0 && doneToday.has(`${r.id}|${time}`)) continue
        const when = at(date, time)
        const extra = { type: 'reminder', reminderId: r.id, time, date: iso(date), day: d }
        const body = `${r.note ? r.note + ' · ' : ''}${t12(time)} — tap Done when finished. You both get this reminder.`
        if (when.getTime() > now) list.push({
          id: notifId(r.id, time, d, false), title: titleFor(r), body, channelId: CHANNEL, actionTypeId: ACTIONS,
          schedule: { at: when, allowWhileIdle: true }, extra, autoCancel: true, smallIcon: 'ic_stat_amma',
        })
        const follow = new Date(when.getTime() + FOLLOW_UP_MIN * 60_000)
        if (follow.getTime() > now) list.push({
          id: notifId(r.id, time, d, true), title: `⏰ Still pending: ${r.kind === 'water' ? 'water' : r.title} (${t12(time)})`,
          body: 'Nobody has marked this done yet. If it is done, tap Done — otherwise please remind her.',
          channelId: CHANNEL, actionTypeId: ACTIONS, schedule: { at: follow, allowWhileIdle: true }, extra: { ...extra, followUp: true }, autoCancel: true, smallIcon: 'ic_stat_amma',
        })
      }
    }
  }

  // Appointments: the day before (as chosen) and 2 hours before.
  for (const a of appts.filter(x => x.status === 'scheduled')) {
    const t = new Date(`${a.date}T${a.time.slice(0, 5)}:00`).getTime()
    for (const [i, mins] of [a.reminderMinutesBefore, 120].entries()) {
      const when = t - mins * 60_000
      if (when > now && when < now + 30 * 86_400_000) list.push({
        id: APPT_BASE + hash(a.id) * 2 + i, title: `🏥 ${a.title} · ${i === 1 ? 'in 2 hours' : new Date(t).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })}`,
        body: `${t12(a.time)}${a.doctorName ? ` · ${a.doctorName}` : ''}${a.location ? ` · ${a.location}` : ''}`,
        channelId: CHANNEL, schedule: { at: new Date(when), allowWhileIdle: true }, extra: { type: 'appointment' }, smallIcon: 'ic_stat_amma',
      })
    }
  }

  // If the app isn't opened for a week, remind to open it so reminders keep coming.
  list.push({
    id: BASE - 1, title: 'Open Amma to keep reminders on time', body: 'Reminders are planned a week ahead. Opening the app plans the next week.',
    channelId: CHANNEL, schedule: { at: at(addDays(start, DAYS_AHEAD - 1), '10:00'), allowWhileIdle: true }, smallIcon: 'ic_stat_amma',
  })

  for (let i = 0; i < list.length; i += 100) await LocalNotifications.schedule({ notifications: list.slice(i, i + 100) })
}

/** After an occurrence is done (on this phone or the other), stop its notifications here. */
export async function clearOccurrence(reminderId: string, time: string) {
  if (!isNative()) return
  const ids = [notifId(reminderId, time, 0, false), notifId(reminderId, time, 0, true)]
  await LocalNotifications.cancel({ notifications: ids.map(id => ({ id })) }).catch(() => {})
  await LocalNotifications.removeDeliveredNotificationsById({ ids }).catch(() => {})
}

export async function snooze(n: LocalNotificationSchema) {
  await LocalNotifications.schedule({ notifications: [{ ...n, id: 1_000_000 + (n.id % 900_000), schedule: { at: new Date(Date.now() + 10 * 60_000), allowWhileIdle: true } }] })
}

/** Plays a sample reminder in 5 seconds so you can check the sound and volume. */
export async function testReminder() {
  if (!isNative()) return false
  if (!(await requestNotificationPermission())) return false
  await prepareNotifications()
  await LocalNotifications.schedule({ notifications: [{
    id: BASE - 2, title: '💧 Test reminder', body: 'This is how Amma reminders sound. Both phones get them.',
    channelId: CHANNEL, schedule: { at: new Date(Date.now() + 5000), allowWhileIdle: true }, smallIcon: 'ic_stat_amma',
  }] })
  return true
}
