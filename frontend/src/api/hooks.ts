import { useMutation, useQuery, useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query'
import { get, isQueued } from './client'
import type * as T from './types'

/** Educational content. Cached on the device (with everything else) so it works offline. */
export function useContent(lang: string = 'en') {
  return useQuery({ queryKey: ['content', lang], queryFn: () => get<T.ContentBundle>(`/content/bundle?lang=${lang}`), staleTime: 10 * 60_000 })
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

/**
 * A mutation that refreshes the given queries when it succeeds.
 * `optimistic` updates the cached data immediately, so offline changes show up straight away;
 * when a change was queued offline the refresh is skipped until it syncs.
 */
export function useAction<A, R>(fn: (a: A) => Promise<R>, invalidate: QueryKey[] = [], onSuccess?: (r: R, a: A) => void, optimistic?: (qc: QueryClient, a: A) => void) {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: fn,
    onMutate: a => { optimistic?.(qc, a) },
    onSuccess: async (r, a) => {
      onSuccess?.(r, a)
      if (!isQueued(r)) await Promise.all(invalidate.map(k => qc.invalidateQueries({ queryKey: k })))
    },
    onError: () => { if (optimistic) invalidate.forEach(k => qc.invalidateQueries({ queryKey: k })) },
  })
}

/** Helper for optimistic cache edits. */
export function patchCache<D>(qc: QueryClient, key: QueryKey, fn: (d: D) => D) {
  qc.setQueryData<D>(key, d => (d === undefined ? d : fn(d)))
}
