export const DAY = 86_400_000
export const pad = (n: number) => String(n).padStart(2, '0')
export const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
export const parseDate = (s: string) => new Date(s + 'T00:00:00')
export const today = () => { const d = new Date(); d.setHours(0, 0, 0, 0); return d }
export const addDays = (d: Date, n: number) => new Date(d.getTime() + n * DAY)
export const daysUntil = (s: string) => Math.round((parseDate(s).getTime() - today().getTime()) / DAY)
export const fmt = (d: Date | string, o: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short', year: 'numeric' }) =>
  (typeof d === 'string' ? parseDate(d) : d).toLocaleDateString('en-IN', o)
export const fmtDateTime = (s: string, o: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' }) => new Date(s).toLocaleDateString('en-IN', o)
export const clamp = (v: number, a: number, b: number) => Math.max(a, Math.min(b, v))
/** "14:00:00" or "14:00" -> "2:00 PM" */
export const t12 = (s: string) => { const [hh, mm] = s.split(':').map(Number); const ap = hh >= 12 ? 'PM' : 'AM'; return `${hh % 12 || 12}:${pad(mm)} ${ap}` }
export const hhmm = (s: string) => s.slice(0, 5)
export const ordinal = (n: number) => `${n}${['st', 'nd', 'rd'][n - 1] ?? 'th'}`
export const triOf = (w: number) => (w < 14 ? 1 : w < 28 ? 2 : 3)
export const relDays = (n: number) => (n === 0 ? 'Today' : n === 1 ? 'Tomorrow' : n < 0 ? `${-n} days ago` : `In ${n} days`)
export const bytes = (n: number) => (n > 1_048_576 ? `${(n / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(n / 1024))} KB`)
export const vibrate = (ms: number) => { try { navigator.vibrate?.(ms) } catch { /* unsupported */ } }

/** Converts a period date or doctor's due date into LMP, mirroring the backend (EDD = LMP + 280 days). */
export const lmpFrom = (mode: 'lmp' | 'edd', value: string) => (mode === 'lmp' ? value : iso(addDays(parseDate(value), -280)))
export function gestationFromLmp(lmp: string) {
  const days = Math.round((today().getTime() - parseDate(lmp).getTime()) / DAY)
  return { days, weeks: Math.floor(days / 7), rem: days % 7, edd: addDays(parseDate(lmp), 280), plausible: days >= 14 && days <= 300 }
}
