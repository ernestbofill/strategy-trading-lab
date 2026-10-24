import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import type { ReactNode } from 'react'
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
import { AdminSettings } from './features/admin/AdminSettings'
import { Dashboard } from './features/dashboard/Dashboard'
import { Advanced } from './features/new-strategy/Advanced'
import { SetBudget } from './features/new-strategy/SetBudget'
import { ChooseMode } from './features/new-strategy/ChooseMode'
import { NewStrategyDraftProvider } from './features/new-strategy/NewStrategyDraft'
import { NotBuiltYet } from './features/new-strategy/NotBuiltYet'
import { Confirm } from './features/new-strategy/Confirm'
import { budgetPath, confirmPath, dashboardPath, homePath, pickPath } from './features/new-strategy/paths'
import { PickTemplate } from './features/new-strategy/PickTemplate'
import { StrategyPage } from './features/strategy/StrategyPage'
import { HowTested } from './features/new-strategy/review/HowTested'
import { ReviewTemplate } from './features/new-strategy/review/ReviewTemplate'

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
              <Route path="/new/mode" element={<ChooseMode />} />
              <Route path={budgetPath} element={<SetBudget />} />
              <Route path={confirmPath} element={<Confirm />} />
              <Route path="/dashboard" element={<Dashboard />} />
              <Route path={accountPath} element={<AccountPage />} />
              <Route element={<RequireOwner />}>
                <Route path="/admin" element={<AdminSettings />} />
              </Route>
              <Route path="/strategies/:id" element={<StrategyPage />} />
              <Route path="/strategies/:id/signals/:signalId" element={<NotBuiltYet title="Confirm a signal" back={dashboardPath} />} />
              <Route path="/strategies/:id/ai-round" element={<NotBuiltYet title="AI round" back={dashboardPath} />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </DraftPerUser>
      </AccountProvider>
    </BrowserRouter>
  )
}
