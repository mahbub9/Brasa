import * as Sentry from '@sentry/react'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './i18n/i18n'
import './index.css'
import App from './App.tsx'
import { DevCrashTrigger } from './components/DevCrashTrigger.tsx'
import { ErrorFallback } from './components/ErrorFallback.tsx'
import { initErrorReporting } from './lib/errorReporting.ts'

initErrorReporting()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Sentry.ErrorBoundary fallback={<ErrorFallback />}>
      <DevCrashTrigger />
      <App />
    </Sentry.ErrorBoundary>
  </StrictMode>,
)
