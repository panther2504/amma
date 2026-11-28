import { registerPlugin } from '@capacitor/core'
import { LocalNotifications } from '@capacitor/local-notifications'
import type { Appointment, Day, Reminder, ReminderKind } from '../api/types'
import { isNative } from './localdb'
import { addDays, iso, t12, today } from './util'

/*
 * Reminders are real ALARMS on each phone (native AmmaAlarms plugin):
 * - exact time, even when Amma is closed, the phone is locked or in battery saver,
 * - sound on the ALARM volume (rings in silent / vibrate mode), vibration, full-screen over the lock screen,
 * - Done / Snooze 10 min / Stop buttons; "Done" works without opening the app and is synced later,
 * - if nobody marks it done, a follow-up alarm 15 minutes later on both phones,
 * - restored after the phone restarts. Five days are planned ahead and refreshed whenever Amma opens.
 */
export const FOLLOW_UP_MIN = 15
const DAYS_AHEAD = 5

export const KIND_ICON: Record<ReminderKind, string> = { water: '💧', meal: '🍽️', snack: '🍎', juice: '🥤', tablet: '💊', other: '⏰' }
export const KIND_LABEL: Record<ReminderKind, string> = { water: 'Water', meal: 'Meal', snack: 'Snack', juice: 'Juice', tablet: 'Tablet', other: 'Other' }

interface AlarmItem { reminderId: string; time: string; date: string; followUp?: boolean }
interface Alarm { id: number; at: number; title: string; body: string; items: AlarmItem[]; quiet?: boolean }
export interface DoneAction { reminderId: string; time: string; date: string; status: 'done'; at: number }
export interface AlarmStatus {
  notifications: boolean; exact: boolean; fullScreen: boolean; batteryUnrestricted: boolean
  alarmVolume: number; alarmVolumeMax: number; scheduled: number; manufacturer: string
}
interface AmmaAlarmsPlugin {
  schedule(o: { alarms: Alarm[] }): Promise<{ scheduled: number }>
  takeActions(): Promise<{ actions: DoneAction[] }>
  dismiss(o: { ids: number[] }): Promise<void>
  status(): Promise<AlarmStatus>
  openSettings(o: { which: 'exact' | 'fullscreen' | 'battery' | 'notifications' | 'app' }): Promise<void>
  setAlarmVolume(o: { percent: number }): Promise<void>
  test(o: { seconds: number }): Promise<void>
}
const AmmaAlarms = registerPlugin<AmmaAlarmsPlugin>('AmmaAlarms')

/** One alarm per minute: reminders due at the same time ring together. */
const minuteId = (at: number) => Math.floor(at / 60_000) % 2_000_000_000
const atTime = (date: Date, time: string) => { const [h, m] = time.split(':').map(Number); const d = new Date(date); d.setHours(h, m, 0, 0); return d.getTime() }

function label(r: { kind: ReminderKind; title: string }) {
  switch (r.kind) {
    case 'water': return '💧 Drink water'
    case 'meal': return `🍽️ ${r.title}`
    case 'tablet': return `💊 ${r.title}`
    default: return `${KIND_ICON[r.kind]} ${r.title}`
  }
}

export async function requestNotificationPermission() {
  if (!isNative()) return true
  try { return (await LocalNotifications.requestPermissions()).display === 'granted' } catch { return false }
}

/** Earlier versions scheduled plain notifications; remove them once so nothing rings twice. */
async function removeOldNotifications() {
  try {
    if (localStorage.getItem('amma.alarms.v2')) return
    const p = await LocalNotifications.getPending()
    if (p.notifications.length) await LocalNotifications.cancel({ notifications: p.notifications.map(n => ({ id: n.id })) })
    localStorage.setItem('amma.alarms.v2', '1')
  } catch { /* ignore */ }
}

