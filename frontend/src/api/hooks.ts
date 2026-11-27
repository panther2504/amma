import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { get } from './client'
import type * as T from './types'

const CONTENT_CACHE = 'amma.content.v1'

function cachedContent(lang: string): T.ContentBundle | undefined {
  try {
    const raw = localStorage.getItem(`${CONTENT_CACHE}.${lang}`)
    return raw ? (JSON.parse(raw) as T.ContentBundle) : undefined
  } catch { return undefined }
}

/** Educational content, cached on the device so essential information works offline. */
export function useContent(lang: string = 'en') {
  return useQuery({
    queryKey: ['content', lang],
    queryFn: async () => {
      const b = await get<T.ContentBundle>(`/content/bundle?lang=${lang}`)
      try { localStorage.setItem(`${CONTENT_CACHE}.${lang}`, JSON.stringify(b)) } catch { /* quota */ }
      return b
    },
    initialData: () => cachedContent(lang),
    initialDataUpdatedAt: 0,
    staleTime: 10 * 60_000,
  })
}

export const useDashboard = () => useQuery({ queryKey: ['dashboard'], queryFn: () => get<T.Dashboard>('/dashboard') })
export const useMe = () => useQuery({ queryKey: ['me'], queryFn: () => get<T.User>('/me') })
export const usePregnancy = () => useQuery({ queryKey: ['pregnancy'], queryFn: () => get<T.Pregnancy>('/pregnancy'), retry: false })
export const useDoses = () => useQuery({ queryKey: ['doses'], queryFn: () => get<T.Dose[]>('/medications/doses') })
export const useMedications = () => useQuery({ queryKey: ['meds'], queryFn: () => get<T.Medication[]>('/medications') })
export const useAppointments = () => useQuery({ queryKey: ['appts'], queryFn: () => get<T.Appointment[]>('/appointments') })
export const useHealth = () => useQuery({ queryKey: ['health'], queryFn: () => get<T.HealthSummary>('/health/summary') })
export const useKicks = () => useQuery({ queryKey: ['kicks'], queryFn: () => get<T.KickSession[]>('/health/kick-sessions') })
export const useBag = () => useQuery({ queryKey: ['bag'], queryFn: () => get<T.HospitalBag>('/tracking/hospital-bag') })
export const useFamily = () => useQuery({ queryKey: ['family'], queryFn: () => get<T.FamilyMember[]>('/family') })
export const useSharedWithMe = () => useQuery({ queryKey: ['shared'], queryFn: () => get<T.SharedJourney[]>('/family/shared-with-me') })
export const useDocuments = () => useQuery({ queryKey: ['docs'], queryFn: () => get<T.MedicalDocument[]>('/documents') })
export const useNotifications = () => useQuery({ queryKey: ['notifs'], queryFn: () => get<T.AppNotification[]>('/notifications') })
export const useNotificationPrefs = () => useQuery({ queryKey: ['prefs'], queryFn: () => get<T.NotificationPrefs>('/notifications/preferences') })
export const useConsents = () => useQuery({ queryKey: ['consents'], queryFn: () => get<T.Consent[]>('/me/consents') })
export const useContacts = () => useQuery({ queryKey: ['contacts'], queryFn: () => get<T.EmergencyContact[]>('/me/emergency-contacts') })
export const useChatHistory = () => useQuery({ queryKey: ['chat'], queryFn: () => get<T.AssistantMessage[]>('/assistant/messages'), staleTime: Infinity })

/** A mutation that refreshes the given queries when it succeeds. */
export function useAction<A, R>(fn: (a: A) => Promise<R>, invalidate: QueryKey[] = [], onSuccess?: (r: R, a: A) => void) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onSuccess: async (r, a) => {
      onSuccess?.(r, a)
      await Promise.all(invalidate.map(k => qc.invalidateQueries({ queryKey: k })))
    },
  })
}
