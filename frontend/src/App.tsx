import { BrowserRouter, Navigate, Route, Routes, useParams } from 'react-router-dom'
import { AccountProvider } from './features/account/AccountContext'
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
import { AdminSettings } from './features/admin/AdminSettings'
import { Dashboard } from './features/dashboard/Dashboard'
import { SetBudget } from './features/new-strategy/SetBudget'
import { ChooseMode } from './features/new-strategy/ChooseMode'
import { NewStrategyDraftProvider } from './features/new-strategy/NewStrategyDraft'
import { NotBuiltYet } from './features/new-strategy/NotBuiltYet'
import { Confirm } from './features/new-strategy/Confirm'
import { budgetPath, confirmPath, dashboardPath, reviewPath } from './features/new-strategy/paths'
import { PickTemplate } from './features/new-strategy/PickTemplate'
import { HowTested } from './features/new-strategy/review/HowTested'
import { ReviewTemplate } from './features/new-strategy/review/ReviewTemplate'

function AdvancedStub() {
  const { key = '' } = useParams()
  return <NotBuiltYet title="Advanced settings" back={reviewPath(key)} />
}

export default function App() {
  return (
    <BrowserRouter>
      <AccountProvider>
        <NewStrategyDraftProvider>
          <Routes>
            <Route path={signUpPath} element={<SignUp />} />
            <Route path={logInPath} element={<LogIn />} />
            <Route path={forgotPasswordPath} element={<ForgotPassword />} />
            <Route path={resetPasswordPath} element={<ResetPassword />} />
            <Route path={verifyEmailPath} element={<VerifyEmail />} />
            <Route path="/admin" element={<AdminSettings />} />
            <Route element={<RequireAccount />}>
              <Route path="/" element={<PickTemplate />} />
              <Route path="/new/templates/:key" element={<ReviewTemplate />} />
              <Route path="/new/templates/:key/how-tested" element={<HowTested />} />
              <Route path="/new/templates/:key/advanced" element={<AdvancedStub />} />
              <Route path="/new/mode" element={<ChooseMode />} />
              <Route path={budgetPath} element={<SetBudget />} />
              <Route path={confirmPath} element={<Confirm />} />
              <Route path={dashboardPath} element={<Dashboard />} />
              <Route path={accountPath} element={<AccountPage />} />
              <Route path="/strategies/:id" element={<NotBuiltYet title="Strategy page" back={dashboardPath} />} />
              <Route path="/strategies/:id/signal" element={<NotBuiltYet title="Confirm a signal" back={dashboardPath} />} />
              <Route path="/strategies/:id/ai-round" element={<NotBuiltYet title="AI round" back={dashboardPath} />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </NewStrategyDraftProvider>
      </AccountProvider>
    </BrowserRouter>
  )
}