/** Rebuilds all alarms on this phone. `todayDay` tells which of today's reminders are already done. */
export async function scheduleAll(reminders: Reminder[], todayDay: Day | undefined, appts: Appointment[]) {
  if (!isNative()) return
  await requestNotificationPermission()
  await removeOldNotifications()

  const now = Date.now()
  const start = today()
  const done = new Set((todayDay?.items ?? []).filter(i => i.status).map(i => `${i.reminderId}|${i.time.slice(0, 5)}`))
  type Group = { at: number; mains: [Reminder, AlarmItem][]; follows: [Reminder, AlarmItem][]; appts: Appointment[] }
  const groups = new Map<number, Group>()
  const group = (at: number) => {
    const id = minuteId(at)
    let g = groups.get(id)
    if (!g) groups.set(id, (g = { at, mains: [], follows: [], appts: [] }))
    return g
  }

  for (let d = 0; d < DAYS_AHEAD; d++) {
    const date = addDays(start, d)
    for (const r of reminders.filter(x => x.isActive)) {
      for (const time of r.times.map(t => t.slice(0, 5))) {
        if (d === 0 && done.has(`${r.id}|${time}`)) continue
        const at = atTime(date, time)
        const item: AlarmItem = { reminderId: r.id, time, date: iso(date) }
        if (at > now) group(at).mains.push([r, item])
        const follow = at + FOLLOW_UP_MIN * 60_000
        if (follow > now) group(follow).follows.push([r, { ...item, followUp: true }])
      }
    }
  }
  for (const a of appts.filter(x => x.status === 'scheduled')) {
    const t = new Date(`${a.date}T${a.time.slice(0, 5)}:00`).getTime()
    for (const mins of [a.reminderMinutesBefore, 120]) {
      const at = t - mins * 60_000
      if (at > now && at < now + 30 * 86_400_000) group(at).appts.push(a)
    }
  }

  const alarms: Alarm[] = []
  for (const [id, g] of groups) {
    let title: string, body: string
    if (g.mains.length) {
      title = g.mains.map(([r]) => label(r)).filter((v, i, a) => a.indexOf(v) === i).join(' · ')
      const notes = g.mains.map(([r]) => r.note).filter(Boolean).join(' · ')
      body = `${notes ? notes + '\n' : ''}${t12(g.mains[0][1].time)} — tap Done when finished. Both phones get this reminder.`
      if (g.follows.length) body += `\nAlso still pending: ${g.follows.map(([r]) => (r.kind === 'water' ? 'water' : r.title)).join(', ')}`
    } else if (g.follows.length) {
      title = `⏰ Still pending: ${g.follows.map(([r]) => (r.kind === 'water' ? 'water' : r.title)).join(', ')}`
      body = `Due at ${t12(g.follows[0][1].time)}. Nobody has marked it done yet — if it's done, tap Done; otherwise please remind her.`
    } else {
      const a = g.appts[0]
      title = `🏥 ${a.title} · ${a.date === iso(new Date(g.at)) ? 'today' : new Date(`${a.date}T00:00:00`).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric', month: 'short' })} ${t12(a.time)}`
      body = [a.doctorName, a.location].filter(Boolean).join(' · ') || 'Hospital checkup'
    }
    alarms.push({ id, at: g.at, title, body, items: [...g.mains, ...g.follows].map(([, it]) => it) })
  }
  const keepAlive = atTime(addDays(start, DAYS_AHEAD - 1), '10:00')
  if (keepAlive > now) alarms.push({ id: minuteId(keepAlive) + 1, at: keepAlive, quiet: true, items: [],
    title: 'Open Amma to keep reminders on time', body: 'Reminders are planned a few days ahead. Opening the app plans the next days.' })

  await AmmaAlarms.schedule({ alarms })
}

/** After an occurrence is done (here or on the other phone), stop and remove its alarms on this phone. */
export async function dismissOccurrence(reminderId: string, time: string) {
  if (!isNative()) return
  void reminderId
  const at = atTime(today(), time.slice(0, 5))
  await AmmaAlarms.dismiss({ ids: [minuteId(at), minuteId(at + FOLLOW_UP_MIN * 60_000)] }).catch(() => {})
}

/** "Done" taps made on the alarm screen while Amma was closed. */
export async function takeDoneActions(): Promise<DoneAction[]> {
  if (!isNative()) return []
  try { return (await AmmaAlarms.takeActions()).actions ?? [] } catch { return [] }
}

export async function alarmStatus(): Promise<AlarmStatus | null> {
  if (!isNative()) return null
  try { return await AmmaAlarms.status() } catch { return null }
}

export const openAlarmSettings = (which: 'exact' | 'fullscreen' | 'battery' | 'notifications' | 'app') => (isNative() ? AmmaAlarms.openSettings({ which }) : Promise.resolve())
export const setAlarmVolume = (percent: number) => (isNative() ? AmmaAlarms.setAlarmVolume({ percent }) : Promise.resolve())

/** A real alarm in a few seconds, so you can lock the phone and check it rings. */
export async function testAlarm(seconds = 10) {
  if (!isNative()) return false
  await requestNotificationPermission()
  await AmmaAlarms.test({ seconds })
  return true
}

/** Problems that stop alarms ringing on time, most important first. */
export function alarmProblems(s: AlarmStatus | null) {
  if (!s) return []
  const p: { key: string; text: string; fix: string; act: () => Promise<void> }[] = []
  if (!s.notifications) p.push({ key: 'n', text: 'Notifications are off for Amma, so reminders cannot show.', fix: 'Turn on', act: async () => { if (!(await requestNotificationPermission())) await openAlarmSettings('notifications') } })
  if (!s.exact) p.push({ key: 'e', text: 'Allow "Alarms & reminders" so reminders ring exactly on time.', fix: 'Allow', act: () => openAlarmSettings('exact') })
  if (!s.fullScreen) p.push({ key: 'f', text: 'Allow full-screen alarms so reminders show over the lock screen.', fix: 'Allow', act: () => openAlarmSettings('fullscreen') })
  if (!s.batteryUnrestricted) p.push({ key: 'b', text: 'Battery saving can delay reminders. Set Amma to "Unrestricted".', fix: 'Fix', act: () => openAlarmSettings('battery') })
  if (s.alarmVolumeMax > 0 && s.alarmVolume === 0) p.push({ key: 'v', text: 'Your alarm volume is 0, so reminders would be silent.', fix: 'Set to 70%', act: () => setAlarmVolume(70) })
  return p
}
