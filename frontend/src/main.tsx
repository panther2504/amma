import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient } from '@tanstack/react-query'
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client'
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister'
import { ApiError, OfflineError } from './api/client'
import { localDb } from './lib/localdb'
import App from './App'
import './styles/app.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      gcTime: 30 * 24 * 3600_000,
      // Try the network even if the phone reports offline; fall back to the on-device copy.
      networkMode: 'offlineFirst',
      retry: (n, e) => !(e instanceof ApiError && e.status < 500) && !(e instanceof OfflineError) && n < 2,
      refetchOnWindowFocus: true,
    },
    // Writes always run: daily logging saves to the on-device outbox when offline.
    mutations: { networkMode: 'always' },
  },
})

// Everything the app has loaded is kept in on-device SQLite (Android) so it opens offline.
const persister = createAsyncStoragePersister({
  storage: { getItem: k => localDb().get(k), setItem: (k, v) => localDb().set(k, v), removeItem: k => localDb().remove(k) },
  key: 'query-cache',
  throttleTime: 1500,
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <PersistQueryClientProvider client={queryClient} persistOptions={{ persister, maxAge: 30 * 24 * 3600_000, buster: 'v1' }}>
      <App />
    </PersistQueryClientProvider>
  </StrictMode>,
)
