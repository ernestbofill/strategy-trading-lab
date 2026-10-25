import { lazy, Suspense, type ReactNode } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AccountProvider, useAccount } from './features/account/AccountContext'
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

// Loaded only when an owner opens /admin, so users never download the admin pages or TanStack Table.
const AdminSettings = lazy(() => import('./features/admin/AdminSettings').then((m) => ({ default: m.AdminSettings })))
const AdminAssets = lazy(() => import('./features/admin/AdminAssets').then((m) => ({ default: m.AdminAssets })))

/** The new-strategy draft belongs to one user: another user logging in on this tab starts it over. */
function DraftPerUser({ children }: { children: ReactNode }) {
  const { owner } = useAccount()
  return <NewStrategyDraftProvider owner={owner}>{children}</NewStrategyDraftProvider>
}

export default function App() {
  return (
    <BrowserRouter>
      <AccountProvider>
        <DraftPerUser>
          <Routes>
            <Route path={signUpPath} element={<SignUp />} />
            <Route path={logInPath} element={<LogIn />} />
            <Route path={forgotPasswordPath} element={<ForgotPassword />} />
            <Route path={resetPasswordPath} element={<ResetPassword />} />
            <Route path={verifyEmailPath} element={<VerifyEmail />} />
            <Route element={<RequireAccount />}>
              <Route path={homePath} element={<Dashboard />} />
              <Route path={pickPath} element={<PickTemplate />} />
              <Route path="/new/templates/:key" element={<ReviewTemplate />} />
              <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
              <Route path="/new/templates/:key/advanced" element={<Advanced />} />
              <Route path="/new/templates/:key/prompt" element={<DraftPromptEditor />} />
              <Route path="/new/mode" element={<ChooseMode />} />
              <Route path={budgetPath} element={<SetBudget />} />
              <Route path={confirmPath} element={<Confirm />} />
              <Route path="/dashboard" element={<Dashboard />} />
              <Route path={accountPath} element={<AccountPage />} />
              <Route element={<RequireOwner />}>
                <Route
                  path="/admin"
                  element={
                    <Suspense fallback={null}>
                      <AdminSettings />
                    </Suspense>
                  }
                />
                <Route
                  path="/admin/assets"
                  element={
                    <Suspense fallback={null}>
                      <AdminAssets />
                    </Suspense>
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
          </Routes>
        </DraftPerUser>
      </AccountProvider>
    </BrowserRouter>
  )
}
