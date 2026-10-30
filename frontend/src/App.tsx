import { useEffect, type ReactNode } from 'react'
import { BrowserRouter, Navigate, Outlet, Route, Routes, useLocation } from 'react-router-dom'
import { I18nProvider, useI18n } from './i18n/I18n'
import { AccountProvider, useAccount, useOptionalAccount } from './features/account/AccountContext'
import { AccountPage } from './features/account/AccountPage'
import { ForgotPassword } from './features/account/ForgotPassword'
import { LogIn } from './features/account/LogIn'
import {
  accountPath,
  forgotPasswordPath,
  logInPath,
  resetPasswordPath,
  signUpPath,
  verifyEmailPath,
} from './features/account/paths'
import { RequireAccount } from './features/account/RequireAccount'
import { ResetPassword } from './features/account/ResetPassword'
import { SignUp } from './features/account/SignUp'
import { VerifyEmail } from './features/account/VerifyEmail'
import { RequireOwner } from './features/account/RequireOwner'
import { FeedbackCapture } from './features/feedback/FeedbackCapture'
import { FeedbackPage } from './features/feedback/FeedbackSection'
import { Dashboard } from './features/dashboard/Dashboard'
import { Advanced } from './features/new-strategy/Advanced'
import { SetBudget } from './features/new-strategy/SetBudget'
import { ChooseMode } from './features/new-strategy/ChooseMode'
import { NewStrategyDraftProvider } from './features/new-strategy/NewStrategyDraft'
import { Confirm } from './features/new-strategy/Confirm'
import { budgetPath, confirmPath, homePath, pickPath } from './features/new-strategy/paths'
import { PickTemplate } from './features/new-strategy/PickTemplate'
import { SignalPage } from './features/signal/SignalPage'
import { StrategyPage } from './features/strategy/StrategyPage'
import { StrategySettings } from './features/strategy/StrategySettings'
import { HowTested } from './features/new-strategy/review/HowTested'
import { DraftPromptEditor } from './features/new-strategy/review/DraftPromptEditor'
import { AiRoundPage } from './features/ai-round/AiRoundPage'
import { AiRoundsPage } from './features/ai-round/AiRoundsPage'
import { StrategyPromptEditor } from './features/ai-round/StrategyPromptEditor'
import { ReviewTemplate } from './features/new-strategy/review/ReviewTemplate'
import { LazyPage } from './components/LazyPage'
import { useTemplates } from './features/new-strategy/useTemplate'
import { lazyPage } from './lib/lazyPage'
import { LegalPage } from './features/legal/LegalPage'
import { aboutPath, disclaimerPath, legalNoticePath, privacyPath, termsPath } from './features/legal/paths'

// Loaded only when an owner opens /admin, so users never download the admin pages or TanStack Table.
const AdminSettings = lazyPage(() => import('./features/admin/AdminSettings').then((m) => m.AdminSettings))
const AdminAssets = lazyPage(() => import('./features/admin/AdminAssets').then((m) => m.AdminAssets))
const AdminUsers = lazyPage(() => import('./features/admin/AdminUsers').then((m) => m.AdminUsers))

/** The new-strategy draft belongs to one user (or a guest): another user, or a log-out, starts it over. */
function DraftPerUser({ children }: { children: ReactNode }) {
  const owner = useOptionalAccount()?.email ?? null
  return <NewStrategyDraftProvider owner={owner}>{children}</NewStrategyDraftProvider>
}

/**
 * Takes the user's saved account language onto this device when the account loads or its language changes. It does not
 * pull the device back to the account while the user is switching (the account page saves, then reloads the account).
 */
function LocaleFromAccount() {
  const { state } = useAccount()
  const { setLocale } = useI18n()
  const accountLocale = state.status === 'ready' ? state.account?.locale : null
  useEffect(() => {
    if (accountLocale) setLocale(accountLocale)
  }, [accountLocale, setLocale])
  return null
}

/** Loads the template list in the background once signed in, so Pick a template opens at once from anywhere. */
function PreloadTemplates() {
  useTemplates()
  return null
}

function Preloads() {
  const { state } = useAccount()
  return state.status === 'ready' && state.account ? <PreloadTemplates /> : null
}

function FeedbackCaptureIfLoggedIn() {
  return useOptionalAccount() ? <FeedbackCapture /> : null
}

/** Root layout providing in-app feedback capture across all screens for logged-in users with verified email. */
function AppLayout() {
  const { pathname } = useLocation()
  return (
    <>
      <Outlet />
      <FeedbackCaptureIfLoggedIn key={pathname} />
    </>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <I18nProvider>
        <AccountProvider>
          <LocaleFromAccount />
          <Preloads />
          <DraftPerUser>
            <Routes>
              <Route element={<AppLayout />}>
                <Route path={signUpPath} element={<SignUp />} />
                <Route path={logInPath} element={<LogIn />} />
                <Route path={forgotPasswordPath} element={<ForgotPassword />} />
                <Route path={resetPasswordPath} element={<ResetPassword />} />
                <Route path={verifyEmailPath} element={<VerifyEmail />} />

                <Route path={legalNoticePath} element={<LegalPage section="notice" />} />
                <Route path={termsPath} element={<LegalPage section="terms" />} />
                <Route path={privacyPath} element={<LegalPage section="privacy" />} />
                <Route path={disclaimerPath} element={<LegalPage section="disclaimer" />} />
                <Route path={aboutPath} element={<LegalPage section="about" />} />

                {/* Public new-strategy flow: guests can look around and configure a draft without an account */}
                <Route path={pickPath} element={<PickTemplate />} />
                <Route path="/new/templates/:key" element={<ReviewTemplate />} />
                <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
                <Route path="/new/templates/:key/advanced" element={<Advanced />} />
                <Route path="/new/templates/:key/prompt" element={<DraftPromptEditor />} />
                <Route path="/new/mode" element={<ChooseMode />} />
                <Route path={budgetPath} element={<SetBudget />} />
                <Route path={confirmPath} element={<Confirm />} />

                {/* Routes requiring an authenticated account */}
                <Route element={<RequireAccount />}>
                  <Route path={homePath} element={<Dashboard />} />
                  <Route path="/dashboard" element={<Dashboard />} />
                  <Route path={accountPath} element={<AccountPage />} />
                  <Route element={<RequireOwner />}>
                    <Route
                      path="/admin"
                      element={
                        <LazyPage back="/">
                          <AdminSettings />
                        </LazyPage>
                      }
                    />
                    <Route
                      path="/admin/users"
                      element={
                        <LazyPage back="/admin">
                          <AdminUsers />
                        </LazyPage>
                      }
                    />
                    <Route
                      path="/admin/assets"
                      element={
                        <LazyPage back="/admin">
                          <AdminAssets />
                        </LazyPage>
                      }
                    />
                    <Route path="/feedback" element={<FeedbackPage />} />
                  </Route>
                  <Route path="/strategies/:id" element={<StrategyPage />} />
                  <Route path="/strategies/:id/settings" element={<StrategySettings />} />
                  <Route path="/strategies/:id/signals/:signalId" element={<SignalPage />} />
                  <Route path="/strategies/:id/ai-round" element={<AiRoundPage />} />
                  <Route path="/strategies/:id/ai-rounds" element={<AiRoundsPage />} />
                  <Route path="/strategies/:id/prompt" element={<StrategyPromptEditor />} />
                </Route>
                <Route path="*" element={<Navigate to="/" replace />} />
              </Route>
            </Routes>
          </DraftPerUser>
        </AccountProvider>
      </I18nProvider>
    </BrowserRouter>
  )
}
